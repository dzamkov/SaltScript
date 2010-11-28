using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// Default integer type.
    /// </summary>
    public class IntType : Type
    {

    }

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
        /// <summary>
        /// Integer type.
        /// </summary>
        public static readonly Type IntType = new _IntType();

        /// <summary>
        /// Default interpret input.
        /// </summary>
        public static readonly InterpreterInput Input = new _Input();

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


        private class _IntType : Type
        {
            public override string Name
            {
                get
                {
                    return "int";
                }
            }

            public override string Display(Value Value)
            {
                return (Value as _IntValue).Value.ToString();
            }
        }

        private class _IntValue : Value
        {
            public int Value;
        }

        private class _Input : InterpreterInput
        {
            public _Input()
            {
                this.RootValues = new Dictionary<string, Datum>();
                this.RootValues.Add("type", new Datum(Type.UniversalType, Type.UniversalType));
                this.RootValues.Add("int", new Datum(Type.UniversalType, IntType));
                this.RootValues.Add("+", new Datum(
                    new FunctionType(new Type[2] { IntType, IntType }, Expression.Constant(Type.UniversalType, IntType)),
                    FunctionValue.Create(x => MakeIntValue(GetIntValue(x[0]) + GetIntValue(x[1])))));
            }

            public override Type IntegerLiteralType
            {
                get
                {
                    return IntType;   
                }
            }

            public override Value GetIntegerLiteral(long Value)
            {
                return MakeIntValue((int)Value);
            }
        }
    }
}