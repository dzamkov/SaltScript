using System;
using System.Collections.Generic;

namespace SaltScript
{
    /// <summary>
    /// An expression represented by a procedure.
    /// </summary>
    public class ProcedureExpression : Expression
    {
        public ProcedureExpression(Statement Statement)
        {
            this._Statement = Statement;
        }

        public ProcedureExpression(Statement[] Statements)
        {
            this._Statement = new CompoundStatement(Statements);
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            Value[] toadd = new Value[this._InitialValues.Length];
            for (int t = 0; t < this._InitialValues.Length; t++)
            {
                toadd[t] = this._InitialValues[t];
            }
            return this._Statement.Call(Stack.Append(toadd));
        }

        private Value[] _InitialValues;
        private Statement _Statement;
    }

    /// <summary>
    /// A single instruction within a procedure.
    /// </summary>
    public abstract class Statement
    {
        /// <summary>
        /// Calls (runs) the statement with the specified mutable stack. Returns a value if this statement returns.
        /// </summary>
        public abstract Value Call(VariableStack<Value> Stack);

        /// <summary>
        /// Determines the type of the statement (return type) if there is any indication to it in the statement. Throws an exception
        /// if there are conflicting return types in the statement.
        /// </summary>
        public abstract bool GetType(Expression Expression, out Type Type);
    }

    /// <summary>
    /// A statement formed from multiple substatements.
    /// </summary>
    public class CompoundStatement : Statement
    {
        public CompoundStatement(Statement[] Substatements)
        {
            this._Substatements = Substatements;
        }

        public override Value Call(VariableStack<Value> Stack)
        {
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

        public override bool GetType(Expression Expression, out Type Type)
        {
            Type = null;
            bool hastype = false;
            for (int t = 0; t < this._Substatements.Length; t++)
            {
                Type ntype;
                if (this._Substatements[t].GetType(Expression, out ntype))
                {
                    if (hastype)
                    {
                        throw new MultipleTypeException(Expression);
                    }
                    else
                    {
                        Type = ntype;
                        hastype = true;
                    }
                }
            }
            return hastype;
        }

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
        }

        public override bool GetType(Expression Expression, out Type Type)
        {
            Type = null;
            return false;
        }

        private VariableIndex _Variable;
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