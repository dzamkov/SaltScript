using System;
using System.Collections.Generic;
using System.Text;

namespace SaltScript
{
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