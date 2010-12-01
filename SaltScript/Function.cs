using System;
using System.Collections.Generic;

namespace SaltScript
{
    /// <summary>
    /// The type for a function.
    /// </summary>
    public class FunctionType : Type
    {
        public FunctionType(Type Argument, FunctionValue ReturnType)
        {
            this.Argument = Argument;
            this.ReturnType = ReturnType;
        }

        public override string Display(Type Type)
        {
            // not much we can do here...
            return "<functiontype>";
        }

        /// <summary>
        /// Gets the return type for this function, given the current stack and an expression giving the arguments.
        /// </summary>
        public Type GetReturnType(VariableStack<Expression> Stack, Expression Argument)
        {
            return this.ReturnType.SubstituteCall(Argument.Substitute(Stack)).AsType;
        }

        /// <summary>
        /// The argument to the function. Null or an empty tuple type can be used to indicate no arguments. A tuple type can be used
        /// to indicate more than one argument.
        /// </summary>
        public Type Argument;

        /// <summary>
        /// A function that when called with the value of the argument, will find the return type of this function.
        /// </summary>
        public FunctionValue ReturnType;
    }

    /// <summary>
    /// A value that can be invoked/called/executed.
    /// </summary>
    public abstract class FunctionValue : Value
    {
        public override string Display(Type Type)
        {
            return "<function>";
        }

        /// <summary>
        /// Calls the function with the specified arguments. Type checking must be performed before calling. Arguments may not be changed
        /// after this call. An argument of null can be supplied if the function type does not require an argument.
        /// </summary>
        public abstract Value Call(Value Argument);

        /// <summary>
        /// Calls the function by providing an expression that yields the argument when evaluated. This can be used to avoid computation when it
        /// isn't needed.
        /// </summary>
        public virtual Value SubstituteCall(Expression Argument)
        {
            return this.Call(Argument.Value);
        }

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

            public override Value SubstituteCall(Expression Argument)
            {
                return Argument.Value;
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

        public override Value SubstituteCall(Expression Argument)
        {
            return this.FixValue.SubstituteCall(Argument);
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

        public override Value SubstituteCall(Expression Argument)
        {
            return this.Value;
        }

        /// <summary>
        /// The value returned by this function.
        /// </summary>
        public Value Value;
    }

    /// <summary>
    /// The delegate type for .net functions that can act as SaltScript functions.
    /// </summary>
    public delegate Value FunctionHandler(Value Argument);
}