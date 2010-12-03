﻿using System;
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
        public virtual Value Evaluate(VariableStack<Value> Stack)
        {
            return null;
        }

        /// <summary>
        /// Substitutes all variables in the expression with their corresponding expression in the specified stack. Note that this
        /// does not guarantee all variables to be removed (some of the substituted expressions may contain variables themselves). The stack may omit
        /// some variables, in which case, they remain unchanged.
        /// </summary>
        public virtual Expression Substitute(VariableStack<Expression> Variables)
        {
            return this;
        }

        /// <summary>
        /// Creates a type-safe version of the expression by using conversions where necessary. An exception will
        /// be thrown if this is not possible.
        /// </summary>
        public abstract void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type);

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
        /// Creates an expression that acts as a function.
        /// </summary>
        public static FunctionDefineExpression DefineFunction(Expression ArgumentType, Expression FunctionExpression)
        {
            return new FunctionDefineExpression(ArgumentType, FunctionExpression);
        }

        /// <summary>
        /// Creates an expression that always evaluates to the same value.
        /// </summary>
        public static ValueExpression Constant(Expression Type, Value Value)
        {
            return new ValueExpression(Value, Type);
        }

        /// <summary>
        /// Creates an expression that always evaluates to the same value.
        /// </summary>
        public static ValueExpression Constant(Datum Datum)
        {
            return new ValueExpression(Datum);
        }

        /// <summary>
        /// Creates an expression for a function type, given the argument type and a function that, when given the argument, will return
        /// the function's return type.
        /// </summary>
        public static FunctionTypeExpression FunctionType(Expression ArgumentType, Expression ReturnTypeFunction)
        {
            return new FunctionTypeExpression(ArgumentType, ReturnTypeFunction);
        }

        /// <summary>
        /// Creates an expression for a function type, given the argument type and the return type.
        /// </summary>
        public static FunctionTypeExpression SimpleFunctionType(Expression ArgumentType, Expression ReturnType)
        {
            return FunctionType(ArgumentType, DefineFunction(ArgumentType, ReturnType));
        }

        /// <summary>
        /// Simplifies the expression, given the last variable index in the current scope. This is should only
        /// be used on type-checked expressions.
        /// </summary>
        public virtual Expression Reduce(VariableIndex LastIndex)
        {
            return this;
        }

        /// <summary>
        /// Gets if the two specified reduced expressions are equivalent.
        /// </summary>
        public static bool Equivalent(Expression A, Expression B)
        {
            if (A == B)
            {
                return true;
            }

            // Variable equality
            VariableExpression va = A as VariableExpression;
            if (va != null)
            {
                VariableExpression ba = B as VariableExpression;
                if (ba != null)
                {
                    return va.Index.FunctionalDepth == ba.Index.FunctionalDepth && va.Index.StackIndex == ba.Index.StackIndex;
                }
            }

            // Tuple equality
            TupleExpression at = A as TupleExpression;
            if (at != null)
            {
                TupleExpression bt = B as TupleExpression;
                if (bt != null)
                {
                    if (at.Parts.Length != bt.Parts.Length)
                    {
                        return false;
                    }
                    for (int t = 0; t < at.Parts.Length; t++)
                    {
                        if (!Equivalent(at.Parts[t], bt.Parts[t]))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates an expression that produces a tuple with one item.
        /// </summary>
        public static TupleExpression Tuple(Expression A)
        {
            return new TupleExpression(new Expression[] { A });
        }

        /// <summary>
        /// Creates an expression that produces a tuple with two items.
        /// </summary>
        public static TupleExpression Tuple(Expression A, Expression B)
        {
            return new TupleExpression(new Expression[] { A, B });
        }

        /// <summary>
        /// Creates an expression that produces a tuple with three items.
        /// </summary>
        public static TupleExpression Tuple(Expression A, Expression B, Expression C)
        {
            return new TupleExpression(new Expression[] { A, B, C });
        }

        /// <summary>
        /// Creates an expression with a varying amount of items.
        /// </summary>
        public static TupleExpression Tuple(Expression[] Items)
        {
            return new TupleExpression(Items);
        }

        /// <summary>
        /// Creates a
        /// </summary>
        public static VariableExpression Variable(VariableIndex Index)
        {
            return new VariableExpression(Index);
        }

        /// <summary>
        /// Gets an expression that evaluates to the universal type, the type of all other types, including itself.
        /// </summary>
        public static readonly Expression UniversalType = new _UniversalType();

        private class _UniversalType : Expression
        {
            public override void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type)
            {
                TypeSafeExpression = this;
                Type = this;
            }
        }
    }

    /// <summary>
    /// An expression that represents an immutable value of a certain type.
    /// </summary>
    public class ValueExpression : Expression
    {
        public ValueExpression(Datum Datum)
        {
            this.Datum = Datum;
        }

        public ValueExpression(Value Value, Expression Type)
        {
            this.Datum = new Datum(Value, Type);
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return this.Datum.Value;
        }

        public override Expression Substitute(VariableStack<Expression> Variables)
        {
            return this;
        }

        public override void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type)
        {
            TypeSafeExpression = this;
            Type = this.Datum.Type;
        }

        /// <summary>
        /// The datum that represents gives the type and value of this expression.
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

        public override Expression Substitute(VariableStack<Expression> Variables)
        {
            Expression e;
            if (Variables.Lookup(this.Index, out e))
            {
                return e;
            }
            else
            {
                return this;
            }
        }

        public override void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type)
        {
            TypeSafeExpression = this;
            Type = TypeStack.Lookup(this.Index);
        }

        public VariableIndex Index;
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

        public override Expression Reduce(VariableIndex LastIndex)
        {
            Expression freduce = this.Function.Reduce(LastIndex);
            Expression areduce = this.Argument.Reduce(LastIndex);

            // If the function is a lambda, call it.
            FunctionDefineExpression fde = freduce as FunctionDefineExpression;
            if (fde != null)
            {
                return fde.Function.Substitute(new VariableStack<Expression>(LastIndex.FunctionalDepth + 1, new Expression[] { areduce })).Reduce(LastIndex);
            }

            return new FunctionCallExpression(freduce, areduce);
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return (this.Function.Evaluate(Stack) as FunctionValue).Call(this.Argument.Evaluate(Stack));
        }

        public override Expression Substitute(VariableStack<Expression> Variables)
        {
            return new FunctionCallExpression(this.Function.Substitute(Variables), this.Argument.Substitute(Variables));
        }

        public override void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type)
        {
            VariableIndex li = TypeStack.LastIndex;

            Expression sfunc;
            Expression functype;
            this.Function.TypeCheck(TypeStack, out sfunc, out functype);

            functype = functype.Reduce(li);
            FunctionTypeExpression fte = functype as FunctionTypeExpression;
            if (fte == null)
            {
                throw new NotCallableException(this);
            }

            Expression sarg;
            Expression argtype;
            this.Argument.TypeCheck(TypeStack, out sarg, out argtype);

            if (!Expression.Equivalent(argtype.Reduce(li), fte.ArgumentType))
            {
                throw new TypeCheckException(this);
            }

            TypeSafeExpression = new FunctionCallExpression(sfunc, sarg);
            Type = new FunctionCallExpression(fte.ReturnTypeFunction, sarg);
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
    /// An expression that creates a function type.
    /// </summary>
    public class FunctionTypeExpression : Expression
    {
        public FunctionTypeExpression(Expression ArgumentType, Expression ReturnTypeFunction)
        {
            this.ArgumentType = ArgumentType;
            this.ReturnTypeFunction = ReturnTypeFunction;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return null;
        }

        public override Expression Substitute(VariableStack<Expression> Variables)
        {
            return new FunctionTypeExpression(this.ArgumentType.Substitute(Variables), this.ReturnTypeFunction.Substitute(Variables));
        }

        public override void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The type of the argument to the function.
        /// </summary>
        public Expression ArgumentType;

        /// <summary>
        /// A function, that when called with the argument value, will get the return type of the function.
        /// </summary>
        public Expression ReturnTypeFunction;
    }

    /// <summary>
    /// An expression that defines a function that takes an argument and returns a result by evaluating another expression.
    /// </summary>
    public class FunctionDefineExpression : Expression
    {
        public FunctionDefineExpression(Expression ArgumentType, Expression Function)
        {
            this.Function = Function;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return new ExpressionFunction(Stack, this.Function);
        }

        public override void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type)
        {
            Expression sifunc;
            Expression itype;
            this.Function.TypeCheck(TypeStack.AppendHigherFunction(new Expression[] { this.ArgumentType }), out sifunc, out itype);
            Type = new FunctionTypeExpression(this.ArgumentType, itype);
            TypeSafeExpression = new FunctionDefineExpression(this.ArgumentType, sifunc);
        }

        /// <summary>
        /// The type of the argument to the created function.
        /// </summary>
        public Expression ArgumentType;

        /// <summary>
        /// The expression that is evaluated to get the function's result. This expression can access the argument by getting
        /// the first variable on the stack with a functional depth one higher than the functional depth of the scope this function
        /// is defined in.
        /// </summary>
        public Expression Function;
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

        public override void TypeCheck(VariableStack<Expression> TypeStack, out Expression TypeSafeExpression, out Expression Type)
        {
            throw new NotImplementedException();
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
}