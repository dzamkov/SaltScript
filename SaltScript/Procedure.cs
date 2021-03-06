﻿using System;
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
            List<int> rclonemap = new List<int>();
            int firstfree = Scope.NextFreeIndex;
            int nextfree = firstfree;
            Statement.PrepareClonedVariables(s, Scope, ref nextfree, clonemap, rclonemap);
            if (clonemap.Count > 0)
            {
                pe._ClonedVars = rclonemap.ToArray();
                Scope = new Scope() { Parent = Scope, Variables = clonemap, NextFreeIndex = nextfree };
            }

            // Prepare statements
            Parser.CompoundStatement cs = s as Parser.CompoundStatement;
            if(cs != null)
            {
                pe._Statement = CompoundStatement.Prepare(cs, Scope, Input);
                return pe;
            }

            throw new NotImplementedException();
        }

        public override Value Evaluate(IMutableVariableStack<Value> Stack)
        {
            if (this._ClonedVars != null)
            {
                Value[] initvals = new Value[this._ClonedVars.Length];
                for (int t = 0; t < this._ClonedVars.Length; t++)
                {
                    Stack.Lookup(this._ClonedVars[t], ref initvals[t]);
                }
            }
            return this._Statement.Call(Stack);
        }

        public override void TypeCheck(
            IVariableStack<Expression> TypeStack,
            IVariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            int pi = Stack.NextFreeIndex;
            if (this._ClonedVars != null)
            {
                Expression[] typestackappend = new Expression[this._ClonedVars.Length];
                Expression[] stackappend = new Expression[this._ClonedVars.Length];
                for (int t = 0; t < this._ClonedVars.Length; t++)
                {
                    int vi = this._ClonedVars[t];
                    stackappend[t] = Expression.Lookup(vi, Stack);
                    TypeStack.Lookup(vi, ref typestackappend[t]);
                }
                Stack = Stack.Append(stackappend);
                TypeStack = TypeStack.Append(typestackappend);
            }

            Statement sstatement;
            Expression returntype;
            ProcedureMap init = ProcedureMap.Initial;
            this._Statement.TypeCheck(TypeStack, Stack, pi, ref init, out sstatement, out returntype);

            if (returntype == null)
            {
                throw new NotImplementedException();
            }

            TypeSafeExpression = new ProcedureExpression() { _Statement = sstatement, _ClonedVars = this._ClonedVars };
            Type = returntype;
        }

        private int[] _ClonedVars;
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
        public static void PrepareClonedVariables(Parser.Statement Statement, Scope Scope, ref int NextFree, Dictionary<string, int> ClonedVarMap, List<int> RClonedVarMap)
        {
            Parser.AssignStatement aas = Statement as Parser.AssignStatement;
            if(aas != null)
            {
                int vi;
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
        /// Insures this statement is type-correct. The value stack may be changed over multiple statements.
        /// Returns true on unconditional return.
        /// </summary>
        public abstract bool TypeCheck(
            IVariableStack<Expression> TypeStack,
            IVariableStack<Expression> Stack,
            int ProcedureIndex,
            ref ProcedureMap ProcedureMap,
            out Statement TypeSafeStatement, out Expression ReturnType);

        /// <summary>
        /// Calls (runs) the statement with the specified mutable stack. Returns a value if this statement returns.
        /// </summary>
        public abstract Value Call(IMutableVariableStack<Value> Stack);
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
            ProgramInput Input)
        {
            CompoundStatement cs = new CompoundStatement();
            cs._DefinedTypesByStatement = new Dictionary<int, KeyValuePair<int, Expression>>();
            cs._Substatements = new Statement[CompoundStatement.Statements.Count];
            int ss = 0;
            int sf = Scope.NextFreeIndex;
            int defined = _Defined(CompoundStatement.Statements);

            Dictionary<string, int> vars;
            Scope = new Scope() { Parent = Scope, Variables = vars = new Dictionary<string, int>(), NextFreeIndex = sf + defined };

            for (int t = 0; t < CompoundStatement.Statements.Count; t++ )
            {
                Parser.Statement ps = CompoundStatement.Statements[t];

                Parser.DefineStatement ds = ps as Parser.DefineStatement;
                if (ds != null)
                {
                    int vs = ss + sf;
                    cs._DefinedTypesByStatement.Add(t, new KeyValuePair<int, Expression>(ss, Expression.Prepare(ds.Type, Scope, Input)));
                    cs._Substatements[t] = new SetStatement(vs, Expression.Prepare(ds.Value, Scope, Input));
                    vars.Add(ds.Variable, vs);
                    ss++;
                    continue;
                }

                Parser.AssignStatement aas = ps as Parser.AssignStatement;
                if (aas != null)
                {
                    int index;
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

        /// <summary>
        /// Gets the amount of defined variables in the statements.
        /// </summary>
        private static int _Defined(IEnumerable<Parser.Statement> Statements)
        {
            int c = 0;
            foreach (var statement in Statements)
            {
                if (statement is Parser.DefineStatement)
                {
                    c++;
                }
            }
            return c;
        }

        public override Value Call(IMutableVariableStack<Value> Stack)
        {
            if (this._DefinedTypesByStatement.Count > 0)
            {
                Stack = Stack.AppendMutable(new Value[this._DefinedTypesByStatement.Count]);
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

        public override bool TypeCheck(
            IVariableStack<Expression> TypeStack, 
            IVariableStack<Expression> Stack,
            int ProcedureIndex,
            ref ProcedureMap ProcedureMap,
            out Statement TypeSafeStatement, out Expression ReturnType)
        {
            Expression[] types = new Expression[this._DefinedTypesByStatement.Count];
            TypeStack = TypeStack.Append(types);
            Stack = Stack.Append(new Expression[this._DefinedTypesByStatement.Count]);

            Statement[] nsubs = new Statement[this._Substatements.Length];

            ReturnType = null;
            bool unconditionalreturn = false;
            bool hasreturn = false;
            for (int t = 0; t < this._Substatements.Length; t++)
            {
                KeyValuePair<int, Expression> defineinfo;
                if (this._DefinedTypesByStatement.TryGetValue(t, out defineinfo))
                {
                    types[defineinfo.Key] = defineinfo.Value.Substitute(ProcedureMap.MakeMap(ProcedureIndex));
                }

                Statement s = this._Substatements[t];
                unconditionalreturn |= s.TypeCheck(
                    TypeStack, Stack, ProcedureIndex,
                    ref ProcedureMap, out nsubs[t], out ReturnType);
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
            return unconditionalreturn;
        }

        private Dictionary<int, KeyValuePair<int, Expression>> _DefinedTypesByStatement;
        private Statement[] _Substatements;
    }

    /// <summary>
    /// A statement that sets a variable on the stack.
    /// </summary>
    public class SetStatement : Statement
    {
        public SetStatement(int Variable, Expression Value)
        {
            this._Variable = Variable;
            this._Value = Value;
        }

        public override Value Call(IMutableVariableStack<Value> Stack)
        {
            Stack.Modify(this._Variable, this._Value.Evaluate(Stack));
            return null;
        }

        public override bool TypeCheck(
            IVariableStack<Expression> TypeStack, 
            IVariableStack<Expression> Stack,
            int ProcedureIndex,
            ref ProcedureMap ProcedureMap,
            out Statement TypeSafeStatement, out Expression ReturnType)
        {
            Expression sval;
            Expression valtype;
            this._Value.TypeCheck(TypeStack, Stack, out sval, out valtype);

            Expression vartype = null;
            TypeStack.Lookup(this._Variable, ref vartype);

            Expression nvartype = vartype;
            FuzzyBool typeokay = Expression.Equivalent(ref nvartype, ref valtype, Stack);
            if (nvartype != vartype)
            {
                // nvartype is a reduced form of vartype, save it.
                //TypeStack.Modify(this._Variable, nvartype);
            }
            if (typeokay == FuzzyBool.True)
            {
                TypeSafeStatement = new SetStatement(this._Variable, sval);
                ReturnType = null;
            }
            else
            {
                throw new NotImplementedException();
            }
            ProcedureMap = ProcedureMap.CreateSetMap(ProcedureMap, this._Variable, sval);
            return false;
        }

        private int _Variable;
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

        public override Value Call(IMutableVariableStack<Value> Stack)
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

        public override bool TypeCheck(
            IVariableStack<Expression> TypeStack, 
            IVariableStack<Expression> Stack,
            int ProcedureIndex,
            ref ProcedureMap ProcedureMap,
            out Statement TypeSafeStatement, out Expression ReturnType)
        {
            Expression sval;
            Expression valtype;
            this._Value.TypeCheck(TypeStack, Stack, out sval, out valtype);
            TypeSafeStatement = new ReturnStatement(sval);
            ReturnType = valtype;
            return true;
        }

        private Expression _Value;
    }

    /// <summary>
    /// A variable map that computes the expression values of local variables in the procedure as they are needed. (Because most of the time, at least while type
    /// checking, they aren't).
    /// </summary>
    public abstract class ProcedureMap
    {
        private ProcedureMap()
        {
        }

        /// <summary>
        /// Gets the expression for the specified variable, given the stack for the previous state.
        /// </summary>
        protected abstract bool Compute(
            int ProcedureIndex,
            int Index,
            ref Expression Value);

        /// <summary>
        /// Creates a variable map object given the procedure index, the index of the first variable in
        /// the procedure.
        /// </summary>
        public IVariableMap<Expression> MakeMap(int ProcedureIndex)
        {
            return new _Map()
            {
                ProcedureIndex = ProcedureIndex,
                Source = this
            };
        }

        private class _Map : IVariableMap<Expression>
        {
            public bool Lookup(int Index, ref Expression Value)
            {
                return this.Source.Compute(this.ProcedureIndex, Index, ref Value);
            }

            public int ProcedureIndex;
            public ProcedureMap Source;
        }

        /// <summary>
        /// An initial procedure map with no variables defined.
        /// </summary>
        public static readonly ProcedureMap Initial = new _InitialProcedureMap();

        private class _InitialProcedureMap : ProcedureMap
        {
            protected override bool Compute(
                int ProcedureIndex,
                int Index,
                ref Expression Value)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a stack representation of the procedure map by appending variables defined in the
        /// procedure to the end of the base stack starting at the procedure index.
        /// </summary>
        public IVariableStack<Expression> MakeStack(
            IVariableStack<Expression> Base, 
            int ProcedureIndex, 
            int NextFreeIndex)
        {
            return new _Stack()
            {
                Map = this,
                Base = Base,
                ProcedureIndex = ProcedureIndex
            };
        }

        private class _Stack : IVariableStack<Expression>
        {

            public ProcedureMap Map;
            public IVariableStack<Expression> Base;
            public int ProcedureIndex;
            public int NextFreeIndex;

            int IVariableStack<Expression>.NextFreeIndex
            {
                get
                {
                    return this.NextFreeIndex;
                }
            }

            public IVariableStack<Expression> Append(Expression[] Values)
            {
                throw new NotImplementedException();
            }

            public IVariableStack<Expression> Cut(int To)
            {
                throw new NotImplementedException();
            }

            public bool Lookup(int Index, ref Expression Value)
            {
                if (Index < this.ProcedureIndex)
                {
                    return this.Base.Lookup(Index, ref Value);
                }
                else
                {
                    return this.Map.Compute(this.ProcedureIndex, Index, ref Value);
                }
            }
        }

        /// <summary>
        /// Creates a procedure map that transforms the previous by setting the value of a single
        /// variable.
        /// </summary>
        public static ProcedureMap CreateSetMap(ProcedureMap Previous, int Index, Expression Value)
        {
            return new _SetMap()
            {
                _Previous = Previous,
                Index = Index,
                Value = Value
            };
        }

        private class _SetMap : ProcedureMap
        {

            protected override bool Compute(
                int ProcedureIndex,
                int Index,
                ref Expression Value)
            {
                if (Index >= ProcedureIndex)
                {
                    Value = this.Value.Substitute(this.MakeMap(ProcedureIndex));
                    return true;
                }
                return false;
            }

            public int Index;
            public Expression Value;
        }

        /// <summary>
        /// The map for the previous state of the variables.
        /// </summary>
        public ProcedureMap Previous
        {
            get
            {
                return this._Previous;
            }
        }

        private ProcedureMap _Previous;
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