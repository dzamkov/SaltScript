using System;
using System.Collections.Generic;

namespace SaltScript
{
    /// <summary>
    /// A mapping of a subset of variable indices to values.
    /// </summary>
    public interface IVariableMap<TValue>
    {
        /// <summary>
        /// Looks up the specified variable index in the map, and if found, returns true and set Value to it.
        /// </summary>
        bool Lookup(int Index, ref TValue Value);
    }

    /// <summary>
    /// A variable map that can be appened to.
    /// </summary>
    public interface IVariableStack<TValue> : IVariableMap<TValue>
    {
        /// <summary>
        /// Gets the next variable index that is not yet in the stack.
        /// </summary>
        int NextFreeIndex { get; }

        /// <summary>
        /// Returns a stack with the specified values appened to the end.
        /// </summary>
        IVariableStack<TValue> Append(TValue[] Values);

        /// <summary>
        /// Returns a stack with only the variables before the specified index.
        /// </summary>
        IVariableStack<TValue> Cut(int To);
    }

    /// <summary>
    /// A variable stack whose values can change.
    /// </summary>
    public interface IMutableVariableStack<TValue> : IVariableStack<TValue>
    {
        /// <summary>
        /// Sets a value in a mutable section on the stack.
        /// </summary>
        void Modify(int Index, TValue Value);

        /// <summary>
        /// Appends a mutable section to the stack.
        /// </summary>
        IMutableVariableStack<TValue> AppendMutable(TValue[] Values);

        /// <summary>
        /// Gets an immutable copy of the stack in its current state.
        /// </summary>
        IVariableStack<TValue> Freeze { get; }
    }

    /// <summary>
    /// A variable map that only has one variable assigned.
    /// </summary>
    public class SingleVariableMap<TValue> : IVariableMap<TValue>
    {
        public SingleVariableMap(int Index, TValue Value)
        {
            this.Index = Index;
            this.Value = Value;
        }

