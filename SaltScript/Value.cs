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
        /// <summary>
        /// Gets if two values, if interpreted as the specified type, are the same.
        /// </summary>
        public static bool Equals(Type Type, Value A, Value B)
        {
            if (A == B)
            {
                return true;
            }
            if (A.Hash != B.Hash)
            {
                return false;
            }
            return A.Equals(Type, B);
        }

        /// <summary>
        /// Gets if this value equals (can be replaced with anywhere in a script, without changing any results) another.
        /// </summary>
        public virtual bool Equals(Type Type, Value Other)
        {
            return false;
        }

        /// <summary>
        /// Gets a hashcode such that two values with different hashcodes must be different.
        /// </summary>
        public virtual int Hash
        {
            get
            {
                return this.GetHashCode();
            }
        }

        /// <summary>
        /// Gets a "friendly" representation of the value when interpreted as the specified type.
        /// </summary>
        public virtual string Display(Type Type)
        {
            return "<unknown>";
        }

        /// <summary>
        /// A convenience function that gets a part of a tuple. This will not work on non-tuples. 
        /// </summary>
        public Value Get(int Index)
        {
            TupleValue tv = this as TupleValue;
            if (tv != null)
            {
                return tv.Values[Index];
            }
            TupleType tt = this as TupleType;
            if (tt != null)
            {
                return tt.Types[Index];
            }
            return null;
        }

        /// <summary>
        /// If this value is a type (and can be interpreted as a value of the type "type"), this returns a type
        /// representation of it. Returns null otherwise.
        /// </summary>
        public virtual Type AsType
        {
            get
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Represents a type for the script.
    /// </summary>
    public class Type : Value
    {
        /// <summary>
        /// All types are values with this type, including this type.
        /// </summary>
        public static readonly Type UniversalType = new _TypeType();

        public override Type AsType
        {
            get
            {
                return this;
            }
        }

        private class _TypeType : Type
        {
            public override string Display(Type Type)
            {
                return "type";
            }
        }
    }

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
        /// Gets a "friendly" string form of this datum.
        /// </summary>
        public string Display
        {
            get
            {
                return this.Value.Display(this.Type);
            }
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
}