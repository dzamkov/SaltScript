using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// Represents a value.
    /// </summary>
    public class Value
    {

    }

    /// <summary>
    /// Represents a type for the script.
    /// </summary>
    public class Type : Value
    {
        /// <summary>
        /// The friendly (ish) name for this type.
        /// </summary>
        public virtual string Name
        {
            get
            {
                return this.ToString();
            }
        }

        /// <summary>
        /// Gets a friendly (ish) string form of a value of this type.
        /// </summary>
        public virtual string Display(Value Value)
        {
            return Value.ToString();
        }

        /// <summary>
        /// Converts a datum to a value of this type.
        /// </summary>
        public virtual bool Convert(Datum Datum, out Value Value)
        {
            if (Datum.Type == this)
            {
                Value = Datum.Value;
                return true;
            }
            else
            {
                Value = null;
                return false;
            }
        }

        /// <summary>
        /// All types are values with this type, including this type.
        /// </summary>
        public static readonly Type UniversalType = new _TypeType();

        private class _TypeType : Type
        {
            public override string Name
            {
                get
                {
                    return "type";
                }
            }

            public override string Display(Value Value)
            {
                return (Value as Type).Name;
            }

            public override bool Convert(Datum Datum, out Value Value)
            {
                if (Datum.Value is Type)
                {
                    Value = Datum.Value;
                    return true;
                }
                else
                {
                    Value = null;
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// The type for a function.
    /// </summary>
    public class FunctionType : Type
    {
        public FunctionType(Type[] Arguments, Expression ReturnType)
        {
            this.Arguments = Arguments;
            this.ReturnType = ReturnType;
        }

        public override string Display(Value Value)
        {
            return "<function>";
        }

        /// <summary>
        /// Gets the return type for this function, given the current stack and the expressions defining the arguments.
        /// </summary>
        public Type GetReturnType(VariableStack<Expression> Stack, Expression[] Arguments)
        {
            ValueExpression ve = this.ReturnType as ValueExpression;
            if (ve != null)
            {
                // Most common way
                return ve.Value as Type;
            }
            else
            {
                // Naive always-works way
                VariableStack<Expression> nstack = Stack.Append(Arguments);
                return this.ReturnType.Substitute(nstack).Value as Type;
            }
        }

        /// <summary>
        /// An array of indicating the types of the arguments for the function.
        /// </summary>
        public Type[] Arguments;

        /// <summary>
        /// An expression that when evaluated (with the argument values at the topmost parts of the stack), will get the return type.
        /// </summary>
        public Expression ReturnType;
    }

    /// <summary>
    /// A value that can be invoked/called/executed.
    /// </summary>
    public abstract class FunctionValue : Value
    {
        /// <summary>
        /// Calls the function with the specified arguments. Type checking must be performed before calling.
        /// </summary>
        public abstract Value Call(Value[] Arguments);

        /// <summary>
        /// Creates a function value from a native .net function.
        /// </summary>
        public static FunctionValue Create(FunctionHandler Function)
        {
            return new _NativeFunctionValue() { Function = Function };
        }

        private class _NativeFunctionValue : FunctionValue
        {
            public override Value Call(Value[] Arguments)
            {
                return this.Function(Arguments);
            }
            public FunctionHandler Function;
        }
    }

    /// <summary>
    /// The delegate type for .net functions that can act as SaltScript functions.
    /// </summary>
    public delegate Value FunctionHandler(Value[] Arguments);

    /// <summary>
    /// Information about a datum (type/value pair).
    /// </summary>
    public struct Datum
    {
        public Datum(Type Type, Value Value)
        {
            this.Type = Type;
            this.Value = Value;
        }

        /// <summary>
        /// The type of the variable.
        /// </summary>
        public Type Type;

        /// <summary>
        /// The current value of the variable.
        /// </summary>
        public Value Value;
    }

    /// <summary>
    /// Relates variable names to their position on the stack.
    /// </summary>
    public class Scope
    {

        /// <summary>
        /// Gets the functional depth and index of the specified variable, if it is found.
        /// </summary>
        public bool LookupVariable(string Name, out int FunctionalDepth, out int Index)
        {
            if (this.Variables.TryGetValue(Name, out Index))
            {
                FunctionalDepth = this.FunctionalDepth;
                return true;
            }
            else
            {
                if (this.Parent != null)
                {
                    return this.Parent.LookupVariable(Name, out FunctionalDepth, out Index);
                }
                else
                {
                    FunctionalDepth = 0;
                    return false;
                }
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
        public VariableStack()
        {

        }

        public VariableStack(TValue[] Values)
        {
            this.Values = Values;
        }

        /// <summary>
        /// Appends values to the stack and returns the new stack.
        /// </summary>
        public VariableStack<TValue> Append(TValue[] Values)
        {
            return new VariableStack<TValue>()
            {
                FunctionalDepth = this.FunctionalDepth,
                Lower =  this,
                StartIndex = this.StartIndex + this.Values.Length,
                Values = Values
            };
        }

        /// <summary>
        /// Gets the value for the variable with the specified start index and functional depth.
        /// </summary>
        public TValue Lookup(int FunctionalDepth, int Index)
        {
            VariableStack<TValue> curstack = this;
            while (curstack.FunctionalDepth > FunctionalDepth || curstack.StartIndex > Index)
            {
                curstack = curstack.Lower;
            }
            return curstack.Values[Index - curstack.StartIndex];
        }

        /// <summary>
        /// Gets the lower levels of the stack.
        /// </summary>
        public VariableStack<TValue> Lower;

        /// <summary>
        /// The functional depth this part of the stack is on.
        /// </summary>
        public int FunctionalDepth;

        /// <summary>
        /// The index of the first value recorded in this part of the stack.
        /// </summary>
        public int StartIndex;

        /// <summary>
        /// Values for variables on this portion of the stack.
        /// </summary>
        public TValue[] Values;
    }

    /// <summary>
    /// Represents an expression, which can be evaluated if all scope variables are known.
    /// </summary>
    public abstract class Expression
    {
        /// <summary>
        /// Evaluates the expression with the given immediate value stack.
        /// </summary>
        public abstract Value Evaluate(VariableStack<Value> Stack);

        /// <summary>
        /// Gets the type of this expression, given the expressions that define the variables in this expression.
        /// </summary>
        public abstract Type GetType(VariableStack<Expression> Stack);

        /// <summary>
        /// Substitutes every variable in the expression with its corresponding expression on the stack.
        /// </summary>
        public abstract Expression Substitute(VariableStack<Expression> Stack);

        /// <summary>
        /// Creates a type-safe version of the expression by using conversions where necessary. An exception will
        /// be thrown if this is not possible.
        /// </summary>
        public virtual Expression TypeCheck(VariableStack<Expression> Stack)
        {
            return this;
        }

        /// <summary>
        /// Gets the value of the expression if there are no variables dependant on an immediate stack.
        /// </summary>
        public Value Value
        {
            get
            {
                return this.Evaluate(null);
            }
        }

        /// <summary>
        /// Gets the type of the expression if one can be found without given the definitions of variables dependant on the
        /// immediate stack.
        /// </summary>
        public Type Type
        {
            get
            {
                return this.GetType(null);
            }
        }

        /// <summary>
        /// Creates an expression that always evaluates to the same value.
        /// </summary>
        public static ValueExpression Constant(Type Type, Value Value)
        {
            return new ValueExpression(Type, Value);
        }
    }

    /// <summary>
    /// An expression that represents an immutable value of a certain type.
    /// </summary>
    public class ValueExpression : Expression
    {
        public ValueExpression(Type Type, Value Value)
        {
            this.Datum = new Datum(Type, Value);
        }

        public ValueExpression(Datum Datum)
        {
            this.Datum = Datum;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return this.Datum.Value;
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            return this.Datum.Type;
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return this;
        }

        /// <summary>
        /// The datum that is always produced by this expression.
        /// </summary>
        public Datum Datum;
    }

    /// <summary>
    /// An expression that represents a variable in the active scope.
    /// </summary>
    public class VariableExpression : Expression
    {
        public VariableExpression(int StackPosition, int FunctionalDepth)
        {
            this.StackIndex = StackPosition;
            this.FunctionalDepth = FunctionalDepth;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return Stack.Lookup(this.FunctionalDepth, this.StackIndex);
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            return Stack.Lookup(this.FunctionalDepth, this.StackIndex).Type;
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return Stack.Lookup(this.FunctionalDepth, this.StackIndex);
        }

        /// <summary>
        /// Relative position of the variable on the stack of the function that defines it.
        /// </summary>
        public int StackIndex;

        /// <summary>
        /// The amount of functional scopes in this variable is defined. 0 Indicates that this variable is used in the same function
        /// it is defined in. 1 Indicates that the variable is used in a closure of the function that defines it. And so on...
        /// </summary>
        public int FunctionalDepth;
    }

    /// <summary>
    /// An expression that represents a function call.
    /// </summary>
    public class FunctionCallExpression : Expression
    {
        public FunctionCallExpression(Expression Function, Expression[] Arguments)
        {
            this.Function = Function;
            this.Arguments = Arguments;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            Value func = this.Function.Evaluate(Stack);
            Value[] args = new Value[this.Arguments.Length];
            for (int t = 0; t < args.Length; t++)
            {
                args[t] = this.Arguments[t].Evaluate(Stack);
            }
            return (func as FunctionValue).Call(args);
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            FunctionType functype = this.Function.GetType(Stack) as FunctionType;
            return functype.GetReturnType(Stack, this.Arguments);
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            Expression[] nargs = new Expression[this.Arguments.Length];
            for(int t = 0; t < nargs.Length; t++)
            {
                nargs[t] = this.Arguments[t].Substitute(Stack);
            }
            return new FunctionCallExpression(this.Function.Substitute(Stack), nargs);
        }

        /// <summary>
        /// The function that is called.
        /// </summary>
        public Expression Function;

        /// <summary>
        /// Expressions for the arguments of the call.
        /// </summary>
        public Expression[] Arguments;
    }

    /// <summary>
    /// Input to the interpreter, containing information about the root scope and how to process certain expressions.
    /// </summary>
    public abstract class InterpreterInput
    {
        /// <summary>
        /// Gets the type integer literals are assigned with.
        /// </summary>
        public abstract Type IntegerLiteralType { get; }

        /// <summary>
        /// Gets the value for the specified integer literal.
        /// </summary>
        public abstract Value GetIntegerLiteral(long Value);

        /// <summary>
        /// The values in the root scope.
        /// </summary>
        public Dictionary<string, Datum> RootValues;
    }

    /// <summary>
    /// Interpreter functions.
    /// </summary>
    public static class Interpret
    {
        /// <summary>
        /// Evaluates a parsed expression using the default interpreter input.
        /// </summary>
        public static Datum Evaluate(Parser.Expression Expression)
        {
            return Evaluate(Expression, Default.Input);
        }

        /// <summary>
        /// Evaluates a parsed expression using the specified interpreter input.
        /// </summary>
        public static Datum Evaluate(Parser.Expression Expression, InterpreterInput Input)
        {
            // Assign all input variables to a position on the stack of the root scope. (Seperates names and values)
            int rootvars = Input.RootValues.Count;
            Dictionary<string, int> vars = new Dictionary<string, int>(rootvars);
            Value[] data = new Value[rootvars];
            Expression[] exps = new Expression[rootvars];
            int i = 0;
            foreach (var kvp in Input.RootValues)
            {
                data[i] = kvp.Value.Value;
                exps[i] = new ValueExpression(kvp.Value);
                vars[kvp.Key] = i;
                i++;
            }
            var datastack = new VariableStack<Value>(data);
            var expstack = new VariableStack<Expression>(exps);

            // Prepare
            Expression exp = Prepare(Expression, new Scope() { FunctionalDepth = 0, Variables = vars }, Input);
            Type exptype = exp.GetType(expstack);

            // Evaluate
            return new Datum(exptype, exp.TypeCheck(expstack).Evaluate(datastack));
        }

        /// <summary>
        /// Prepares a parsed expression for use.
        /// </summary>
        public static Expression Prepare(Parser.Expression Expression, Scope Scope, InterpreterInput Input)
        {
            // Function call
            Parser.FunctionCallExpression fce = Expression as Parser.FunctionCallExpression;
            if (fce != null)
            {
                Expression[] args = new Expression[fce.Arguments.Count];
                for (int t = 0; t < args.Length; t++)
                {
                    args[t] = Prepare(fce.Arguments[t], Scope, Input);
                }
                Expression func = Prepare(fce.Function, Scope, Input);

                return new FunctionCallExpression(func, args);
            }

            // Variable
            Parser.VariableExpression ve = Expression as Parser.VariableExpression;
            if (ve != null)
            {
                return PrepareVariable(ve.Name, Scope);
            }

            // Integer liteal
            Parser.IntegerLiteralExpression ile = Expression as Parser.IntegerLiteralExpression;
            if (ile != null)
            {
                return new ValueExpression(Input.IntegerLiteralType, Input.GetIntegerLiteral(ile.Value));
            }

            return null;
        }

        /// <summary>
        /// Prepares a variable expression based on its name and the scope it's used in. Returns null if the variable is not found.
        /// </summary>
        public static VariableExpression PrepareVariable(string Name, Scope Scope)
        {
            int depth;
            int index;
            if (Scope.LookupVariable(Name, out depth, out index))
            {
                return new VariableExpression(index, depth);
            }
            else
            {
                return null;
            }
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
}