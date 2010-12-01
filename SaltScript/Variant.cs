using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// The type for a variant whose values can have one of many forms (a generalization of enumerated types).
    /// </summary>
    public class VariantType : Type
    {
        public VariantType(IEnumerable<VariantForm> Forms)
            : this(new List<VariantForm>(Forms))
        {

        }

        public VariantType(List<VariantForm> Forms)
        {
            this.FormsByName = new Dictionary<string, int>();
            this.Forms = Forms;
            int i = 0;
            foreach (VariantForm vf in this.Forms)
            {
                this.FormsByName.Add(vf.Name, i);
                i++;
            }
        }

        public VariantType()
        {

        }

        /// <summary>
        /// Gets a variant form description by its name.
        /// </summary>
        public bool Lookup(string FormName, out VariantForm Form, out int Index)
        {
            if (this.FormsByName.TryGetValue(FormName, out Index))
            {
                Form = this.Forms[Index];
                return true;
            }
            else
            {
                Form = new VariantForm();
                return false;
            }
        }

        public override string Display(Type Type)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("variant { ");
            bool comma = false;
            foreach (VariantForm vf in this.Forms)
            {
                if (comma)
                {
                    sb.Append(", ");
                }
                sb.Append(vf.Name);
                if (vf.DataType != null)
                {
                    sb.Append("(");
                    sb.Append(vf.DataType.Display(Type.UniversalType));
                    sb.Append(")");
                }
                comma = true;
            }
            sb.Append(" }");
            return sb.ToString();
        }

        public Dictionary<string, int> FormsByName;
        public List<VariantForm> Forms;
    }

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

        public override string Display(Type Type)
        {
            VariantForm vf = (Type as VariantType).Forms[this.FormIndex];
            StringBuilder sb = new StringBuilder();
            sb.Append(vf.Name);
            sb.Append("(");
            sb.Append(this.Data.Display(vf.DataType));
            sb.Append(")");
            return sb.ToString();
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

        public override string Display(Type Type)
        {
            return "<variant constructor>";
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