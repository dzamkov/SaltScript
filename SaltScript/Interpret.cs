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

    }

    /// <summary>
    /// Represents a type for the script.
    /// </summary>
    public class Type : Value
    {
        /// <summary>
        /// The friendly (ish) name for this type.
        /// </summary>
        public virtual string Name
        {
            get
            {
                return this.ToString();
            }
        }

        /// <summary>
        /// Gets a friendly (ish) string form of a value of this type.
        /// </summary>
        public virtual string Display(Value Value)
        {
            return Value.ToString();
        }

        /// <summary>
        /// Converts a datum to a value of this type.
        /// </summary>
        public virtual bool Convert(Datum Datum, out Value Value)
        {
            if (Datum.Type == this)
            {
                Value = Datum.Value;
                return true;
            }
            else
            {
                Value = null;
                return false;
            }
        }

        /// <summary>
        /// All types are values with this type, including this type.
        /// </summary>
        public static readonly Type UniversalType = new _TypeType();

        private class _TypeType : Type
        {
            public override string Name
            {
                get
                {
                    return "type";
                }
            }

            public override string Display(Value Value)
            {
                return (Value as Type).Name;
            }

            public override bool Convert(Datum Datum, out Value Value)
            {
                if (Datum.Value is Type)
                {
                    Value = Datum.Value;
                    return true;
                }
                else
                {
                    Value = null;
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// The type for a function.
    /// </summary>
    public class FunctionType : Type
    {
        public FunctionType(Type[] Arguments, Expression ReturnType)
        {
            this.Arguments = Arguments;
            this.ReturnType = ReturnType;
        }

        /// <summary>
        /// Gets the return type given the arguments to the function.
        /// </summary>
        public Type GetReturnType(Value[] Arguments)
        {
            List<Datum> stack = new List<Datum>(Arguments.Length);
            for (int t = 0; t < Arguments.Length; t++)
            {
                stack.Add(new Datum(this.Arguments[t], Arguments[t]));
            }
            return this.ReturnType.Evaluate(stack).Value as Type;
        }

        /// <summary>
        /// An array of indicating the types of the arguments for the function.
        /// </summary>
        public Type[] Arguments;

        /// <summary>
        /// An expression that when evaluated, with the argument values, will get the return type.
        /// </summary>
        public Expression ReturnType;
    }

    /// <summary>
    /// A value that can be invoked/called/executed.
    /// </summary>
    public abstract class FunctionValue : Value
    {
        /// <summary>
        /// Calls the function with the specified arguments. Type checking must be performed before calling.
        /// </summary>
        public abstract Value Call(Value[] Arguments);

        /// <summary>
        /// Creates a function value from a native .net function.
        /// </summary>
        public static FunctionValue Create(FunctionHandler Function)
        {
            return new _NativeFunctionValue() { Function = Function };
        }

        private class _NativeFunctionValue : FunctionValue
        {
            public override Value Call(Value[] Arguments)
            {
                return this.Function(Arguments);
            }
            public FunctionHandler Function;
        }
    }

    /// <summary>
    /// The delegate type for .net functions that can act as SaltScript functions.
    /// </summary>
    public delegate Value FunctionHandler(Value[] Arguments);

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
    /// Represents an expression, which can be evaluated if all scope variables are known.
    /// </summary>
    public abstract class Expression
    {
        /// <summary>
        /// Evaluates the expression with the given immediate value stack.
        /// </summary>
        public abstract Datum Evaluate(List<Datum> Stack);

        /// <summary>
        /// Creates an expression that always evaluates to the same value.
        /// </summary>
        public static ValueExpression Constant(Type Type, Value Value)
        {
            return new ValueExpression(Type, Value);
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

        public override Datum Evaluate(List<Datum> Stack)
        {
            return this.Datum;
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
            this.StackPosition = StackPosition;
            this.FunctionalDepth = FunctionalDepth;
        }

        public override Datum Evaluate(List<Datum> Stack)
        {
            return Stack[this.StackPosition];
        }

        /// <summary>
        /// Relative position of the variable on the stack of the function that defines it.
        /// </summary>
        public int StackPosition;

        /// <summary>
        /// The amount of functional scopes in this variable is defined. 0 Indicates that this variable is used in the same function
        /// it is defined in. 1 Indicates that the variable is used in a closure of the function that defines it. And so on...
        /// </summary>
        /// <remarks>Variables can only be retreived from the stack if their functional depth is 0.</remarks>
        public int FunctionalDepth;
    }

    /// <summary>
    /// An expression that represents a function call.
    /// </summary>
    public class FunctionCallExpression : Expression
    {
        public FunctionCallExpression(Expression Function, Expression[] Arguments)
        {
            this.Function = Function;
            this.Arguments = Arguments;
        }

        public override Datum Evaluate(List<Datum> Stack)
        {
            Datum func = this.Function.Evaluate(Stack);
            Datum[] args = new Datum[this.Arguments.Length];
            for (int t = 0; t < this.Arguments.Length; t++)
            {
                args[t] = this.Arguments[t].Evaluate(Stack);
            }

            // Type safety check and call
            FunctionType functype = func.Type as FunctionType;
            if (functype != null)
            {
                if (args.Length == functype.Arguments.Length)
                {
                    bool fail = false;
                    Value[] vals = new Value[args.Length];
                    for (int t = 0; t < args.Length; t++)
                    {
                        Value val;
                        if (functype.Arguments[t].Convert(args[t], out val))
                        {
                            vals[t] = val;
                        }
                        else
                        {
                            fail = true;
                            break;
                        }
                    }

                    if (!fail)
                    {
                        return new Datum(functype.GetReturnType(vals), (func.Value as FunctionValue).Call(vals));
                    }
                }
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// The function that is called.
        /// </summary>
        public Expression Function;

        /// <summary>
        /// Expressions for the arguments of the call.
        /// </summary>
        public Expression[] Arguments;
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
            Dictionary<string, int> vars = new Dictionary<string, int>();
            List<Datum> stack = new List<Datum>();
            int i = 0;
            foreach (var kvp in Input.RootValues)
            {
                vars[kvp.Key] = i;
                stack.Add(kvp.Value);
                i++;
            }

            // Prepare
            Expression exp = Prepare(Expression, new Scope() { FunctionalDepth = 0, Variables = vars }, Input);

            // Evaluate
            return exp.Evaluate(stack);
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
                Expression[] args = new Expression[fce.Arguments.Count];
                for (int t = 0; t < args.Length; t++)
                {
                    args[t] = Prepare(fce.Arguments[t], Scope, Input);
                }
                Expression func = Prepare(fce.Function, Scope, Input);

                return new FunctionCallExpression(func, args);
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

            return null;
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
}