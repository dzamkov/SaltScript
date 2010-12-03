using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// Represents a runtime value.
    /// </summary>
    public class Value
    {
        /// <summary>
        /// A convenience function for getting the specified part of a tuple value.
        /// </summary>
        public Value Get(int Index)
        {
            TupleValue tv = this as TupleValue;
            if (tv != null)
            {
                return tv.Values[Index];
            }
            return this;
        }
    }

    /// <summary>
    /// A combination of a runtime value with a type, valid in a certain scope.
    /// </summary>
    public struct Datum
    {
        public Datum(Value Value, Expression Type)
        {
            this.Value = Value;
            this.Type = Type;
        }

        /// <summary>
        /// The value of the datum.
        /// </summary>
        public Value Value;

        /// <summary>
        /// The type of the datum.
        /// </summary>
        public Expression Type;

        /// <summary>
        /// Creates a "friendly" string representation of the datum.
        /// </summary>
        public string Display()
        {
            return this.Value.ToString();
        }
    }
}