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
        /// Prepares a parsed expression for use.
        /// </summary>
        public static Expression Prepare(Parser.Expression Expression, Scope Scope, ProgramInput Input)
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

            // Procedure
            Parser.ProcedureExpression pe = Expression as Parser.ProcedureExpression;
            if (pe != null)
            {
                return ProcedureExpression.Prepare(pe, Scope, Input);
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
                return new ValueExpression(Input.GetIntegerLiteral(ile.Value), Input.IntegerLiteralType);
            }

            // Accessor
            Parser.AccessorExpression ae = Expression as Parser.AccessorExpression;
            if (ae != null)
            {
                return new AccessorExpression(Prepare(ae.Object, Scope, Input), ae.Property);
            }

            // Function definition
            Parser.FunctionDefineExpression fde = Expression as Parser.FunctionDefineExpression;
            if (fde != null)
            {
                Expression argtype;
                Expression inner;
                _PrepareLambda(fde.Arguments, fde.Definition, Scope, Input, out argtype, out inner);
                return new FunctionDefineExpression(argtype, inner);
            }

            // Function type
            Parser.FunctionTypeExpression fte = Expression as Parser.FunctionTypeExpression;
            if (fte != null)
            {
                // Notice that this is almost exactly the same as a function definition? weird type system, eh?
                Expression argtype;
                Expression inner;
                _PrepareLambda(fte.ArgumentTypes, fte.ReturnType, Scope, Input, out argtype, out inner);
                return new FunctionDefineExpression(argtype, inner);
            }

            throw new NotImplementedException();
        }

        private static void _PrepareLambda(
            List<KeyValuePair<Parser.Expression, string>> Arguments,
            Parser.Expression Inner, Scope Scope, ProgramInput Input,
            out Expression ArgumentType, out Expression PreparedInner)
        {
            // 0 arg function
            if (Arguments.Count == 0)
            {
                ArgumentType = TupleExpression.Empty;
                PreparedInner = Prepare(Inner, Scope, Input);
                return;
            }

            // 1 arg function
            Dictionary<string, int> vars;
            int argloc = Scope.NextFreeIndex;
            Scope nscope = new Scope()
            {
                NextFreeIndex = argloc + 1,
                Variables = vars = new Dictionary<string, int>(),
                Parent = Scope
            };
            if (Arguments.Count == 1)
            {
                var kvp = Arguments[0];
                if (kvp.Value != null)
                {
                    vars.Add(kvp.Value, argloc);
                }
                ArgumentType = Prepare(kvp.Key, Scope, Input);
                PreparedInner = Prepare(Inner, nscope, Input);
                return;
            }

            // 2+ arg function
            Expression[] types = new Expression[Arguments.Count];
            for (int t = 0; t < types.Length; t++)
            {
                types[t] = Prepare(Arguments[t].Key, Scope, Input);
            }

            nscope.NextFreeIndex += types.Length;
            for (int t = 0; t < Arguments.Count; t++)
            {
                string argname = Arguments[t].Value;
                if (argname != null)
                {
                    vars.Add(argname, t + argloc + 1);
                }
            }
            ArgumentType = Expression.Tuple(types);
            PreparedInner = Expression.BreakTuple(
                Arguments.Count,
                Expression.Variable(argloc), 
                Prepare(Inner, nscope, Input));
        }

        /// <summary>
        /// Looks up or "dereferences" a variable with the given index in the specified stack.
        /// </summary>
        public static Expression Lookup(int Index, VariableStack<Expression> Stack)
        {
            Expression res;
            if (Stack.Lookup(Index, out res))
            {
                VariableExpression ve = res as VariableExpression;
                if (ve != null)
                {
                    if (ve.Index == Index)
                    {
                        return ve;
                    }
                }
                
                // Substitute again until we reach a fixed point.
                return res.Substitute(Stack);
            }
            else
            {
                return Expression.Variable(Index);
            }
        }

        /// <summary>
        /// Prepares a variable expression based on its name and the scope it's used in. Returns null if the variable is not found.
        /// </summary>
        public static VariableExpression PrepareVariable(string Name, Scope Scope)
        {
            int index;
            if (Scope.LookupVariable(Name, out index))
            {
                return new VariableExpression(index);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Compresses the scope size of expression by removing the specified variables. If it is not possible to remove the variables because
        /// the expression depends on them, this returns null.
        /// </summary>
        public virtual Expression Compress(int Start, int Amount)
        {
            return null;
        }

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
        public virtual Expression Substitute(VariableStack<Expression> Stack)
        {
            return this;
        }

        /// <summary>
        /// Substitutes a single variable in the expression.
        /// </summary>
        public Expression SubstituteOne(int Index, Expression Value)
        {
            return this.Substitute(VariableStack<Expression>.Empty(Index).Append(new Expression[] { Value }));
        }

        /// <summary>
        /// Creates a type-safe version of the expression by using conversions where necessary. An exception will
        /// be thrown if this is not possible.
        /// </summary>
        public abstract void TypeCheck(
            VariableStack<Expression> TypeStack, 
            VariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type);

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
        /// Creates an expression for a function type, given the argument type and the return type(which has access to the argument in its current scope).
        /// </summary>
        public static FunctionDefineExpression FunctionType(Expression ArgumentType, Expression ReturnType)
        {
            return new FunctionDefineExpression(ArgumentType, ReturnType);
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
        /// Creates an expression that causes the parts in tuple to be used in the stack of the inner expression.
        /// </summary>
        public static TupleBreakExpression BreakTuple(int Size, Expression Tuple, Expression Inner)
        {
            return new TupleBreakExpression(Size, Tuple, Inner);
        }

        /// <summary>
        /// Creates a
        /// </summary>
        public static VariableExpression Variable(int Index)
        {
            return new VariableExpression(Index);
        }

        /// <summary>
        /// Simplifies the expression, given the last variable index in the current scope. This is should only
        /// be used on type-checked expressions.
        /// </summary>
        public virtual Expression Reduce(int NextIndex)
        {
            return this;
        }

        /// <summary>
        /// Gets if the two specified reduced expressions are equivalent.
        /// </summary>
        public static FuzzyBool Equivalent(Expression A, Expression B, VariableStack<Expression> Stack)
        {
            if (A == B)
            {
                return FuzzyBool.True;
            }

            // Variable equality
            VariableExpression va = A as VariableExpression;
            if (va != null)
            {
                VariableExpression vb = B as VariableExpression;
                if (vb != null)
                {
                    if (va.Index == vb.Index)
                    {
                        return FuzzyBool.True;
                    }
                    else
                    {
                        // Maybe a dereference is needed?
                        bool vachange = false;
                        bool vbchange = false;
                        Expression na;
                        Expression nb;
                        if (Stack.Lookup(va.Index, out na))
                        {
                            VariableExpression temp = na as VariableExpression;
                            if (temp == null || temp.Index != va.Index)
                            {
                                vachange = true;
                            }
                        }
                        else
                        {
                            na = va;
                        }
                        if (Stack.Lookup(vb.Index, out nb))
                        {
                            VariableExpression temp = nb as VariableExpression;
                            if (temp == null || temp.Index != vb.Index)
                            {
                                vbchange = true;
                            }
                        }
                        else
                        {
                            nb = vb;
                        }
                        if (vachange || vbchange)
                        {
                            return Equivalent(na, nb, Stack);
                        }
                        else
                        {
                            return FuzzyBool.False;
                        }
                    }
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
                        return FuzzyBool.False;
                    }
                    for (int t = 0; t < at.Parts.Length; t++)
                    {
                        FuzzyBool pe = Equivalent(at.Parts[t], bt.Parts[t], Stack);
                        if (pe == FuzzyBool.False)
                        {
                            return FuzzyBool.False;
                        }
                        if (pe == FuzzyBool.Undetermined)
                        {
                            return FuzzyBool.Undetermined;
                        }
                    }
                    return FuzzyBool.True;
                }
            }

            // Tuple break
            TupleBreakExpression atb = A as TupleBreakExpression;
            if (atb != null)
            {
                TupleBreakExpression btb = B as TupleBreakExpression;
                if (btb != null)
                {
                    return FuzzyBoolLogic.And(
                        Expression.Equivalent(atb.SourceTuple, btb.SourceTuple, Stack),
                        Expression.Equivalent(atb.InnerExpression, btb.InnerExpression, Stack));
                }
            }

            // Function definition equality
            FunctionDefineExpression afd = A as FunctionDefineExpression;
            if (afd != null)
            {
                FunctionDefineExpression bfd = B as FunctionDefineExpression;
                if (bfd != null)
                {
                    return FuzzyBoolLogic.And(
                        Expression.Equivalent(afd.ArgumentType, bfd.ArgumentType, Stack),
                        Expression.Equivalent(afd.Function, bfd.Function, Stack));
                }
            }

            return FuzzyBool.Undetermined;
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

        public override void TypeCheck(
            VariableStack<Expression> TypeStack, 
            VariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
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
        public VariableExpression(int Index)
        {
            this.Index = Index;
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return Stack.Lookup(this.Index);
        }

        public override Expression Compress(int Start, int Amount)
        {
            if (Start + Amount <= this.Index)
            {
                return new VariableExpression(this.Index - Amount);
            }
            if (Start > this.Index)
            {
                return this;
            }
            return null;
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return Expression.Lookup(this.Index, Stack);
        }

        public override void TypeCheck(
            VariableStack<Expression> TypeStack,
            VariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            TypeSafeExpression = this;
            Type = TypeStack.Lookup(this.Index);
        }

        public int Index;
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

        public override Expression Reduce(int LastIndex)
        {
            Expression freduce = this.Function.Reduce(LastIndex);
            Expression areduce = this.Argument.Reduce(LastIndex);

            // If the function is a lambda, call it.
            FunctionDefineExpression fde = freduce as FunctionDefineExpression;
            if (fde != null)
            {
                return fde.Function.Substitute(new VariableStack<Expression>(LastIndex + 1, new Expression[] { areduce })).Reduce(LastIndex);
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

        public override void TypeCheck(
            VariableStack<Expression> TypeStack,
            VariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            int li = TypeStack.NextIndex;

            Expression sfunc;
            Expression functype;
            this.Function.TypeCheck(TypeStack, Stack, out sfunc, out functype);

            functype = functype.Reduce(li);
            FunctionDefineExpression fte = functype as FunctionDefineExpression;
            if (fte == null)
            {
                throw new NotCallableException(this);
            }

            Expression sarg;
            Expression argtype;
            this.Argument.TypeCheck(TypeStack, Stack, out sarg, out argtype);

            FuzzyBool typeokay = Expression.Equivalent(argtype.Reduce(li), fte.ArgumentType, Stack);
            if (typeokay != FuzzyBool.True)
            {
                throw new TypeCheckException(this);
            }

            TypeSafeExpression = new FunctionCallExpression(sfunc, sarg);
            Type = fte.Function.SubstituteOne(Stack.NextIndex, sarg);
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
    /// An expression that defines a function that takes an argument and returns a result by evaluating another expression. Can be used to indicate
    /// a function type where the argument type is the type of the argument in a function type, and the function, when evaluated with the argument, will
    /// get the return type of the function type.
    /// </summary>
    public class FunctionDefineExpression : Expression
    {
        public FunctionDefineExpression(Expression ArgumentType, Expression Function)
        {
            this.ArgumentType = ArgumentType;
            this.Function = Function;
        }

        public override Expression Reduce(int NextIndex)
        {
            return new FunctionDefineExpression(this.ArgumentType.Reduce(NextIndex), this.Function.Reduce(NextIndex + 1));
        }

        public override Value Evaluate(VariableStack<Value> Stack)
        {
            return new ExpressionFunction(Stack, this.Function);
        }

        public override Expression Substitute(VariableStack<Expression> Stack)
        {
            return new FunctionDefineExpression(
                this.ArgumentType.Substitute(Stack),
                this.Function.Substitute(
                    Stack.Append(new Expression[] { Expression.Variable(Stack.NextIndex) })));
        }

        public override void TypeCheck(
            VariableStack<Expression> TypeStack,
            VariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            Expression sifunc;
            Expression itype;
            this.Function.TypeCheck(
                TypeStack.Append(new Expression[] { this.ArgumentType }), 
                Stack.Append(new Expression[] { Expression.Variable(Stack.NextIndex) }), 
                out sifunc, out itype);
            Type = Expression.FunctionType(this.ArgumentType, itype);
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

        public override void TypeCheck(
            VariableStack<Expression> TypeStack,
            VariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
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
}