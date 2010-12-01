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
    /// The type for a variant whose values can have one of many forms (a generalization of enumerated types).
    /// </summary>
    public class VariantType : Type
    {
        public VariantType(IEnumerable<VariantForm> Forms) : this(new List<VariantForm>(Forms))
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
    /// The type for a function.
    /// </summary>
    public class FunctionType : Type
    {
        public FunctionType(Type Argument, FunctionValue ReturnType)
        {
            this.Argument = Argument;
            this.ReturnType = ReturnType;
        }

        public override string Display(Type Type)
        {
            // not much we can do here...
            return "<functiontype>";
        }

        /// <summary>
        /// Gets the return type for this function, given the current stack and an expression giving the arguments.
        /// </summary>
        public Type GetReturnType(VariableStack<Expression> Stack, Expression Argument)
        {
            return this.ReturnType.SubstituteCall(Argument.Substitute(Stack)).AsType;
        }

        /// <summary>
        /// The argument to the function. Null or an empty tuple type can be used to indicate no arguments. A tuple type can be used
        /// to indicate more than one argument.
        /// </summary>
        public Type Argument;

        /// <summary>
        /// A function that when called with the value of the argument, will find the return type of this function.
        /// </summary>
        public FunctionValue ReturnType;
    }

    /// <summary>
    /// A value that can be invoked/called/executed.
    /// </summary>
    public abstract class FunctionValue : Value
    {
        public override string Display(Type Type)
        {
            return "<function>";
        }

        /// <summary>
        /// Calls the function with the specified arguments. Type checking must be performed before calling. Arguments may not be changed
        /// after this call. An argument of null can be supplied if the function type does not require an argument.
        /// </summary>
        public abstract Value Call(Value Argument);

        /// <summary>
        /// Calls the function by providing an expression that yields the argument when evaluated. This can be used to avoid computation when it
        /// isn't needed.
        /// </summary>
        public virtual Value SubstituteCall(Expression Argument)
        {
            return this.Call(Argument.Value);
        }

        /// <summary>
        /// If this has the type &lt;x&gt;x, creates a value of type x, 
        /// by calling the function with itself. the current function should not unconditionally require (invoke/use) its first
        /// parameter as this may lead to a stack overflow or circular dependency (either while calling this fix function, or at runtime).
        /// </summary>
        /// <remarks>If you still don't understand http://en.wikipedia.org/wiki/Fixed_point_combinator </remarks>
        public virtual Value Fix
        {
            get
            {
                // This way only works for functions...
                return new FixFunction(this);
            }
        }

        /// <summary>
        /// Creates a constant function for the specified value.
        /// </summary>
        public static ConstantFunction Constant(Value Value)
        {
            return new ConstantFunction(Value);
        }

        /// <summary>
        /// Gets the identity function value. The identity function will always return what it is called with.
        /// </summary>
        public static readonly FunctionValue Identity = new _IdentityFunction();

        /// <summary>
        /// Creates a function value from a native .net function.
        /// </summary>
        public static FunctionValue Create(FunctionHandler Function)
        {
            return new _NativeFunction() { Function = Function };
        }

        private class _NativeFunction : FunctionValue
        {
            public override Value Call(Value Argument)
            {
                return this.Function(Argument);
            }
            public FunctionHandler Function;
        }

        private class _IdentityFunction : FunctionValue
        {
            public override Value Call(Value Argument)
            {
                return Argument;
            }

            public override Value SubstituteCall(Expression Argument)
            {
                return Argument.Value;
            }
        }
    }

    /// <summary>
    /// The value produced while performing the "Fix" operation on a function that itself returns a function.
    /// </summary>
    public class FixFunction : FunctionValue
    {
        public FixFunction(FunctionValue Source)
        {
            this.FixValue = (FunctionValue)Source.Call(this);
        }

        public override Value Call(Value Argument)
        {
            return this.FixValue.Call(Argument);
        }

        public override Value SubstituteCall(Expression Argument)
        {
            return this.FixValue.SubstituteCall(Argument);
        }

        public FunctionValue FixValue;
    }

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
    /// A function that always returns the same value.
    /// </summary>
    public class ConstantFunction : FunctionValue
    {
        public ConstantFunction(Value Value)
        {
            this.Value = Value;
        }

        public override Value Call(Value Argument)
        {
            return this.Value;
        }

        public override Value SubstituteCall(Expression Argument)
        {
            return this.Value;
        }

        /// <summary>
        /// The value returned by this function.
        /// </summary>
        public Value Value;
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

    /// <summary>
    /// The delegate type for .net functions that can act as SaltScript functions.
    /// </summary>
    public delegate Value FunctionHandler(Value Argument);

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

    /// <summary>
    /// Relates variable names to their position on the stack.
    /// </summary>
    public class Scope
    {

        /// <summary>
        /// Gets the functional depth and index of the specified variable, if it is found.
        /// </summary>
        public bool LookupVariable(string Name, out int FunctionalDepth, out int Index)
        {
            if (this.Variables.TryGetValue(Name, out Index))
            {
                FunctionalDepth = this.FunctionalDepth;
                return true;
            }
            else
            {
                if (this.Parent != null)
                {
                    return this.Parent.LookupVariable(Name, out FunctionalDepth, out Index);
                }
                else
                {
                    FunctionalDepth = 0;
                    return false;
                }
            }
        }

        /// <summary>
        /// Parent scope for this scope. Variables in the parent scope can be used in a more immediate scope.
        /// </summary>
        public Scope Parent;

        /// <summary>
        /// Table of variable names and their relative stack indices.
        /// </summary>
        public Dictionary<string, int> Variables;

        /// <summary>
        /// Functional depth of this scope.
        /// </summary>
        public int FunctionalDepth;
    }

    /// <summary>
    /// Maps numerically indexed variables to values.
    /// </summary>
    public class VariableStack<TValue>
    {
        public VariableStack()
        {

        }

        public VariableStack(TValue[] Values)
        {
            this.Values = Values;
        }

        /// <summary>
        /// Appends values to the stack and returns the new stack.
        /// </summary>
        public VariableStack<TValue> Append(TValue[] Values)
        {
            return new VariableStack<TValue>()
            {
                FunctionalDepth = this.FunctionalDepth,
                Lower =  this,
                StartIndex = this.StartIndex + this.Values.Length,
                Values = Values
            };
        }

        /// <summary>
        /// Gets the value for the variable with the specified start index and functional depth.
        /// </summary>
        public TValue Lookup(int FunctionalDepth, int Index)
        {
            VariableStack<TValue> curstack = this;
            while (curstack.FunctionalDepth > FunctionalDepth || curstack.StartIndex > Index)
            {
                curstack = curstack.Lower;
            }
            return curstack.Values[Index - curstack.StartIndex];
        }

        /// <summary>
        /// Gets the lower levels of the stack.
        /// </summary>
        public VariableStack<TValue> Lower;

        /// <summary>
        /// The functional depth this part of the stack is on.
        /// </summary>
        public int FunctionalDepth;

        /// <summary>
        /// The index of the first value recorded in this part of the stack.
        /// </summary>
        public int StartIndex;

        /// <summary>
        /// Values for variables on this portion of the stack.
        /// </summary>
        public TValue[] Values;
    }

    /// <summary>
    /// Represents an expression, which can be evaluated if all scope variables are known.
    /// </summary>
    public abstract class Expression
    {
        /// <summary>
        /// Evaluates the expression with the given immediate value stack.
        /// </summary>
        public abstract Value Evaluate(VariableStack<Value> Stack);

        /// <summary>
        /// Gets the type of this expression, given the expressions that define the variables in this expression.
        /// </summary>
        public abstract Type GetType(VariableStack<Expression> Stack);

        /// <summary>
        /// Substitutes every variable in the expression with its corresponding expression on the stack.
        /// </summary>
        public abstract Expression Substitute(VariableStack<Expression> Stack);

        /// <summary>
        /// Creates a type-safe version of the expression by using conversions where necessary. An exception will
        /// be thrown if this is not possible. Additionally the type of the expression will be returned. Both conversions are of type
        /// &lt;x&gt;x where x = &lt;type a, type b&gt;maybe(&lt;a&gt;b)
        /// </summary>
        /// <remarks>Type checking should always be performed before any other operation on the expression, as only the results
        /// given from type checking can be guaranteed to be accurate (along with all operations on the type safe expression). </remarks>
        public virtual void TypeCheck(
            VariableStack<Expression> Stack, 
            FunctionValue ConversionFactory,
            out Expression TypeSafeExpression, out Type Type)
        {
            TypeSafeExpression = this;
            Type = this.GetType(Stack);
        }

        /// <summary>
        /// Gets the value of the expression if there are no variables dependant on an immediate stack.
        /// </summary>
        public Value Value
        {
            get
            {
                return this.Evaluate(null);
            }
        }

        /// <summary>
        /// Gets the type of the expression if one can be found without given the definitions of variables dependant on the
        /// immediate stack.
        /// </summary>
        public Type Type
        {
            get
            {
                return this.GetType(null);
            }
        }

        /// <summary>
        /// Creates an expression that always evaluates to the same value.
        /// </summary>
        public static ValueExpression Constant(Type Type, Value Value)
        {
            return new ValueExpression(Type, Value);
        }

        /// <summary>
        /// The type of expression conversions factories. Functions of this type are used to find the conversion function
        /// from one type to another. Note that the conversion function is recursive, and takes a fixed conversion
        /// function.
        /// </summary>
        public static readonly FunctionType ConversionFactoryType = new FunctionType(FixedConversionType, FunctionValue.Constant(FixedConversionType));

        /// <summary>
        /// The more basic type of a conversion factory (nonrecursive).
        /// </summary>
        public static readonly FunctionType FixedConversionType = _CreateFixedConversionType();

        /// <summary>
        /// Returns a function that take a value of type "From" and converts it to a value of type "To". Null is returned if no conversion
        /// exists for the type pair.
        /// </summary>
        public static FunctionValue GetConversion(FunctionValue UnfixedConversionFactory, Type From, Type To)
        {
            if (From == To)
            {
                return FunctionValue.Identity;
            }
            return GetConversionFixed((FunctionValue)UnfixedConversionFactory.Fix, From, To);
        }

        /// <summary>
        /// Gets the type pair conversion function from a fixed conversion facory.
        /// </summary>
        public static FunctionValue GetConversionFixed(FunctionValue FixedConversionFactory, Type From, Type To)
        {
            if (From == To)
            {
                return FunctionValue.Identity;
            }
            VariantValue maybeconversion = (VariantValue)(FixedConversionFactory).Call(TupleValue.Create(From, To));
            Value conversion;
            if (Default.HasValue(maybeconversion, out conversion))
            {
                return (FunctionValue)conversion;
            }
            else
            {
                return null;
            }
        }

        private static FunctionType _CreateFixedConversionType()
        {
            return new FunctionType(
                TupleType.Create(Type.UniversalType, Type.UniversalType),
                FunctionValue.Create(delegate(Value TypeTuple)
                {
                    return Default.MaybeTypeConstructor.Call(
                        new FunctionType(
                            TypeTuple.Get(0).AsType,
                            FunctionValue.Constant(TypeTuple.Get(1))));
                }));
        }
    }

    /// <summary>
    /// An expression that represents an immutable value of a certain type.
    /// </summary>
    public class ValueExpression : Expression
    {
        public ValueExpression(Type Type, Value Value)
        {
            this.Datum = new Datum(Type, Value);
        }

        public ValueExpression(Datum Datum)
        {
            this.Datum = Datum;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return this.Datum.Value;
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            return this.Datum.Type;
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return this;
        }

        /// <summary>
        /// The datum that is always produced by this expression.
        /// </summary>
        public Datum Datum;
    }

    /// <summary>
    /// An expression that represents a variable in the active scope.
    /// </summary>
    public class VariableExpression : Expression
    {
        public VariableExpression(int StackPosition, int FunctionalDepth)
        {
            this.StackIndex = StackPosition;
            this.FunctionalDepth = FunctionalDepth;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return Stack.Lookup(this.FunctionalDepth, this.StackIndex);
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            return Stack.Lookup(this.FunctionalDepth, this.StackIndex).Type;
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return Stack.Lookup(this.FunctionalDepth, this.StackIndex);
        }

        /// <summary>
        /// Relative position of the variable on the stack of the function that defines it.
        /// </summary>
        public int StackIndex;

        /// <summary>
        /// The amount of functional scopes in this variable is defined. 0 Indicates that this variable is used in the same function
        /// it is defined in. 1 Indicates that the variable is used in a closure of the function that defines it. And so on...
        /// </summary>
        public int FunctionalDepth;
    }

    /// <summary>
    /// An expression that represents a function call.
    /// </summary>
    public class FunctionCallExpression : Expression
    {
        public FunctionCallExpression(Expression Function, Expression Argument)
        {
            this.Function = Function;
            this.Argument = Argument;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return (this.Function.Evaluate(Stack) as FunctionValue).Call(this.Argument.Evaluate(Stack));
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            FunctionType functype = this.Function.GetType(Stack) as FunctionType;
            return functype.GetReturnType(Stack, this.Argument);
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return new FunctionCallExpression(this.Function.Substitute(Stack), this.Argument.Substitute(Stack));
        }

        public override void TypeCheck(VariableStack<Expression> Stack, 
            FunctionValue ConversionFactory,
            out Expression TypeSafeExpression, out Type Type)
        {
            Expression sfunction;
            Type possiblefunctiontype;
            this.Function.TypeCheck(Stack, ConversionFactory, out sfunction, out possiblefunctiontype);
            
            FunctionType functiontype = possiblefunctiontype as FunctionType;
            if(functiontype == null)
            {
                throw new NotCallableException(this);
            }

            Expression sarg;
            Type argtype;
            this.Argument.TypeCheck(Stack, ConversionFactory, out sarg, out argtype);

            // Check types
            FunctionValue conversion = Expression.GetConversion(ConversionFactory, argtype, functiontype.Argument);
            if(conversion != null)
            {
                // We can save some time if the conversion function is identity (there is no conversion, but types are compatiable).
                if (conversion != FunctionValue.Identity)
                {
                    TypeSafeExpression =
                        new FunctionCallExpression(sfunction,
                            new FunctionCallExpression(
                                Expression.Constant(new FunctionType(argtype, FunctionValue.Constant(functiontype.Argument)), conversion),
                                sarg));
                }
                else
                {
                    TypeSafeExpression = new FunctionCallExpression(sfunction, sarg);
                }
            }
            else
            {
                // Incompatiable types
                throw new TypeCheckException(this);
            }

            // Get return type
            Type = functiontype.GetReturnType(Stack, sarg);
        }

        /// <summary>
        /// The function that is called.
        /// </summary>
        public Expression Function;

        /// <summary>
        /// Expressions for the argument of the call.
        /// </summary>
        public Expression Argument;
    }

    /// <summary>
    /// An expression that represents a property of another expression. This expression can not be used directly for anything
    /// other than typechecking. An accessor can be used to get an individual form constructor for a variant type, or to get a property from
    /// a struct.
    /// </summary>
    public class AccessorExpression : Expression
    {
        public AccessorExpression(Expression Object, string Property)
        {
            this.Object = Object;
            this.Property = Property;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            throw new NotImplementedException();
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            throw new NotImplementedException();
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return new AccessorExpression(this.Object.Substitute(Stack), this.Property);
        }

        public override void TypeCheck(VariableStack<Expression> Stack,
            FunctionValue ConversionFactory,
            out Expression TypeSafeExpression, out Type Type)
        {
            Expression sobject;
            Type objecttype;
            this.Object.TypeCheck(Stack, ConversionFactory, out sobject, out objecttype);

            // Check if variant
            if (objecttype == Type.UniversalType)
            {
                VariantType vt = sobject.Substitute(Stack).Value as VariantType;
                if (vt != null)
                {
                    int index;
                    VariantForm vf;
                    if (vt.Lookup(this.Property, out vf, out index))
                    {
                        TypeSafeExpression = Expression.Constant(
                            Type = new FunctionType(vf.DataType, FunctionValue.Constant(vt)),
                            new VariantConstructor(index));
                        return;
                    }
                }
            }

            throw new AccessorFailException(this);
        }

        /// <summary>
        /// The object to "access".
        /// </summary>
        public Expression Object;

        /// <summary>
        /// The property to retreive from the object.
        /// </summary>
        public string Property;
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

    /// <summary>
    /// Input to the interpreter, containing information about the root scope and how to process certain expressions.
    /// </summary>
    public abstract class InterpreterInput
    {
        /// <summary>
        /// Gets the type integer literals are assigned with.
        /// </summary>
        public abstract Type IntegerLiteralType { get; }

        /// <summary>
        /// Gets the value for the specified integer literal.
        /// </summary>
        public abstract Value GetIntegerLiteral(long Value);

        /// <summary>
        /// The values in the root scope.
        /// </summary>
        public Dictionary<string, Datum> RootValues;
    }

    /// <summary>
    /// Interpreter functions.
    /// </summary>
    public static class Interpret
    {
        /// <summary>
        /// Evaluates a parsed expression using the default interpreter input.
        /// </summary>
        public static Datum Evaluate(Parser.Expression Expression)
        {
            return Evaluate(Expression, Default.Input);
        }

        /// <summary>
        /// Evaluates a parsed expression using the specified interpreter input.
        /// </summary>
        public static Datum Evaluate(Parser.Expression Expression, InterpreterInput Input)
        {
            // Assign all input variables to a position on the stack of the root scope. (Seperates names and values)
            int rootvars = Input.RootValues.Count;
            Dictionary<string, int> vars = new Dictionary<string, int>(rootvars);
            Value[] data = new Value[rootvars];
            Expression[] exps = new Expression[rootvars];
            int i = 0;
            foreach (var kvp in Input.RootValues)
            {
                data[i] = kvp.Value.Value;
                exps[i] = new ValueExpression(kvp.Value);
                vars[kvp.Key] = i;
                i++;
            }
            var datastack = new VariableStack<Value>(data);
            var expstack = new VariableStack<Expression>(exps);

            // Prepare
            Expression exp = Prepare(Expression, new Scope() { FunctionalDepth = 0, Variables = vars }, Input);
            Type exptype;
            exp.TypeCheck(expstack, Default.ConversionFactory, out exp, out exptype);

            // Evaluate
            return new Datum(exptype, exp.Evaluate(datastack));
        }

        /// <summary>
        /// Prepares a parsed expression for use.
        /// </summary>
        public static Expression Prepare(Parser.Expression Expression, Scope Scope, InterpreterInput Input)
        {
            // Function call
            Parser.FunctionCallExpression fce = Expression as Parser.FunctionCallExpression;
            if (fce != null)
            {
                Expression func = Prepare(fce.Function, Scope, Input);
                if (fce.Arguments.Count == 0)
                {
                    return new FunctionCallExpression(func, TupleExpression.Empty);
                }
                if (fce.Arguments.Count == 1)
                {
                    return new FunctionCallExpression(func, Prepare(fce.Arguments[0], Scope, Input));
                }
                Expression[] args = new Expression[fce.Arguments.Count];
                for (int t = 0; t < args.Length; t++)
                {
                    args[t] = Prepare(fce.Arguments[t], Scope, Input);
                }
                return new FunctionCallExpression(func, new TupleExpression(args));
            }

            // Variable
            Parser.VariableExpression ve = Expression as Parser.VariableExpression;
            if (ve != null)
            {
                return PrepareVariable(ve.Name, Scope);
            }

            // Integer liteal
            Parser.IntegerLiteralExpression ile = Expression as Parser.IntegerLiteralExpression;
            if (ile != null)
            {
                return new ValueExpression(Input.IntegerLiteralType, Input.GetIntegerLiteral(ile.Value));
            }

            // Accessor
            Parser.AccessorExpression ae = Expression as Parser.AccessorExpression;
            if (ae != null)
            {
                return new AccessorExpression(Prepare(ae.Object, Scope, Input), ae.Property);
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Prepares a variable expression based on its name and the scope it's used in. Returns null if the variable is not found.
        /// </summary>
        public static VariableExpression PrepareVariable(string Name, Scope Scope)
        {
            int depth;
            int index;
            if (Scope.LookupVariable(Name, out depth, out index))
            {
                return new VariableExpression(index, depth);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Very simply, evaluates an expression.
        /// </summary>
        public static Datum Evaluate(string Expression)
        {
            int lc;
            Parser.Expression exp;
            if (Parser.AcceptExpression(Expression, 0, out exp, out lc))
            {
                return Evaluate(exp);
            }
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// An exception indicating a type mismatch while type checking.
    /// </summary>
    public class TypeCheckException : Exception
    {
        public TypeCheckException(Expression Expression)
        {
            this.Expression = Expression;
        }

        /// <summary>
        /// The expression where the type mismatch occured.
        /// </summary>
        public Expression Expression;
    }

    /// <summary>
    /// An exception indicating that a type is not suitable for an accessor.
    /// </summary>
    public class AccessorFailException : TypeCheckException
    {
        public AccessorFailException(Expression Expression)
            : base(Expression)
        {
            this.Expression = Expression;
        }
    }

    /// <summary>
    /// An exception indicating that an object used as a function in a function call expression cannot be called.
    /// </summary>
    public class NotCallableException : TypeCheckException
    {
        public NotCallableException(Expression Expression)
            : base(Expression)
        {
            this.Expression = Expression;
        }
    }
}