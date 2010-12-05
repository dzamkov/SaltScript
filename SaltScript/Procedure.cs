using System;
using System.Collections.Generic;

namespace SaltScript
{
    /// <summary>
    /// An expression represented by a procedure.
    /// </summary>
    public class ProcedureExpression : Expression
    {
        private ProcedureExpression()
        {

        }

        /// <summary>
        /// Prepares a parsed procedure.
        /// </summary>
        public static ProcedureExpression Prepare(Parser.ProcedureExpression ParsedExpression, Scope Scope, ProgramInput Input)
        {
            ProcedureExpression pe = new ProcedureExpression();
            Parser.Statement s = ParsedExpression.Statement;

            // Prepare cloned variable list
            Dictionary<string, int> clonemap = new Dictionary<string, int>();
            List<VariableIndex> rclonemap = new List<VariableIndex>();
            int firstfree = Scope.NextFreeIndex.StackIndex;
            int nextfree = firstfree;
            Statement.PrepareClonedVariables(s, Scope, ref nextfree, clonemap, rclonemap);
            if (clonemap.Count > 0)
            {
                pe._ClonedVars = rclonemap.ToArray();
                Scope = new Scope() { Parent = Scope, FunctionalDepth = Scope.FunctionalDepth, Variables = clonemap };
            }

            // Prepare statements
            Parser.CompoundStatement cs = s as Parser.CompoundStatement;
            if(cs != null)
            {
                pe._Statement = CompoundStatement.Prepare(cs, Scope, Input, ref nextfree);
                return pe;
            }

            throw new NotImplementedException();
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            if (this._ClonedVars != null)
            {
                Value[] initvals = new Value[this._ClonedVars.Length];
                for (int t = 0; t < this._ClonedVars.Length; t++)
                {
                    initvals[t] = Stack.Lookup(this._ClonedVars[t]);
                }
            }
            return this._Statement.Call(Stack);
        }

        public override void TypeCheck(
            VariableStack<Expression> TypeStack,
            VariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            if (this._ClonedVars != null)
            {
                Expression[] typestackappend = new Expression[this._ClonedVars.Length];
                Expression[] stackappend = new Expression[this._ClonedVars.Length];
                for (int t = 0; t < this._ClonedVars.Length; t++)
                {
                    VariableIndex vi = this._ClonedVars[t];
                    stackappend[t] = Expression.Lookup(vi, Stack);
                    typestackappend[t] = TypeStack.Lookup(vi);
                }
                Stack = Stack.Append(stackappend);
                TypeStack = TypeStack.Append(typestackappend);
            }

            Statement sstatement;
            Expression returntype;
            this._Statement.TypeCheck(TypeStack, Stack, out sstatement, out returntype);

            if (returntype == null)
            {
                throw new NotImplementedException();
            }

            TypeSafeExpression = new ProcedureExpression() { _Statement = sstatement, _ClonedVars = this._ClonedVars };
            Type = returntype;
        }

        private VariableIndex[] _ClonedVars;
        private Statement _Statement;
    }

    /// <summary>
    /// A single instruction within a procedure.
    /// </summary>
    public abstract class Statement
    {
        /// <summary>
        /// Gets the "Cloned" variables in a statement. Cloned variables are defined outside of the procedure, but assigned within it. Since the
        /// procedure cannot modify variables outside of itself, these variables are cloned into the procedure and used instead of the variables
        /// outside the procedure.
        /// </summary>
        public static void PrepareClonedVariables(Parser.Statement Statement, Scope Scope, ref int NextFree, Dictionary<string, int> ClonedVarMap, List<VariableIndex> RClonedVarMap)
        {
            Parser.AssignStatement aas = Statement as Parser.AssignStatement;
            if(aas != null)
            {
                VariableIndex vi;
                if (Scope.LookupVariable(aas.Variable, out vi))
                {
                    RClonedVarMap.Add(vi);
                    ClonedVarMap.Add(aas.Variable, NextFree);
                    NextFree++;
                }
                return;
            }

            Parser.CompoundStatement cs = Statement as Parser.CompoundStatement;
            if (cs != null)
            {
                for (int t = 0; t < cs.Statements.Count; t++)
                {
                    PrepareClonedVariables(cs.Statements[t], Scope, ref NextFree, ClonedVarMap, RClonedVarMap);
                }
            }
        }

        /// <summary>
        /// Insures this statement is type-correct.
        /// </summary>
        public abstract void TypeCheck(
            VariableStack<Expression> TypeStack,
            VariableStack<Expression> Stack,
            out Statement TypeSafeStatement, out Expression ReturnType);

        /// <summary>
        /// Calls (runs) the statement with the specified mutable stack. Returns a value if this statement returns.
        /// </summary>
        public abstract Value Call(VariableStack<Value> Stack);
    }

    /// <summary>
    /// A statement formed from multiple substatements.
    /// </summary>
    public class CompoundStatement : Statement
    {
        private CompoundStatement()
        {

        }

