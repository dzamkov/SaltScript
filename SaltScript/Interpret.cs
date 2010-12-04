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
        /// Gets the functional depth and index of the specified variable, if it is found.
        /// </summary>
        public bool LookupVariable(string Name, out VariableIndex Index)
        {
            int i;
            if (this.Variables.TryGetValue(Name, out i))
            {
                Index = new VariableIndex(i, this.FunctionalDepth);
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
                    Index = new VariableIndex();
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the next unassigned variable index.
        /// </summary>
        public VariableIndex NextFreeIndex
        {
            get
            {
                int max = 0;
                foreach (int v in this.Variables.Values)
                {
                    max = max < v ? v : max;
                }
                return new VariableIndex(max + 1, this.FunctionalDepth);
            }
        }

        /// <summary>
        /// Parent scope for this scope. Variables in the parent scope can be used in a more immediate scope.
        /// </summary>
        public Scope Parent;

        /// <summary>
        /// Table of variable names and their relative stack indices.
        /// </summary>
        public Dictionary<string, int> Variables;

        /// <summary>
        /// Functional depth of this scope.
        /// </summary>
        public int FunctionalDepth;
    }

    /// <summary>
    /// Maps numerically indexed variables to values.
    /// </summary>
    public class VariableStack<TValue>
    {
        private VariableStack()
        {

        }

        public VariableStack(TValue[] Values)
        {
            this._Values = Values;
        }

        public VariableStack(int FunctionalDepth, TValue[] Values)
        {
            this._FunctionalDepth = FunctionalDepth;
            this._Values = Values;
        }

        /// <summary>
        /// Gets the index of the variable after the last in the stack.
        /// </summary>
        public VariableIndex LastIndex
        {
            get
            {
                return new VariableIndex(this._StartIndex + this._Values.Length, this._FunctionalDepth);
            }
        }

        /// <summary>
        /// Appends values to the stack and returns the new stack.
        /// </summary>
        public VariableStack<TValue> Append(TValue[] Values)
        {
            return new VariableStack<TValue>()
            {
                _FunctionalDepth = this._FunctionalDepth,
                _Lower =  this,
                _StartIndex = this._StartIndex + this._Values.Length,
                _Values = Values
            };
        }

        /// <summary>
        /// Appends the specified amount of variables on the stack.
        /// </summary>
        public VariableStack<TValue> Append(int Amount)
        {
            return new VariableStack<TValue>()
            {
                _FunctionalDepth = this._FunctionalDepth,
                _Lower = this,
                _StartIndex = this._StartIndex + this._Values.Length,
                _Values = new TValue[Amount]
            };
        }

        /// <summary>
        /// Appends values to the stack at a higher functional depth.
        /// </summary>
        public VariableStack<TValue> AppendHigherFunction(TValue[] Values)
        {
            return new VariableStack<TValue>()
            {
                _FunctionalDepth = this._FunctionalDepth + 1,
                _Lower = this,
                _StartIndex = 0,
                _Values = Values
            };
        }

        /// <summary>
        /// Gets the value for the variable with the specified start index and functional depth.
        /// </summary>
        public TValue Lookup(VariableIndex Index)
        {
            VariableStack<TValue> stack;
            int valindex;
            this._GetIndex(Index, out stack, out valindex);
            return stack._Values[valindex];
        }

        /// <summary>
        /// Tries getting the variable with the specified index, returning false if not found.
        /// </summary>
        public bool Lookup(VariableIndex Index, out TValue Value)
        {
            VariableStack<TValue> stack;
            int valindex;
            if (this._GetIndex(Index, out stack, out valindex))
            {
                Value = stack._Values[valindex];
                return true;
            }
            else
            {
                Value = default(TValue);
                return false;
            }
        }

        /// <summary>
        /// Modifies a variable on the stack.
        /// </summary>
        public void Modify(VariableIndex Index, TValue Value)
        {
            VariableStack<TValue> stack;
            int valindex;
            this._GetIndex(Index, out stack, out valindex);
            stack._Values[valindex] = Value;
        }

        private bool _GetIndex(VariableIndex Index, out VariableStack<TValue> Stack, out int ValIndex)
        {
            Stack = this;
            while (Stack._FunctionalDepth > Index.FunctionalDepth || Stack._StartIndex > Index.StackIndex)
            {
                Stack = Stack._Lower;
                if (Stack == null)
                {
                    ValIndex = 0;
                    return false;
                }
            }
            ValIndex = Index.StackIndex - Stack._StartIndex;
            return true;
        }

        /// <summary>
        /// Gets the lower levels of the stack.
        /// </summary>
        private VariableStack<TValue> _Lower;

        /// <summary>
        /// The functional depth this part of the stack is on.
        /// </summary>
        private int _FunctionalDepth;

        /// <summary>
        /// The index of the first value recorded in this part of the stack.
        /// </summary>
        private int _StartIndex;

        /// <summary>
        /// Values for variables on this portion of the stack.
        /// </summary>
        private TValue[] _Values;
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
        /// Gets the type integer literals are assigned with.
        /// </summary>
        public abstract Expression IntegerLiteralType { get; }

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
            return Expression.Variable(new VariableIndex(i, 0));
        }

        /// <summary>
        /// Creates stacks and information about the root scope of this input.
        /// </summary>
        public void PrepareRootScope(out Scope Scope, out VariableStack<Value> Stack, out VariableStack<Expression> TypeStack)
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
            Scope = new Scope() { FunctionalDepth = 0, Variables = varmap };
            Stack = new VariableStack<Value>(vals);
            TypeStack = new VariableStack<Expression>(types);
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
            VariableStack<Expression> typestack;
            VariableStack<Value> datastack;
            Scope scope;
            Input.PrepareRootScope(out scope, out datastack, out typestack);

            // Prepare
            Expression exp = Expression.Prepare(ParsedExpression, scope, Input);
            Expression exptype;
            exp.TypeCheck(typestack, out exp, out exptype);

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