        public bool Lookup(int Index, ref TValue Value)
        {
            if (Index == this.Index)
            {
                Value = this.Value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The index of the variable.
        /// </summary>
        public int Index;

        /// <summary>
        /// The value of the variable.
        /// </summary>
        public TValue Value;
    }

    /// <summary>
    /// A map that captures a subset of another.
    /// </summary>
    public class SubsetMap<TValue> : IVariableMap<TValue>
    {
        public SubsetMap(int Start, int Amount, IVariableMap<TValue> Source)
        {
            this._Start = Start;
            this._Amount = Amount;
            this._Source = Source;
        }

        public bool Lookup(int Index, ref TValue Value)
        {
            if (Index >= this._Start && Index < this._Start + this._Amount)
            {
                return this._Source.Lookup(Index, ref Value);
            }
            return false;
        }

        private int _Start;
        private int _Amount;
        private IVariableMap<TValue> _Source;
    }

    /// <summary>
    /// A map that takes from two sources depending on wether the index is less than or greater than the switch index.
    /// </summary>
    public class SwitchMap<TValue> : IVariableMap<TValue>
    {
        public SwitchMap(int SwitchIndex, IVariableMap<TValue> Low, IVariableMap<TValue> High)
        {
            this._SwitchIndex = SwitchIndex;
            this._Low = Low;
            this._High = High;
        }

        public bool Lookup(int Index, ref TValue Value)
        {
            if (Index >= this._SwitchIndex)
            {
                return this._High.Lookup(Index, ref Value);
            }
            else
            {
                return this._Low.Lookup(Index, ref Value);
            }
        }

        private int _SwitchIndex;
        private IVariableMap<TValue> _Low;
        private IVariableMap<TValue> _High;
    }

    /// <summary>
    /// A map that takes variables from an array.
    /// </summary>
    public class SimpleMap<TValue> : IVariableMap<TValue>
    {
        public SimpleMap(int StartIndex, TValue[] Values)
        {
            this._StartIndex = StartIndex;
            this._Values = Values;
        }

        public bool Lookup(int Index, ref TValue Value)
        {
            Index -= this._StartIndex;
            if (Index >= 0 && Index < this._Values.Length)
            {
                Value = this._Values[Index];
            }
            return false;
        }

        private int _StartIndex;
        private TValue[] _Values;
    }

    /// <summary>
    /// An implementation of a spaghetti stack, which allows efficent appending at the cost of lookup speed.
    /// </summary>
    public class SpaghettiStack<TValue> : IMutableVariableStack<TValue>
    {
        private SpaghettiStack()
        {

        }

        public SpaghettiStack(TValue[] Values)
            : this(0, Values)
        {

        }

        public SpaghettiStack(int StartIndex, TValue[] Values)
            : this(null, StartIndex, Values)
        {

        }

        public SpaghettiStack(SpaghettiStack<TValue> Previous, int StartIndex, TValue[] Values)
            : this(false, Previous, StartIndex, Values)
        {

        }

        public SpaghettiStack(bool Mutable, SpaghettiStack<TValue> Previous, int StartIndex, TValue[] Values)
        {
            this._Mutable = Previous != null ? Mutable || Previous._Mutable : Mutable;
            this._Previous = Previous;
            this._StartIndex = StartIndex;
            this._Values = Values;
        }

        /// <summary>
        /// Creates a stack that is empty up until the specified variable.
        /// </summary>
        public static SpaghettiStack<TValue> Empty(int Start)
        {
            return new SpaghettiStack<TValue>(Start, new TValue[0]);
        }

        public IMutableVariableStack<TValue> AppendMutable(TValue[] Values)
        {
            return new SpaghettiStack<TValue>(true, this, this.NextFreeIndex, Values);
        }

        public IVariableStack<TValue> Freeze
        {
            get 
            {
                if (this._Mutable)
                {
                    TValue[] nvals = new TValue[this._Values.Length];
                    for (int t = 0; t < this._Values.Length; t++)
                    {
                        nvals[t] = this._Values[t];
                    }
                    return new SpaghettiStack<TValue>()
                    {
                        _Mutable = false,
                        _Previous = this._Previous == null ? null : (SpaghettiStack<TValue>)this._Previous.Freeze,
                        _StartIndex = this._StartIndex,
                        _Values = nvals
                    };
                }
                return this;
            }
        }

        public int NextFreeIndex
        {
            get 
            {
                return this._StartIndex + this._Values.Length;
            }
        }

        public IVariableStack<TValue> Append(TValue[] Values)
        {
            return new SpaghettiStack<TValue>(false, this, this.NextFreeIndex, Values);
        }

        public IVariableStack<TValue> Cut(int To)
        {
            SpaghettiStack<TValue> start;
            if (To != this.NextFreeIndex && this._GetIndex(To, out start, out To))
            {
                return new SpaghettiStack<TValue>()
                {
                    _Previous = start,
                    _StartIndex = To,
                    _Values = new TValue[0]
                };
            }
            return this;
        }

        public bool Lookup(int Index, ref TValue Value)
        {
            SpaghettiStack<TValue> stack;
            int valindex;
            if (this._GetIndex(Index, out stack, out valindex))
            {
                Value = stack._Values[valindex];
                return true;
            }
            else
            {
                return false;
            }
        }
        public void Modify(int Index, TValue Value)
        {
            SpaghettiStack<TValue> stack;
            int valindex;
            this._GetIndex(Index, out stack, out valindex);
            stack._Values[valindex] = Value;
        }

        private bool _GetIndex(int Index, out SpaghettiStack<TValue> Stack, out int ValIndex)
        {
            Stack = this;
            while (Stack._StartIndex > Index)
            {
                Stack = Stack._Previous;
                if (Stack == null)
                {
                    ValIndex = 0;
                    return false;
                }
            }
            ValIndex = Index - Stack._StartIndex;
            if (ValIndex < Stack._Values.Length)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private SpaghettiStack<TValue> _Previous;
        private int _StartIndex;
        private TValue[] _Values;
        private bool _Mutable;
    }
}