using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// Value of an integer of the default type.
    /// </summary>
    public class IntValue : Value
    {

    }

    /// <summary>
    /// Functions related to the default interpret input.
    /// </summary>
    public static class Default
    {
        /*/// <summary>
        /// Integer type.
        /// </summary>
        public static readonly Type IntType = new _IntType();

        /// <summary>
        /// A function that takes a type and returns the maybe (variant) associated with it.
        /// </summary>
        public static readonly FunctionValue MaybeTypeConstructor = _CreateMaybeTypeConstructor();

        /// <summary>
        /// The value for nothing of a maybe type.
        /// </summary>
        public static readonly VariantValue Nothing = new VariantValue(0, null);

        /// <summary>
        /// Creates a value for a maybe type.
        /// </summary>
        public static VariantValue Just(Value Value)
        {
            return new VariantValue(1, Value);
        }

        private static FunctionValue _CreateMaybeTypeConstructor()
        {
            return FunctionValue.Create(delegate(Value Arg)
            {
                List<VariantForm> forms = new List<VariantForm>();
                forms.Add(new VariantForm("nothing", null));
                forms.Add(new VariantForm("just", Arg.AsType));
                return new VariantType(forms);
            });
        }

        /// <summary>
        /// Given a value of type maybe(x), gets if the value is "nothing".
        /// </summary>
        public static bool IsNothing(VariantValue MaybeValue)
        {
            return MaybeValue.FormIndex == 0;
        }

        /// <summary>
        /// Given a value of type maybe(x), gets if the value is of the form of "just y". If so, returns y.
        /// </summary>
        public static bool HasValue(VariantValue MaybeValue, out Value Value)
        {
            if (MaybeValue.FormIndex == 1)
            {
                Value = MaybeValue.Data;
                return true;
            }
            else
            {
                Value = null;
                return false;
            }
        }*/

        /// <summary>
        /// Default interpret input.
        /// </summary>
        public static readonly ProgramInput Input = new _Input();

        /// <summary>
        /// Makes a value for an integer.
        /// </summary>
        public static Value MakeIntValue(int Value)
        {
            return new _IntValue() { Value = Value };
        }

        /// <summary>
        /// Gets an integer from a value of the integer type.
        /// </summary>
        public static int GetIntValue(Value Value)
        {
            return ((_IntValue)Value).Value;
        }

        private class _IntValue : Value
        {
            public override string ToString()
            {
                return this.Value.ToString();
            }

            public int Value;
        }

        private class _Input : ProgramInput
        {
            public _Input()
            {
                this.AddRootVariable("type", Expression.UniversalType, null);
                this.IntType = this.AddRootVariable("int", Expression.UniversalType, null);
                this.AddBinaryFunction("+", this.IntType, this.IntType, this.IntType, (x, y) => MakeIntValue(GetIntValue(x) + GetIntValue(y)));
                this.AddBinaryFunction("-", this.IntType, this.IntType, this.IntType, (x, y) => MakeIntValue(GetIntValue(x) - GetIntValue(y)));
                this.AddBinaryFunction("*", this.IntType, this.IntType, this.IntType, (x, y) => MakeIntValue(GetIntValue(x) * GetIntValue(y)));
            }

            public Expression AddBinaryFunction(string Name, Expression TypeA, Expression TypeB, Expression ReturnType, BinaryFunction Handler)
            {
                return this.AddRootVariable(Name, Expression.SimpleFunctionType(Expression.Tuple(TypeA, TypeB), ReturnType),
                    new BinaryFunctionValue() { Function = Handler });
            }

            public class BinaryFunctionValue : FunctionValue
            {
                public override Value Call(Value Argument)
                {
                    TupleValue tv = (TupleValue)Argument;
                    return this.Function(tv.Values[0], tv.Values[1]);
                }

                public BinaryFunction Function;
            }

            public delegate Value BinaryFunction(Value A, Value B);

            public override Value GetIntegerLiteral(long Value)
            {
                return MakeIntValue((int)Value);
            }

            public override Expression IntegerLiteralType
            {
                get
                {
                    return this.IntType;
                }
            }

            private Expression IntType;
        }
    }
}