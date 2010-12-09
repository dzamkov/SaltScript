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

        public VariableStack(int StartIndex, TValue[] Values)
        {
            this._StartIndex = StartIndex;
            this._Values = Values;
        }

        public VariableStack(VariableStack<TValue> Lower, int StartIndex, TValue[] Values)
        {
            this._Lower = Lower;
            this._StartIndex = StartIndex;
            this._Values = Values;
            this._Mutable = this._Lower._Mutable;
        }

        /// <summary>
        /// Gets or sets if this stack is marked mutable, which indicates if it can be changed at a later time.
        /// </summary>
        public bool Mutable
        {
            get
            {
                return this._Mutable;
            }
        }

        /// <summary>
        /// Marks the top portion of the stack as mutable.
        /// </summary>
        public void MarkMutable()
        {
            this._Mutable = true;
        }

        /// <summary>
        /// Marks every variable after the one specified as mutable.
        /// </summary>
        public void MarkMutable(int Index)
        {
            VariableStack<TValue> start;
            this._GetIndex(Index, out start, out Index);
            start.MarkMutable();
        }

        /// <summary>
        /// Gets a frozen copy of this stack that will not be modified by an outside influence.
        /// </summary>
        public VariableStack<TValue> Freeze
        {
            get
            {
                if (this._Mutable)
                {
                    TValue[] nvals = new TValue[this._Values.Length];
                    for(int t = 0; t < nvals.Length; t++)
                    {
                        nvals[t] = this._Values[t];
                    }
                    return new VariableStack<TValue>()
                    {
                        _Lower = this._Lower == null ? null : this._Lower.Freeze,
                        _StartIndex = this._StartIndex,
                        _Values = nvals
                    };
                }
                return this;
            }
        }

        /// <summary>
        /// Creates a stack that is empty up until the specified index, after which it is undefined.
        /// </summary>
        public static VariableStack<TValue> Empty(int Index)
        {
            return new VariableStack<TValue>(Index, new TValue[0]);
        }

        /// <summary>
        /// Gets the index of the variable after the last in the stack.
        /// </summary>
        public int NextIndex
        {
            get
            {
                return this._StartIndex + this._Values.Length;
            }
        }

        /// <summary>
        /// Appends values to the stack and returns the new stack.
        /// </summary>
        public VariableStack<TValue> Append(TValue[] Values)
        {
            return new VariableStack<TValue>(this, this._StartIndex + this._Values.Length, Values);
        }

        /// <summary>
        /// Appends a single value to the stack.
        /// </summary>
        public VariableStack<TValue> Append(TValue Value)
        {
            return new VariableStack<TValue>(this, this._StartIndex + this._Values.Length, new TValue[] { Value });
        }

        /// <summary>
        /// Appends the specified amount of variables on the stack.
        /// </summary>
        public VariableStack<TValue> Append(int Amount)
        {
            return new VariableStack<TValue>(this, this._StartIndex + this._Values.Length, new TValue[Amount]);
        }

        /// <summary>
        /// Cuts off the top of the stack starting at the specified index.
        /// </summary>
        public VariableStack<TValue> Cut(int To)
        {
            if (this.NextIndex > To)
            {
                return new VariableStack<TValue>(this, To, new TValue[0]);
            }
            return this;
        }

        /// <summary>
        /// Gets the value for the variable with the specified start index and functional depth.
        /// </summary>
        public TValue Lookup(int Index)
        {
            VariableStack<TValue> stack;
            int valindex;
            this._GetIndex(Index, out stack, out valindex);
            return stack._Values[valindex];
        }

        /// <summary>
        /// Tries getting the variable with the specified index, returning false if not found.
        /// </summary>
        public bool Lookup(int Index, out TValue Value)
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
        public void Modify(int Index, TValue Value)
        {
            VariableStack<TValue> stack;
            int valindex;
            this._GetIndex(Index, out stack, out valindex);
            stack._Values[valindex] = Value;
        }

        private bool _GetIndex(int Index, out VariableStack<TValue> Stack, out int ValIndex)
        {
            Stack = this;
            while (Stack._StartIndex > Index)
            {
                Stack = Stack._Lower;
                if (Stack == null)
                {
                    ValIndex = 0;
                    return false;
                }
            }
            ValIndex = Index - Stack._StartIndex;
            if (ValIndex < Stack._Values.Length)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets if this part of the stack can be changed at a later time.
        /// </summary>
        private bool _Mutable;

        /// <summary>
        /// Gets the lower levels of the stack.
        /// </summary>
        private VariableStack<TValue> _Lower;

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
            Scope = new Scope() { Variables = varmap, NextFreeIndex = vals.Length };
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
            exp.TypeCheck(typestack, VariableStack<Expression>.Empty(typestack.NextIndex), out exp, out exptype);

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