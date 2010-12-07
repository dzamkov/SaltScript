using System;
using System.Collections.Generic;

namespace SaltScript
{

    /// <summary>
    /// A value that can be invoked/called/executed.
    /// </summary>
    public abstract class FunctionValue : Value
    {
        /// <summary>
        /// Calls the function with the specified arguments. Type checking must be performed before calling. Arguments may not be changed
        /// after this call. An argument of null can be supplied if the function type does not require an argument.
        /// </summary>
        public abstract Value Call(Value Argument);

        /// <summary>
        /// If this has the type &lt;x&gt;x, creates a value of type x, 
        /// by calling the function with itself. the current function should not unconditionally require (invoke/use) its first
        /// parameter as this may lead to a stack overflow or circular dependency (either while calling this fix function, or at runtime).
        /// </summary>
        /// <remarks>If you still don't understand http://en.wikipedia.org/wiki/Fixed_point_combinator </remarks>
        public virtual Value Fix
        {
            get
            {
                // This way only works for functions...
                return new FixFunction(this);
            }
        }

        /// <summary>
        /// Creates a constant function for the specified value.
        /// </summary>
        public static ConstantFunction Constant(Value Value)
        {
            return new ConstantFunction(Value);
        }

        /// <summary>
        /// Gets the identity function value. The identity function will always return what it is called with.
        /// </summary>
        public static readonly FunctionValue Identity = new _IdentityFunction();

        /// <summary>
        /// Creates a function value from a native .net function.
        /// </summary>
        public static FunctionValue Create(FunctionHandler Function)
        {
            return new _NativeFunction() { Function = Function };
        }

        private class _NativeFunction : FunctionValue
        {
            public override Value Call(Value Argument)
            {
                return this.Function(Argument);
            }
            public FunctionHandler Function;
        }

        private class _IdentityFunction : FunctionValue
        {
            public override Value Call(Value Argument)
            {
                return Argument;
            }
        }
    }

    /// <summary>
    /// The value produced while performing the "Fix" operation on a function that itself returns a function.
    /// </summary>
    public class FixFunction : FunctionValue
    {
        public FixFunction(FunctionValue Source)
        {
            this.FixValue = (FunctionValue)Source.Call(this);
        }

        public override Value Call(Value Argument)
        {
            return this.FixValue.Call(Argument);
        }

        public FunctionValue FixValue;
    }

    /// <summary>
    /// A function that always returns the same value.
    /// </summary>
    public class ConstantFunction : FunctionValue
    {
        public ConstantFunction(Value Value)
        {
            this.Value = Value;
        }

        public override Value Call(Value Argument)
        {
            return this.Value;
        }

        /// <summary>
        /// The value returned by this function.
        /// </summary>
        public Value Value;
    }

    /// <summary>
    /// A function that gets a return value by evaluating an expression.
    /// </summary>
    public class ExpressionFunction : FunctionValue
    {
        public ExpressionFunction(VariableStack<Value> BaseStack, Expression Expression)
        {
            this.BaseStack = BaseStack;
            this.Expression = Expression;
        }

        public override Value Call(Value Argument)
        {
            return this.Expression.Evaluate(this.BaseStack.Append(new Value[] { Argument }));
        }

        /// <summary>
        /// The base stack for variables with a functional depth lower than the functional depth of the expression in this function.
        /// </summary>
        public VariableStack<Value> BaseStack;

        /// <summary>
        /// The expression that is evaluated to get the result of the function.
        /// </summary>
        public Expression Expression;
    }

    /// <summary>
    /// The delegate type for .net functions that can act as SaltScript functions.
    /// </summary>
    public delegate Value FunctionHandler(Value Argument);
}