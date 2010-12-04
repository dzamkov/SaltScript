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

        public override void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type)
        {
            TypeSafeExpression = this;
            Type = null;
            //throw new NotImplementedException();
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
            int sv;
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