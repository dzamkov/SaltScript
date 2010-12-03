using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// A possible form of a variant type.
    /// </summary>
    public struct VariantForm
    {
        public VariantForm(string Name, Type DataType)
        {
            this.Name = Name;
            this.DataType = DataType;
        }

        /// <summary>
        /// The name of this form.
        /// </summary>
        public string Name;

        /// <summary>
        /// The type of the data associated with this form.
        /// </summary>
        public Type DataType;
    }

    /// <summary>
    /// A value of a variant type.
    /// </summary>
    public class VariantValue : Value
    {
        public VariantValue(int FormIndex, Value Data)
        {
            this.FormIndex = FormIndex;
            this.Data = Data;
        }

        /// <summary>
        /// The index of the form of this value.
        /// </summary>
        public int FormIndex;

        /// <summary>
        /// The data associated with this value, or null if the form does not require any additional data.
        /// </summary>
        public Value Data;
    }

    /// <summary>
    /// A function value for a constructor that makes a variant.
    /// </summary>
    public class VariantConstructor : FunctionValue
    {
        public VariantConstructor(int FormIndex)
        {
            this.FormIndex = FormIndex;
        }

        public override Value Call(Value Argument)
        {
            return new VariantValue(this.FormIndex, Argument);
        }

        /// <summary>
        /// The index of the form that is constructed.
        /// </summary>
        public int FormIndex;
    }
}