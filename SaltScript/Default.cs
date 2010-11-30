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
        /// A function that takes a type and returns the maybe (variant) associated with it.
        /// </summary>
        public static readonly FunctionValue MaybeTypeConstructor = _CreateMaybeTypeConstructor();

        private static FunctionValue _CreateMaybeTypeConstructor()
        {
            return FunctionValue.Create(delegate(Value[] Args)
            {
                List<VariantForm> forms = new List<VariantForm>();
                forms.Add(new VariantForm("nothing", new Type[0]));
                forms.Add(new VariantForm("just", new Type[] { Args[0] as Type }));
                return new VariantType(forms);
            });
        }

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
            public override string Display(Type Type)
            {
                return "int";
            }
        }

        private class _IntValue : Value
        {
            public override string Display(Type Type)
            {
                return this.Value.ToString();
            } 

            public int Value;
        }

        private class _Input : InterpreterInput
        {
            public _Input()
            {
                this.RootValues = new Dictionary<string, Datum>();
                this.RootValues.Add("type", new Datum(Type.UniversalType, Type.UniversalType));
                this.RootValues.Add("int", new Datum(Type.UniversalType, IntType));
                this.RootValues.Add("maybe", new Datum(
                    new FunctionType(new Type[] { Type.UniversalType }, Expression.Constant(Type.UniversalType, Type.UniversalType)),
                    MaybeTypeConstructor));
                this._AddBinaryFunction("+", IntType, IntType, IntType, x => MakeIntValue(GetIntValue(x[0]) + GetIntValue(x[1])));
                this._AddBinaryFunction("-", IntType, IntType, IntType, x => MakeIntValue(GetIntValue(x[0]) - GetIntValue(x[1])));
                this._AddBinaryFunction("*", IntType, IntType, IntType, x => MakeIntValue(GetIntValue(x[0]) * GetIntValue(x[1])));
            }

            private void _AddBinaryFunction(string Name, Type TypeA, Type TypeB, Type ReturnType, FunctionHandler Handler)
            {
                this.RootValues.Add(Name, new Datum(
                    new FunctionType(new Type[] { TypeA, TypeB }, Expression.Constant(Type.UniversalType, ReturnType)),
                    FunctionValue.Create(Handler)));
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