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
                return new FunctionDefineExpression(Scope.NextFreeIndex, argtype, inner);
            }

            // Function type
            Parser.FunctionTypeExpression fte = Expression as Parser.FunctionTypeExpression;
            if (fte != null)
            {
                // Notice that this is almost exactly the same as a function definition? weird type system, eh?
                Expression argtype;
                Expression inner;
                _PrepareLambda(fte.ArgumentTypes, fte.ReturnType, Scope, Input, out argtype, out inner);
                return new FunctionDefineExpression(Scope.NextFreeIndex, argtype, inner);
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
                argloc + 1,
                Arguments.Count,
                Expression.Variable(argloc), 
                Prepare(Inner, nscope, Input));
        }

        /// <summary>
        /// Looks up or "dereferences" a variable with the given index in the specified stack.
        /// </summary>
        public static Expression Lookup(int Index, IVariableMap<Expression> Map)
        {
            Expression res = null;
            if (Map.Lookup(Index, ref res))
            {
                return res.Substitute(Map);
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
        public virtual Value Evaluate(IMutableVariableStack<Value> Stack)
        {
            return null;
        }

        /// <summary>
        /// Substitutes all variables in the expression with their corresponding expression in the specified map. This guarntees all variables in the stack's range
        /// to be removed (If all variables only reference variables below them in stack).
        /// </summary>
        public virtual Expression Substitute(IVariableMap<Expression> Map)
        {
            return this;
        }

        /// <summary>
        /// Substitutes a single variable in the expression.
        /// </summary>
        public Expression SubstituteOne(int Index, Expression Value)
        {
            return this.Substitute(new SingleVariableMap<Expression>(Index, Value));
        }

        /// <summary>
        /// Creates a type-safe version of the expression by using conversions where necessary. An exception will
        /// be thrown if this is not possible.
        /// </summary>
        public abstract void TypeCheck(
            IVariableStack<Expression> TypeStack, 
            IVariableStack<Expression> Stack,
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
        public static FunctionDefineExpression DefineFunction(int NextFreeIndex, Expression ArgumentType, Expression FunctionExpression)
        {
            return new FunctionDefineExpression(NextFreeIndex, ArgumentType, FunctionExpression);
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
        public static FunctionDefineExpression FunctionType(int NextFreeIndex, Expression ArgumentType, Expression ReturnType)
        {
            return new FunctionDefineExpression(NextFreeIndex, ArgumentType, ReturnType);
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
        public static TupleBreakExpression BreakTuple(int NextFreeIndex, int Size, Expression Tuple, Expression Inner)
        {
            return new TupleBreakExpression(NextFreeIndex, Size, Tuple, Inner);
        }

        /// <summary>
        /// Creates a
        /// </summary>
        public static VariableExpression Variable(int Index)
        {
            return new VariableExpression(Index);
        }

        /// <summary>
        /// Tries to gradually simplifies the expression. Can possibly access the map to dereference a variable.
        /// </summary>
        public virtual bool Reduce(IVariableMap<Expression> Map, ref Expression Reduced)
        {
            return false;
        }

        /// <summary>
        /// Gets if the two specified expressions are equivalent. This function reduces and subsitutes 
        /// given expressions and the stack as needed to get an accurate result.
        /// </summary>
        public static FuzzyBool Equivalent(ref Expression A, ref Expression B, IVariableStack<Expression> Stack)
        {
            if (A == B)
            {
                return FuzzyBool.True;
            }

            while (true)
            {
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
                            FuzzyBool pe = Equivalent(ref at.Parts[t], ref bt.Parts[t], Stack);
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
                            Expression.Equivalent(ref atb.SourceTuple, ref btb.SourceTuple, Stack),
                            Expression.Equivalent(ref atb.InnerExpression, ref btb.InnerExpression, Stack));
                    }
                }

                // Function definition equality
                FunctionDefineExpression afd = A as FunctionDefineExpression;
                if (afd != null)
                {
                    FunctionDefineExpression bfd = B as FunctionDefineExpression;
                    if (bfd != null)
                    {
                        if (afd.ArgumentIndex == bfd.ArgumentIndex)
                        {
                            var nstack = Stack.Cut(afd.ArgumentIndex).Append(new Expression[] { Expression.Variable(afd.ArgumentIndex) });
                            return FuzzyBoolLogic.And(
                                Expression.Equivalent(ref afd.ArgumentType, ref bfd.ArgumentType, nstack),
                                Expression.Equivalent(ref afd.Function, ref bfd.Function, nstack));
                        }
                    }
                }


                // Nothing yet? try reducing
                if (A.Reduce(Stack, ref A) | B.Reduce(Stack, ref B))
                {
                    continue;
                }
                else
                {
                    break;
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

        public override Value Evaluate(IMutableVariableStack<Value> Stack)
        {
            return this.Datum.Value;
        }

        public override Expression Substitute(IVariableMap<Expression> Map)
        {
            return this;
        }

        public override void TypeCheck(
            IVariableStack<Expression> TypeStack, 
            IVariableStack<Expression> Stack,
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

        public override bool Reduce(IVariableMap<Expression> Map, ref Expression Reduced)
        {
            // Only possible way to reduce a variable is to replace it with its value.
            Expression possible = null;
            if (Map.Lookup(this.Index, ref possible))
            {
                VariableExpression ve = possible as VariableExpression;
                if (ve != null)
                {
                    if (ve.Index == this.Index)
                    {
                        return false;
                    }
                }
                Reduced = possible;
                return true;
            }
            return false;
        }

        public override Value Evaluate(IMutableVariableStack<Value> Stack)
        {
            Value val = null;
            Stack.Lookup(this.Index, ref val);
            return val;
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

        public override Expression Substitute(IVariableMap<Expression> Map)
        {
            return Expression.Lookup(this.Index, Map);
        }

        public override void TypeCheck(
            IVariableStack<Expression> TypeStack,
            IVariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            TypeSafeExpression = this;
            Type = null;
            TypeStack.Lookup(this.Index, ref Type);
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

        public override bool Reduce(IVariableMap<Expression> Map, ref Expression Reduced)
        {
            // Beta reduction (substituting an argument in a function definition)
            FunctionDefineExpression fde = this.Function as FunctionDefineExpression;
            if (fde != null)
            {
                Reduced = fde.SubstituteCall(this.Argument);
                return true;
            }

            // Recursive reduction
            Expression fre = this.Function;
            Expression are = this.Argument;
            if (fre.Reduce(Map, ref fre) | are.Reduce(Map, ref are))
            {
                Reduced = new FunctionCallExpression(fre, are);
                return true;
            }

            return false;
        }

        public override Value Evaluate(IMutableVariableStack<Value> Stack)
        {
            return (this.Function.Evaluate(Stack) as FunctionValue).Call(this.Argument.Evaluate(Stack));
        }

        public override Expression Substitute(IVariableMap<Expression> Map)
        {
            return new FunctionCallExpression(this.Function.Substitute(Map), this.Argument.Substitute(Map));
        }
        
        public override void TypeCheck(
            IVariableStack<Expression> TypeStack,
            IVariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            int li = TypeStack.NextFreeIndex;

            Expression sfunc;
            Expression functype;
            this.Function.TypeCheck(TypeStack, Stack, out sfunc, out functype);

            FunctionDefineExpression fte;
            while ((fte = functype as FunctionDefineExpression) == null && functype.Reduce(Stack, ref functype)) ;
            if (fte == null)
            {
                throw new NotCallableException(this);
            }

            Expression sarg;
            Expression argtype;
            this.Argument.TypeCheck(TypeStack, Stack, out sarg, out argtype);

            FuzzyBool typeokay = Expression.Equivalent(ref argtype, ref fte.ArgumentType, Stack);
            if (typeokay != FuzzyBool.True)
            {
                throw new TypeCheckException(this);
            }

            TypeSafeExpression = new FunctionCallExpression(sfunc, sarg);
            Type = fte.Function.SubstituteOne(Stack.NextFreeIndex, sarg);
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
        public FunctionDefineExpression(int ArgumentIndex, Expression ArgumentType, Expression Function)
        {
            this.ArgumentIndex = ArgumentIndex;
            this.ArgumentType = ArgumentType;
            this.Function = Function;
        }

        /// <summary>
        /// Creates an expression that represents the return value of the function when supplied with an argument.
        /// </summary>
        public Expression SubstituteCall(Expression Argument)
        {
            return this.Function.SubstituteOne(this.ArgumentIndex, Argument).Compress(this.ArgumentIndex, 1);
        }

        public override Value Evaluate(IMutableVariableStack<Value> Stack)
        {
            return new ExpressionFunction((IMutableVariableStack<Value>)((IMutableVariableStack<Value>)Stack.Cut(this.ArgumentIndex)).Freeze, this.Function);
        }

        public override Expression Compress(int Start, int Amount)
        {
            return new FunctionDefineExpression(
                this.ArgumentIndex - Amount,
                this.ArgumentType.Compress(Start, Amount),
                this.Function.Compress(Start, Amount));
        }

        public override Expression Substitute(IVariableMap<Expression> Map)
        {
            return new FunctionDefineExpression(
                this.ArgumentIndex,
                this.ArgumentType.Substitute(Map),
                this.Function.Substitute(new SubsetMap<Expression>(0, this.ArgumentIndex, Map)));
        }

        public override void TypeCheck(
            IVariableStack<Expression> TypeStack,
            IVariableStack<Expression> Stack,
            out Expression TypeSafeExpression, out Expression Type)
        {
            Expression sifunc;
            Expression itype;
            this.Function.TypeCheck(
                TypeStack.Cut(this.ArgumentIndex).Append(new Expression[] { this.ArgumentType }), 
                Stack.Cut(this.ArgumentIndex).Append(new Expression[] { Expression.Variable(Stack.NextFreeIndex) }), 
                out sifunc, out itype);
            Type = Expression.FunctionType(this.ArgumentIndex, this.ArgumentType, itype);
            TypeSafeExpression = new FunctionDefineExpression(this.ArgumentIndex, this.ArgumentType, sifunc);
        }

        /// <summary>
        /// The index of the argument given to Function. The function may access any variable with an index lower than this.
        /// </summary>
        public int ArgumentIndex;

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
            IVariableStack<Expression> TypeStack,
            IVariableStack<Expression> Stack,
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