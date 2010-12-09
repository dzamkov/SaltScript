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

        /// <summary>
        /// The values that make up the tuple. This can be null in the case of an empty tuple.
        /// </summary>
        public Value[] Values;
    }
    /// <summary>
    /// An expression that combines several values into a tuple. This expression can be evaluated, and used as a type.
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

        public override Expression Compress(int Start, int Amount)
        {
            if (this.Parts == null)
            {
                return Empty;
            }
            Expression[] nparts = new Expression[this.Parts.Length];
            for (int t = 0; t < nparts.Length; t++)
            {
                if ((nparts[t] = this.Parts[t].Compress(Start, Amount)) == null)
                {
                    return null;
                }
            }
            return new TupleExpression(nparts);
        }

        public override Value Evaluate(IMutableVariableStack<Value> Stack)
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

        public override Expression Substitute(IVariableMap<Expression> Map)
        {
            if (this.Parts != null)
            {
                Expression[] subs = new Expression[this.Parts.Length];
                for (int t = 0; t < this.Parts.Length; t++)
                {
                    subs[t] = this.Parts[t].Substitute(Map);
                }
                return new TupleExpression(subs);
            }
            else
            {
                return Empty;
            }
        }

        public override void TypeCheck(
            IVariableStack<Expression> TypeStack,
            IVariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            if (this.Parts != null && this.Parts.Length != 0)
            {
                Expression[] sparts = new Expression[this.Parts.Length];
                Expression[] stypes = new Expression[this.Parts.Length];
                for (int t = 0; t < this.Parts.Length; t++)
                {
                    this.Parts[t].TypeCheck(TypeStack, Stack, out sparts[t], out stypes[t]);
                }
                TypeSafeExpression = new TupleExpression(sparts);
                Type = new TupleExpression(stypes);
            }
            else
            {
                TypeSafeExpression = Empty;
                Type = Empty;
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

    /// <summary>
    /// An expression that causes a tuple to be split into several variables in a subordinate expression.
    /// </summary>
    public class TupleBreakExpression : Expression
    {
        public TupleBreakExpression(int BreakIndex, int TupleSize, Expression Tuple, Expression InnerExpression)
        {
            this.BreakIndex = BreakIndex;
            this.TupleSize = TupleSize;
            this.SourceTuple = Tuple;
            this.InnerExpression = InnerExpression;
        }

        public override Expression Compress(int Start, int Amount)
        {
            Expression source = this.SourceTuple.Compress(Start, Amount);
            Expression inner = this.InnerExpression.Compress(Start, Amount);
            if (source != null && inner != null)
            {
                return new TupleBreakExpression(this.BreakIndex - Amount, this.TupleSize, source, inner);
            }
            else
            {
                return null;
            }
        }

        public override Expression Substitute(IVariableMap<Expression> Map)
        {
            return new TupleBreakExpression(
                this.BreakIndex,
                this.TupleSize,
                this.SourceTuple.Substitute(Map),
                this.InnerExpression.Substitute(this._CreateInner(Map)));
        }

        private IVariableMap<Expression> _CreateInner(IVariableMap<Expression> Source)
        {
            Expression[] stackappend = new Expression[this.TupleSize];
            for (int t = 0; t < stackappend.Length; t++)
            {
                stackappend[t] = Expression.Variable(this.BreakIndex + t);
            }
            return new SwitchMap<Expression>(
                this.BreakIndex,
                Source,
                new SimpleMap<Expression>(this.BreakIndex, stackappend));
        }

        public override bool Reduce(IVariableMap<Expression> Map, ref Expression Reduced)
        {
            // Is the source tuple an actual tuple?
            TupleExpression te = this.SourceTuple as TupleExpression;
            if (te != null)
            {
                // Now we got this :D
                Reduced = this.InnerExpression.Substitute(new SimpleMap<Expression>(this.BreakIndex, te.Parts));
                return true;
            }

            // Wouldn't it be hilarious if the inner expression never even used the tuple's data?
            Expression cire = this.InnerExpression.Compress(this.BreakIndex, this.TupleSize);
            if (cire != null)
            {
                Reduced = cire;
                return true;
            }

            // Nope, guess i'll have to do it the normal way :(
            Expression tre = this.SourceTuple;
            Expression ire = this.InnerExpression;
            if (tre.Reduce(Map, ref tre) | ire.Reduce(this._CreateInner(Map), ref ire))
            {
                Reduced = new TupleBreakExpression(this.BreakIndex, this.TupleSize, tre, ire);
                return true;
            }

            return false;
        }

        public override Value Evaluate(IMutableVariableStack<Value> Stack)
        {
            TupleValue tuple = (TupleValue)this.SourceTuple.Evaluate(Stack);
            if (tuple.Values != null)
            {
                return this.InnerExpression.Evaluate((IMutableVariableStack<Value>)Stack.Append(tuple.Values));
            }
            else
            {
                return this.InnerExpression.Evaluate(Stack);
            }
        }

        public override void TypeCheck(
            IVariableStack<Expression> TypeStack, 
            IVariableStack<Expression> Stack, 
            out Expression TypeSafeExpression, out Expression Type)
        {
            Expression stuple;
            Expression tupletype;
            this.SourceTuple.TypeCheck(TypeStack, Stack, out stuple, out tupletype);

            TupleExpression te;
            while ((te = tupletype as TupleExpression) == null && tupletype.Reduce(Stack, ref tupletype)) ;
            if (te == null)
            {
                throw new NotImplementedException();
            }

            TupleExpression se;
            while ((se = stuple as TupleExpression) == null && stuple.Reduce(Stack, ref stuple)) ;
            Expression[] stackappend;
            if (se != null)
            {
                stackappend = se.Parts ?? new Expression[0];
                if (stackappend.Length != this.TupleSize)
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                if (te.Parts.Length != this.TupleSize)
                {
                    throw new NotImplementedException();
                }

                stackappend = new Expression[te.Parts.Length];
                int ni = Stack.NextFreeIndex;
                for (int t = 0; t < te.Parts.Length; t++)
                {
                    stackappend[t] = Expression.Variable(t + ni);
                }
            }

            Expression si;
            Expression itype;
            this.InnerExpression.TypeCheck(
                TypeStack.Cut(this.BreakIndex).Append(te.Parts ?? new Expression[0]),
                Stack.Cut(this.BreakIndex).Append(stackappend),
                out si,
                out itype);

            TypeSafeExpression = new TupleBreakExpression(this.BreakIndex, this.TupleSize, stuple, si);
            Type = itype;
        }

        /// <summary>
        /// The index of the first variable broken from the tuple.
        /// </summary>
        public int BreakIndex;

        /// <summary>
        /// The size of the source tuple.
        /// </summary>
        public int TupleSize;

        /// <summary>
        /// The tuple to "break".
        /// </summary>
        public Expression SourceTuple;

        /// <summary>
        /// The expression where the tuple's parts can be accessed.
        /// </summary>
        public Expression InnerExpression;
    }
}