using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// Relates variable names to their position on the stack.
    /// </summary>
    public class Scope
    {
        public Scope()
        {

        }

        /// <summary>
        /// Gets the index of the specified variable, if it is found.
        /// </summary>
        public bool LookupVariable(string Name, out int Index)
        {
            if (this.Variables.TryGetValue(Name, out Index))
            {
                return true;
            }
            else
            {
                if (this.Parent != null)
                {
                    return this.Parent.LookupVariable(Name, out Index);
                }
                else
                {
                    Index = -1;
                    return false;
                }
            }
        }

        
        /// <summary>
        /// Parent scope for this scope. Variables in the parent scope can be used in a more immediate scope.
        /// </summary>
        public Scope Parent;

        /// <summary>
        /// Table of variable names and their stack indices.
        /// </summary>
        public Dictionary<string, int> Variables;

        /// <summary>
        /// The next available unassigned index.
        /// </summary>
        public int NextFreeIndex;
    }

    /// <summary>
    /// Input to the interpreter, containing information about the root scope and how to process certain expressions.
    /// </summary>
    public abstract class ProgramInput
    {
        public ProgramInput()
        {
            this._RootVariables = new List<_RootVariable>();
        }

        /// <summary>
        /// Gets the next free variable index after the input.
        /// </summary>
        public int NextFreeIndex
        {
            get
            {
                return this._RootVariables.Count;
            }
        }

        /// <summary>
        /// Gets the type integer literals are assigned with.
        /// </summary>
        public abstract Expression IntegerLiteralType { get; }
        
        /// <summary>
        /// Gets the universal type (type assigned to all other types).
        /// </summary>
        public abstract Expression UniversalType { get; }

        /// <summary>
        /// Gets the value for the specified integer literal.
        /// </summary>
        public abstract Value GetIntegerLiteral(long Value);

        /// <summary>
        /// Adds a variable to the root scope, returning an expression that can be used to reference it.
        /// </summary>
        protected Expression AddRootVariable(string Name, Expression Type, Value Value)
        {
            int i = this._RootVariables.Count;
            this._RootVariables.Add(new _RootVariable()
            {
                Name = Name,
                Type = Type,
                Value = Value
            });
            return Expression.Variable(i);
        }

        /// <summary>
        /// Adds the universal type to the root scope. This variable is unique because it is its own type.
        /// </summary>
        protected Expression AddUniversalType(string Name, Value Value)
        {
            int i = this._RootVariables.Count;
            Expression exp = Expression.Variable(i);
            this._RootVariables.Add(new _RootVariable()
            {
                Name = Name,
                Type = exp,
                Value = Value
            });
            return exp;
        }

        /// <summary>
        /// Creates stacks and information about the root scope of this input.
        /// </summary>
        public void PrepareRootScope(out Scope Scope, out IMutableVariableStack<Value> Stack, out IVariableStack<Expression> TypeStack)
        {
            Dictionary<string, int> varmap = new Dictionary<string, int>();
            Value[] vals = new Value[this._RootVariables.Count];
            Expression[] types = new Expression[vals.Length];
            for (int t = 0; t < vals.Length; t++)
            {
                vals[t] = this._RootVariables[t].Value;
                types[t] = this._RootVariables[t].Type;
                varmap.Add(this._RootVariables[t].Name, t);
            }
            Scope = new Scope() { Variables = varmap, NextFreeIndex = vals.Length };
            Stack = new SpaghettiStack<Value>(vals);
            TypeStack = new SpaghettiStack<Expression>(types);
        }

        private struct _RootVariable
        {
            public string Name;
            public Expression Type;
            public Value Value;
        };

        private List<_RootVariable> _RootVariables;
    }

    /// <summary>
    /// Interpreter functions.
    /// </summary>
    public static class Interpret
    {
        /// <summary>
        /// Evaluates a parsed expression using the default interpreter input.
        /// </summary>
        public static Datum Evaluate(Parser.Expression ParsedExpression)
        {
            return Evaluate(ParsedExpression, Default.Input);
        }

        /// <summary>
        /// Evaluates a parsed expression using the specified interpreter input.
        /// </summary>
        public static Datum Evaluate(Parser.Expression ParsedExpression, ProgramInput Input)
        {
            // Assign all input variables to a position on the stack of the root scope. (Seperates names and values)
            IVariableStack<Expression> typestack;
            IMutableVariableStack<Value> datastack;
            Scope scope;
            Input.PrepareRootScope(out scope, out datastack, out typestack);

            // Prepare
            Expression exp = Expression.Prepare(ParsedExpression, scope, Input);
            Expression exptype;
            exp.TypeCheck(typestack, SpaghettiStack<Expression>.Empty(typestack.NextFreeIndex), out exp, out exptype);

            // Evaluate
            return new Datum(exp.Evaluate(datastack), exptype);
        }

        /// <summary>
        /// Very simply, evaluates an expression.
        /// </summary>
        public static Datum Evaluate(string Expression)
        {
            int lc;
            Parser.Expression exp;
            if (Parser.AcceptExpression(Expression, 0, out exp, out lc))
            {
                return Evaluate(exp);
            }
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// An exception indicating a type mismatch while type checking.
    /// </summary>
    public class TypeCheckException : Exception
    {
        public TypeCheckException(Expression Expression)
        {
            this.Expression = Expression;
        }

        /// <summary>
        /// The expression where the type mismatch occured.
        /// </summary>
        public Expression Expression;
    }

    /// <summary>
    /// An exception indicating that a type is not suitable for an accessor.
    /// </summary>
    public class AccessorFailException : TypeCheckException
    {
        public AccessorFailException(Expression Expression)
            : base(Expression)
        {
            this.Expression = Expression;
        }
    }

    /// <summary>
    /// An exception indicating that an object used as a function in a function call expression cannot be called.
    /// </summary>
    public class NotCallableException : TypeCheckException
    {
        public NotCallableException(Expression Expression)
            : base(Expression)
        {
            this.Expression = Expression;
        }
    }
}