        /// <summary>
        /// Prepares a parsed compound statement.
        /// </summary>
        public static CompoundStatement Prepare(
            Parser.CompoundStatement CompoundStatement,
            Scope Scope, 
            ProgramInput Input, 
            ref int NextFreeVariable)
        {
            Dictionary<string, int> vars;
            int fd = Scope.FunctionalDepth;
            Scope = new Scope() { Parent = Scope, FunctionalDepth = fd, Variables = vars = new Dictionary<string,int>() };

            CompoundStatement cs = new CompoundStatement();
            cs._DefinedTypesByStatement = new Dictionary<int, KeyValuePair<int, Expression>>();
            cs._Substatements = new Statement[CompoundStatement.Statements.Count];
            int ss = 0;

            for (int t = 0; t < CompoundStatement.Statements.Count; t++ )
            {
                Parser.Statement ps = CompoundStatement.Statements[t];

                Parser.DefineStatement ds = ps as Parser.DefineStatement;
                if (ds != null)
                {
                    cs._DefinedTypesByStatement.Add(t, new KeyValuePair<int, Expression>(ss, Expression.Prepare(ds.Type, Scope, Input)));
                    cs._Substatements[t] = new SetStatement(new VariableIndex(NextFreeVariable, fd), Expression.Prepare(ds.Value, Scope, Input));
                    vars.Add(ds.Variable, NextFreeVariable);
                    NextFreeVariable++; ss++;
                    continue;
                }

                Parser.AssignStatement aas = ps as Parser.AssignStatement;
                if (aas != null)
                {
                    VariableIndex index;
                    if (Scope.LookupVariable(aas.Variable, out index))
                    {
                        cs._Substatements[t] = new SetStatement(index, Expression.Prepare(aas.Value, Scope, Input));
                    }
                    else
                    {
                        // Variable doesnt exist
                        throw new NotImplementedException();
                    }
                    continue;
                }

                Parser.ReturnStatement rs = ps as Parser.ReturnStatement;
                if (rs != null)
                {
                    cs._Substatements[t] = new ReturnStatement(Expression.Prepare(rs.Value, Scope, Input));
                    continue;
                }

                throw new NotImplementedException();
            }

            return cs;
        }

        public override Value Call(VariableStack<Value> Stack)
        {
            if (this._DefinedTypesByStatement.Count > 0)
            {
                Stack = Stack.Append(this._DefinedTypesByStatement.Count);
            }
            for (int t = 0; t < this._Substatements.Length; t++)
            {
                Value val = this._Substatements[t].Call(Stack);
                if (val != null)
                {
                    return val;
                }
            }
            return null;
        }

        public override void TypeCheck(
            VariableStack<Expression> TypeStack, 
            VariableStack<Expression> Stack, 
            out Statement TypeSafeStatement, out Expression ReturnType)
        {
            Expression[] types = new Expression[this._DefinedTypesByStatement.Count];
            TypeStack = TypeStack.Append(types);
            Stack = Stack.Append(new Expression[this._DefinedTypesByStatement.Count]);

            Statement[] nsubs = new Statement[this._Substatements.Length];

            ReturnType = null;
            bool hasreturn = false;
            for (int t = 0; t < this._Substatements.Length; t++)
            {
                KeyValuePair<int, Expression> defineinfo;
                if (this._DefinedTypesByStatement.TryGetValue(t, out defineinfo))
                {
                    types[defineinfo.Key] = defineinfo.Value.Substitute(Stack);
                }

                Statement s = this._Substatements[t];
                s.TypeCheck(TypeStack, Stack, out nsubs[t], out ReturnType);
                if (ReturnType != null)
                {
                    if (hasreturn)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        hasreturn = true;
                    }
                }
            }

            TypeSafeStatement = new CompoundStatement() { _Substatements = nsubs, _DefinedTypesByStatement = this._DefinedTypesByStatement };
        }

        private Dictionary<int, KeyValuePair<int, Expression>> _DefinedTypesByStatement;
        private Statement[] _Substatements;
    }

    /// <summary>
    /// A statement that sets a variable on the stack.
    /// </summary>
    public class SetStatement : Statement
    {
        public SetStatement(VariableIndex Variable, Expression Value)
        {
            this._Variable = Variable;
            this._Value = Value;
        }

        public override Value Call(VariableStack<Value> Stack)
        {
            Stack.Modify(this._Variable, this._Value.Evaluate(Stack));
            return null;
        }

        public override void TypeCheck(
            VariableStack<Expression> TypeStack, 
            VariableStack<Expression> Stack, 
            out Statement TypeSafeStatement, out Expression ReturnType)
        {
            Expression sval;
            Expression valtype;
            this._Value.TypeCheck(TypeStack, Stack, out sval, out valtype);

            if (Expression.Equivalent(TypeStack.Lookup(this._Variable), valtype.Reduce(Stack.NextIndex)))
            {
                Stack.Modify(this._Variable, sval.Substitute(Stack));
                TypeSafeStatement = new SetStatement(this._Variable, sval);
                ReturnType = null;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private VariableIndex _Variable;
        private Expression _Value;
    }

    /// <summary>
    /// A statement that invariably causes a value to be returned.
    /// </summary>
    public class ReturnStatement : Statement
    {
        public ReturnStatement(Expression ReturnValue)
        {
            this._Value = ReturnValue;
        }

        public override Value Call(VariableStack<Value> Stack)
        {
            return this._Value.Evaluate(Stack);
        }

        /// <summary>
        /// Gets the value to be returned.
        /// </summary>
        public Expression ReturnValue
        {
            get
            {
                return this._Value;
            }
        }

        public override void TypeCheck(
            VariableStack<Expression> TypeStack, 
            VariableStack<Expression> Stack, 
            out Statement TypeSafeStatement, out Expression ReturnType)
        {
            Expression sval;
            Expression valtype;
            this._Value.TypeCheck(TypeStack, Stack, out sval, out valtype);
            TypeSafeStatement = new ReturnStatement(sval);
            ReturnType = valtype;
        }

        private Expression _Value;
    }

    /// <summary>
    /// An exception that is thrown when the type check of a procedure reveals that it has inconsistent return types.
    /// </summary>
    public class MultipleTypeException : TypeCheckException
    {
        public MultipleTypeException(Expression Expression)
            : base(Expression)
        {

        }
    }
}