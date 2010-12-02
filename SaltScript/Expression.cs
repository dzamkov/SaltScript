using System;
using System.Collections.Generic;

namespace SaltScript
{
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
        public VariableExpression(VariableIndex Index)
        {
            this.Index = Index;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return Stack.Lookup(this.Index);
        }

        public override Type GetType(VariableStack<Expression> Stack)
        {
            return Stack.Lookup(this.Index).Type;
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return Stack.Lookup(this.Index);
        }

        public VariableIndex Index;
    }

    /// <summary>
    /// Identifies a variable on the stack.
    /// </summary>
    public struct VariableIndex
    {
        public VariableIndex(int StackIndex, int FunctionalDepth)
        {
            this.StackIndex = StackIndex;
            this.FunctionalDepth = FunctionalDepth;
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
            if (functiontype == null)
            {
                throw new NotCallableException(this);
            }

            Expression sarg;
            Type argtype;
            this.Argument.TypeCheck(Stack, ConversionFactory, out sarg, out argtype);

            // Check types
            FunctionValue conversion = Expression.GetConversion(ConversionFactory, argtype, functiontype.Argument);
            if (conversion != null)
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
}