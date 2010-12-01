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
        }

        /// <summary>
        /// Default interpret input.
        /// </summary>
        public static readonly InterpreterInput Input = new _Input();

        /// <summary>
        /// The default conversion factory.
        /// </summary>
        public static readonly FunctionValue ConversionFactory = _CreateDefaultImplicitConversion();

        private static FunctionValue _CreateDefaultImplicitConversion()
        {
            return FunctionValue.Create(delegate(Value fixedconv)
            {
                FunctionValue ffixedconv = (FunctionValue)fixedconv;
                return FunctionValue.Create(delegate(Value types)
                {
                    Type from = types.Get(0).AsType;
                    Type to = types.Get(1).AsType;

                    // Types are the same? anwser is obvious
                    if (from == to)
                    {
                        return Just(FunctionValue.Identity);
                    }

                    // Types are both tuples, we can individually convert each part.
                    TupleType fromtt = from as TupleType;
                    TupleType tott = to as TupleType;
                    if (fromtt != null && tott != null)
                    {
                        if (fromtt.Types == null)
                        {
                            if (tott.Types == null)
                            {
                                return Just(FunctionValue.Identity);
                            }
                            return Nothing;
                        }

                        if (fromtt.Types.Length != tott.Types.Length)
                        {
                            return Nothing;
                        }

                        FunctionValue[] convs = new FunctionValue[fromtt.Types.Length];
                        for (int t = 0; t < convs.Length; t++)
                        {
                            if ((convs[t] = Expression.GetConversionFixed(ffixedconv, fromtt.Types[t], tott.Types[t])) == null)
                            {
                                return Nothing;
                            }
                            else
                            {
                                if (convs[t] == FunctionValue.Identity)
                                {
                                    // Quick optimization, since most tuple parts remain unaffected.
                                    convs[t] = null;
                                }
                            }
                        }
                        
                        return Just(FunctionValue.Create(delegate(Value values)
                        {
                            TupleValue tv = (TupleValue)values;
                            Value[] outvals = new Value[convs.Length];
                            for (int t = 0; t < convs.Length; t++)
                            {
                                FunctionValue conv = convs[t];
                                if (conv == null)
                                {
                                    outvals[t] = tv.Values[t]; 
                                }
                                else
                                {
                                    outvals[t] = conv.Call(tv.Values[t]);
                                }
                            }
                            return new TupleValue(outvals);
                        }));
                    }

                    // Well, I don't know...
                    return Nothing;
                });
            });
        }

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
                    new FunctionType(Type.UniversalType, FunctionValue.Constant(Type.UniversalType)),
                    MaybeTypeConstructor));
                this._AddBinaryFunction("+", IntType, IntType, IntType, x => MakeIntValue(GetIntValue(x.Get(0)) + GetIntValue(x.Get(1))));
                this._AddBinaryFunction("-", IntType, IntType, IntType, x => MakeIntValue(GetIntValue(x.Get(0)) - GetIntValue(x.Get(1))));
                this._AddBinaryFunction("*", IntType, IntType, IntType, x => MakeIntValue(GetIntValue(x.Get(0)) * GetIntValue(x.Get(1))));
            }

            private void _AddBinaryFunction(string Name, Type TypeA, Type TypeB, Type ReturnType, FunctionHandler Handler)
            {
                this.RootValues.Add(Name, new Datum(
                    new FunctionType(TupleType.Create(TypeA, TypeB), FunctionValue.Constant(ReturnType)),
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