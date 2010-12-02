using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// A heterogeneous collection of values. A tuple value may also be a type if all its values are types.
    /// </summary>
    public class TupleValue : Value
    {
        public TupleValue(Value[] Values)
        {
            this.Values = Values;
        }

        public override Type AsType
        {
            get
            {
                if (this.Values == null)
                {
                    return new TupleType(null);
                }
                Type[] types = new Type[this.Values.Length];
                for (int t = 0; t < types.Length; t++)
                {
                    if ((types[t] = this.Values[t].AsType) == null)
                    {
                        return null;
                    }
                }
                return new TupleType(types);
            }
        }

        /// <summary>
        /// The value for a tuple with no items.
        /// </summary>
        public static readonly TupleValue Empty = new TupleValue(null);

        /// <summary>
        /// Creates a tuple with one item.
        /// </summary>
        public static TupleValue Create(Value A)
        {
            return new TupleValue(new Value[] { A });
        }

        /// <summary>
        /// Creates a tuple with two items.
        /// </summary>
        public static TupleValue Create(Value A, Value B)
        {
            return new TupleValue(new Value[] { A, B });
        }

        /// <summary>
        /// Creates a tuple with three items.
        /// </summary>
        public static TupleValue Create(Value A, Value B, Value C)
        {
            return new TupleValue(new Value[] { A, B, C });
        }

        public override string Display(Type Type)
        {
            StringBuilder sb = new StringBuilder();
            TupleType tt = (TupleType)Type;
            for (int t = 0; t < this.Values.Length; t++)
            {
                if (t > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(this.Values[t].Display(tt.Types[t]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// The values that make up the tuple. This can be null in the case of an empty tuple.
        /// </summary>
        public Value[] Values;
    }

    /// <summary>
    /// A type of a tuple.
    /// </summary>
    public class TupleType : Type
    {
        public TupleType(Type[] Types)
        {
            this.Types = Types;
        }

        /// <summary>
        /// The type for a tuple with no items.
        /// </summary>
        public static readonly TupleType Empty = new TupleType(null);

        /// <summary>
        /// Creates a type for a tuple with one item.
        /// </summary>
        public static TupleType Create(Type A)
        {
            return new TupleType(new Type[] { A });
        }

        /// <summary>
        /// Creates a type for a tuple with two items.
        /// </summary>
        public static TupleType Create(Type A, Type B)
        {
            return new TupleType(new Type[] { A, B });
        }

        /// <summary>
        /// Creates a type for a tuple with three items.
        /// </summary>
        public static TupleType Create(Type A, Type B, Type C)
        {
            return new TupleType(new Type[] { A, B, C });
        }

        /// <summary>
        /// The types for the items in a tuple of this type.
        /// </summary>
        public Type[] Types;
    }

    /// <summary>
    /// An expression that combines several values into a tuple.
    /// </summary>
    public class TupleExpression : Expression
    {
        public TupleExpression()
        {

        }

        public TupleExpression(Expression[] Parts)
        {
            this.Parts = Parts;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            if (this.Parts != null)
            {
                Value[] vals = new Value[this.Parts.Length];
                for (int t = 0; t < this.Parts.Length; t++)
                {
                    vals[t] = this.Parts[t].Evaluate(Stack);
                }
                return new TupleValue(vals);
            }
            else
            {
                return TupleValue.Empty;
            }
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            if (this.Parts != null)
            {
                Type[] types = new Type[this.Parts.Length];
                for (int t = 0; t < this.Parts.Length; t++)
                {
                    types[t] = this.Parts[t].GetType(Stack);
                }
                return new TupleType(types);
            }
            else
            {
                return TupleType.Empty;
            }
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            if (this.Parts != null)
            {
                Expression[] subs = new Expression[this.Parts.Length];
                for (int t = 0; t < this.Parts.Length; t++)
                {
                    subs[t] = this.Parts[t].Substitute(Stack);
                }
                return new TupleExpression(subs);
            }
            else
            {
                return Empty;
            }
        }

        public override void TypeCheck(VariableStack<Expression> Stack,
            FunctionValue ConversionFactory,
            out Expression TypeSafeExpression, out Type Type)
        {
            if (this.Parts != null && this.Parts.Length != 0)
            {
                Expression[] sparts = new Expression[this.Parts.Length];
                Type[] stypes = new Type[this.Parts.Length];
                for (int t = 0; t < this.Parts.Length; t++)
                {
                    this.Parts[t].TypeCheck(Stack, ConversionFactory, out sparts[t], out stypes[t]);
                }
                TypeSafeExpression = new TupleExpression(sparts);
                Type = new TupleType(stypes);
            }
            else
            {
                TypeSafeExpression = Empty;
                Type = TupleType.Empty;
            }
        }

        /// <summary>
        /// A make tuple expression that makes an empty tuple.
        /// </summary>
        public static readonly TupleExpression Empty = new TupleExpression();

        /// <summary>
        /// The individual parts of the tuple.
        /// </summary>
        public Expression[] Parts;
    }
}