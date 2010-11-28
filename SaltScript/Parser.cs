using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaltScript
{
    /// <summary>
    /// Parser functions.
    /// </summary>
    public static class Parser
    {
        /// <summary>
        /// Gets if the specified character is valid in a word (variable, constant).
        /// </summary>
        public static bool ValidWordChar(char Character)
        {
            int ascii = (int)Character;
            if (ascii >= 94 && ascii <= 122) return true;  // ^ _ ` Lowercase
            if (ascii >= 65 && ascii <= 90) return true; // Capitals
            if (ascii >= 47 && ascii <= 58) return true; // / Numerals :
            if (ascii == 33) return true; // !
            if (ascii >= 35 && ascii <= 39) return true; // # $ % & '
            if (ascii >= 42 && ascii <= 43) return true; // * +
            if (ascii == 45) return true; // -
            if (ascii == 61) return true; // =
            if (ascii == 63) return true; // ?
            if (ascii == 126) return true; // ~
            return false;
        }

        /// <summary>
        /// Gets if the specified character is a numeral.
        /// </summary>
        public static bool ValidNumeral(char Character)
        {
            return char.IsNumber(Character);
        }

        /// <summary>
        /// Gets if the specified string (with one or more character) is a word.
        /// </summary>
        public static bool ValidWord(string Word)
        {
            bool val = true;
            for (int t = 1; t < Word.Length; t++)
            {
                if (!ValidWordChar(Word[t]))
                {
                    val = false;
                    break;
                }
            }
            return val && ValidWordChar(Word[0]) && !ValidNumeral(Word[0]);
        }

        /// <summary>
        /// Gets if the specified word is a valid variable name.
        /// </summary>
        public static bool ValidVariable(string Word)
        {
            if (Word == "const") return false;
            if (Word == "function") return false;
            if (Word == "var") return false;
            if (Word == "if") return false;
            if (Word == "else") return false;
            if (Word == "while") return false;
            if (Word == "for") return false;
            if (Word == "return") return false;
            return true;
        }

        /// <summary>
        /// Greedily parses a word, or returns null if there is no word at the specified position.
        /// </summary>
        public static bool AcceptWord(string Text, int Start, out string Word, out int LastChar)
        {
            bool first = true;
            int c = Start;
            while (c < Text.Length)
            {
                char ch = Text[c];
                if (first)
                {
                    first = false;
                    if (ValidNumeral(ch) || !ValidWordChar(ch))
                    {
                        Word = null;
                        LastChar = c;
                        return false;
                    }
                }
                else
                {
                    if (!ValidWordChar(ch))
                    {
                        Word = Text.Substring(Start, c - Start);
                        LastChar = c;
                        return true;
                    }
                }
                c++;
            }
            Word = Text.Substring(Start, c - Start);
            LastChar = c;
            return true;
        }

        /// <summary>
        /// Parses the specified target string in the text.
        /// </summary>
        public static bool AcceptString(string Text, int Start, string Target, out int LastChar)
        {
            if (Text.Length - Start < Target.Length)
            {
                LastChar = Start;
                return false;
            }
            bool match = true;
            for (int c = 0; c < Target.Length; c++)
            {
                if (Target[c] != Text[Start])
                {
                    match = false;
                    break;
                }
                Start++;
            }
            if (match)
            {
                LastChar = Start;
                return true;
            }
            else
            {
                LastChar = Start;
                return false;
            }
        }


        /// <summary>
        /// Parses whitespace. Returns the length of the parsed whitespace in characters.
        /// </summary>
        public static int AcceptWhitespace(string Text, int Start, out int LastChar)
        {
            int c = Start;
            int nc = 0;
            bool singlelinecomment = false;
            bool multiplelinecomment = false;
            while (c < Text.Length)
            {
                char ch = Text[c];
                if (ch == ' ' || ch == '\t')
                {
                    c++;
                    continue;
                }
                if (ch == '\n' || ch == '\r')
                {
                    singlelinecomment = false;
                    c++;
                    continue;
                }
                if (multiplelinecomment && AcceptString(Text, c, "*/", out nc))
                {
                    multiplelinecomment = false;
                    c = nc;
                    continue;
                }
                if (!singlelinecomment && !multiplelinecomment)
                {
                    if (AcceptString(Text, c, "//", out nc))
                    {
                        singlelinecomment = true;
                        c = nc;
                        continue;
                    }
                    if (AcceptString(Text, c, "/*", out nc))
                    {
                        multiplelinecomment = true;
                        c = nc;
                        continue;
                    }
                    LastChar = c;
                    return LastChar - Start;
                }
                c++;
            }
            LastChar = c;
            return LastChar - Start;
        }

        /// <summary>
        /// Checks if the specified name corresponds to an operator, and returns it if so.
        /// </summary>
        public static bool LookupOperator(string Name, out Operator Operator)
        {
            KeyValuePair<int, bool> opinfo;
            if (_Operators.TryGetValue(Name, out opinfo))
            {
                Operator = new Operator()
                {
                    Name = Name,
                    Precedence = opinfo.Key,
                    LeftAssociative = opinfo.Value
                };
                return true;
            }
            else
            {
                Operator = new Operator();
                return false;
            }
        }

        static Parser()
        {
            _Operators = new Dictionary<string, KeyValuePair<int, bool>>();
            _Operators.Add("+", new KeyValuePair<int, bool>(0, true));
            _Operators.Add("-", new KeyValuePair<int, bool>(0, true));
            _Operators.Add("*", new KeyValuePair<int, bool>(1, true));
            _Operators.Add("/", new KeyValuePair<int, bool>(1, true));
        }

        private static readonly Dictionary<string, KeyValuePair<int, bool>> _Operators;

        /// <summary>
        /// Information about an operator.
        /// </summary>
        public struct Operator
        {
            /// <summary>
            /// The name of the operator (how it appears).
            /// </summary>
            public string Name;

            /// <summary>
            /// How strongly the operator is bound. Higher numbers indicate stronger binding.
            /// </summary>
            public int Precedence;

            /// <summary>
            /// True if the operator is left associative. If two operators have the same precedence and are
            /// left associative, the first one binds stronger.
            /// </summary>
            public bool LeftAssociative;
        }

        /// <summary>
        /// Parses an operator.
        /// </summary>
        public static bool AcceptOperator(string Text, int Start, out Operator Operator, out int LastChar)
        {
            string name;
            if (AcceptWord(Text, Start, out name, out LastChar))
            {
                if (LookupOperator(name, out Operator))
                {
                    return true;
                }
            }

            Operator = new Operator();
            return false;
        }

        /// <summary>
        /// Parses an integer literal.
        /// </summary>
        public static bool AcceptIntegerLiteral(string Text, int Start, out long Value, out int LastChar)
        {
            List<char> numchars = new List<char>();
            LastChar = Start;
            while (LastChar < Text.Length)
            {
                char ch = Text[LastChar];
                if (ValidNumeral(ch))
                {
                    numchars.Add(ch);
                    LastChar++;
                }
                else
                {
                    break;
                }
            }
            if (numchars.Count > 0)
            {
                Value = long.Parse(new string(numchars.ToArray()), System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            else
            {
                Value = 0;
                return false;
            }
        }

        /// <summary>
        /// Parses a simple expression (no function calls on top-level expression.
        /// </summary>
        public static bool AcceptAtom(string Text, int Start, out Expression Expression, out int LastChar)
        {
            // Procedure
            if (AcceptString(Text, Start, "{", out LastChar))
            {
                ProcedureExpression procedure;
                AcceptWhitespace(Text, LastChar, out LastChar);
                if (AcceptProcedure(Text, LastChar, out procedure, out LastChar))
                {
                    AcceptWhitespace(Text, LastChar, out LastChar);
                    if (AcceptString(Text, LastChar, "}", out LastChar))
                    {
                        Expression = procedure;
                        return true;
                    }
                }
            }

            // Variable
            Operator op;
            string varname;
            if (AcceptWord(Text, Start, out varname, out LastChar) && ValidVariable(varname) && !LookupOperator(varname, out op))
            {
                Expression = new VariableExpression(varname);
                return true;
            }

            // Parentheses
            if (AcceptString(Text, Start, "(", out LastChar))
            {
                // Operator
                if (AcceptWord(Text, LastChar, out varname, out LastChar) && LookupOperator(varname, out op))
                {
                    if (AcceptString(Text, LastChar, ")", out LastChar))
                    {
                        Expression = new VariableExpression(varname);
                        return true;
                    }
                }

                Expression exp;
                AcceptWhitespace(Text, LastChar, out LastChar);
                if (AcceptExpression(Text, LastChar, out exp, out LastChar))
                {
                    AcceptWhitespace(Text, LastChar, out LastChar);
                    if (AcceptString(Text, LastChar, ")", out LastChar))
                    {
                        Expression = exp;
                        return true;
                    }
                }
            }

            // Integer literal
            long val;
            if (AcceptIntegerLiteral(Text, Start, out val, out LastChar))
            {
                Expression = new IntegerLiteralExpression(val);
                return true;
            }


            Expression = null;
            return false;
        }

        /// <summary>
        /// Accepts a list of comma-delimited expressions.
        /// </summary>
        public static void AcceptExpressions(string Text, int Start, out List<Expression> Expressions, out int LastChar)
        {
            Expressions = new List<Expression>();
            int nc = LastChar = Start;
            while (true)
            {
                Expression exp;
                if (AcceptExpression(Text, nc, out exp, out nc))
                {
                    LastChar = nc;
                    Expressions.Add(exp);
                    AcceptWhitespace(Text, nc, out nc);
                    if (AcceptString(Text, nc, ",", out nc))
                    {
                        AcceptWhitespace(Text, nc, out nc);
                        continue;
                    }
                }
                break;
            }
        }

        /// <summary>
        /// Parses an expression that can not be broken apart with an operator.
        /// </summary>
        public static bool AcceptTightExpression(string Text, int Start, out Expression Expression, out int LastChar)
        {
            if (AcceptAtom(Text, Start, out Expression, out LastChar))
            {
                // Try to get a function
                int nc = 0;
                if (AcceptString(Text, LastChar, "(", out nc))
                {
                    List<Expression> exps;
                    AcceptExpressions(Text, nc, out exps, out nc);
                    if (AcceptString(Text, nc, ")", out nc))
                    {
                        LastChar = nc;
                        Expression = new FunctionCallExpression(Expression, exps);
                        return true;
                    }
                }

                return true;
            }

            Expression = null;
            return false;
        }

        /// <summary>
        /// A tree, of operators and expressions.
        /// </summary>
        private class _OperatorTree
        {
            public _OperatorTree Left;
            public Operator Operator;
            public _OperatorTree Right;
            public Expression Value;

            /// <summary>
            /// Adds an expression to the right of this tree.
            /// </summary>
            public _OperatorTree AddRight(Operator Operator, Expression Expression)
            {
                if (this.Value != null)
                {
                    return new _OperatorTree()
                    {
                        Left = this,
                        Operator = Operator,
                        Right = new _OperatorTree() { Value = Expression }
                    };
                }
                if (Operator.Precedence > this.Operator.Precedence || (Operator.Precedence == this.Operator.Precedence && !Operator.LeftAssociative))
                {
                    // Split
                    return new _OperatorTree()
                    {
                        Left = this.Left,
                        Operator = this.Operator,
                        Right = this.Right.AddRight(Operator, Expression)
                    };
                }
                else
                {
                    // Loose bind
                    return new _OperatorTree()
                    {
                        Left = this,
                        Operator = Operator,
                        Right = new _OperatorTree() { Value = Expression }
                    };
                }
            }

            /// <summary>
            /// Gets the expression for this operator tree.
            /// </summary>
            public Expression Expression
            {
                get
                {
                    if (this.Value != null)
                    {
                        return this.Value;
                    }
                    else
                    {
                        List<Expression> args = new List<Expression>();
                        args.Add(this.Left.Expression);
                        args.Add(this.Right.Expression);
                        return new FunctionCallExpression(new VariableExpression(this.Operator.Name), args);
                    }
                }
            }
        }

        /// <summary>
        /// Parses an expression.
        /// </summary>
        public static bool AcceptExpression(string Text, int Start, out Expression Expression, out int LastChar)
        {
            _OperatorTree curtree;
            if (AcceptTightExpression(Text, Start, out Expression, out LastChar))
            {
                curtree = new _OperatorTree() { Value = Expression };
                while (true)
                {
                    int nc;
                    if (AcceptWhitespace(Text, LastChar, out nc) > 0)
                    {
                        Operator op;
                        if (AcceptOperator(Text, nc, out op, out nc))
                        {
                            if (AcceptWhitespace(Text, nc, out nc) > 0)
                            {
                                if (AcceptTightExpression(Text, nc, out Expression, out nc))
                                {
                                    LastChar = nc;
                                    curtree = curtree.AddRight(op, Expression);
                                    continue;
                                }
                            }
                        }
                    }

                    break;
                }

                Expression = curtree.Expression;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Parses a statement.
        /// </summary>
        public static bool AcceptStatement(string Text, int Start, out Statement Statement, out int LastChar)
        {
            // Constant assignment
            if (AcceptString(Text, Start, "const", out LastChar))
            {
                if (AcceptWhitespace(Text, LastChar, out LastChar) > 0)
                {
                    string varname;
                    if (AcceptWord(Text, LastChar, out varname, out LastChar) && ValidVariable(varname))
                    {
                        if (AcceptWhitespace(Text, LastChar, out LastChar) > 0)
                        {
                            if (AcceptString(Text, LastChar, "=", out LastChar))
                            {
                                if (AcceptWhitespace(Text, LastChar, out LastChar) > 0)
                                {
                                    Expression value;
                                    if (AcceptExpression(Text, LastChar, out value, out LastChar))
                                    {
                                        AcceptWhitespace(Text, LastChar, out LastChar);
                                        if (AcceptString(Text, LastChar, ";", out LastChar))
                                        {
                                            Statement = new ConstantStatement(varname, value);
                                            return true;
                                        }
                                    }
                                }
                            }
                        }   
                    }
                }
            }

            // Define
            Expression type;
            if (AcceptTightExpression(Text, Start, out type, out LastChar))
            {
                if (AcceptWhitespace(Text, LastChar, out LastChar) > 0)
                {
                    string varname;
                    if (AcceptWord(Text, LastChar, out varname, out LastChar) && ValidVariable(varname))
                    {
                        if (AcceptWhitespace(Text, LastChar, out LastChar) > 0)
                        {
                            if (AcceptString(Text, LastChar, "=", out LastChar))
                            {
                                if (AcceptWhitespace(Text, LastChar, out LastChar) > 0)
                                {
                                    Expression value;
                                    if (AcceptExpression(Text, LastChar, out value, out LastChar))
                                    {
                                        AcceptWhitespace(Text, LastChar, out LastChar);
                                        if (AcceptString(Text, LastChar, ";", out LastChar))
                                        {
                                            Statement = new DefineStatement(type, varname, value);
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Return
            if (AcceptString(Text, Start, "return", out LastChar))
            {
                if (AcceptWhitespace(Text, LastChar, out LastChar) > 0)
                {
                    Expression returnvalue;
                    if (AcceptExpression(Text, LastChar, out returnvalue, out LastChar))
                    {
                        AcceptWhitespace(Text, LastChar, out LastChar);
                        if (AcceptString(Text, LastChar, ";", out LastChar))
                        {
                            Statement = new ReturnStatement(returnvalue);
                            return true;
                        }
                    }
                }
            }

            Statement = null;
            return false;
        }

        /// <summary>
        /// Parses a procedure expression.
        /// </summary>
        public static bool AcceptProcedure(string Text, int Start, out ProcedureExpression Expression, out int LastChar)
        {
            List<Statement> statements = new List<Statement>();
            Statement statement;
            if (AcceptStatement(Text, Start, out statement, out LastChar))
            {
                statements.Add(statement);
                while (true)
                {
                    int nc;
                    AcceptWhitespace(Text, LastChar, out nc);
                    if (AcceptStatement(Text, nc, out statement, out nc))
                    {
                        LastChar = nc;
                        statements.Add(statement);
                    }
                    else
                    {
                        Expression = new ProcedureExpression(statements);
                        return true;
                    }
                }
            }
            else
            {
                Expression = null;
                return false;
            }
        }

        /// <summary>
        /// An expression generated by the parser.
        /// </summary>
        public abstract class Expression
        {

        }

        /// <summary>
        /// An expression that acts as the value given by a single variable or constant.
        /// </summary>
        public class VariableExpression : Expression
        {
            public VariableExpression(string Name)
            {
                this.Name = Name;
            }

            public string Name;
        }

        /// <summary>
        /// An expression indicating a function call. A function can either be identified by a string (in the case
        /// of an operator, or an overload) or an expression (eg. when chaining together multiple functions).
        /// </summary>
        public class FunctionCallExpression : Expression
        {
            public FunctionCallExpression(Expression Function, IEnumerable<Expression> Arguments)
            {
                this.Function = Function;
                this.Arguments = new List<Expression>(Arguments);
            }
            public FunctionCallExpression(Expression Function, List<Expression> Arguments)
            {
                this.Function = Function;
                this.Arguments = Arguments;
            }

            public Expression Function;
            public List<Expression> Arguments;
        }

        /// <summary>
        /// An expression that acts as a function defined by a series of statements that must be performed to obtain the result.
        /// </summary>
        public class ProcedureExpression : Expression
        {
            public ProcedureExpression(IEnumerable<Statement> Statements)
            {
                this.Statements = new List<Statement>(Statements);
            }

            public ProcedureExpression(List<Statement> Statements)
            {
                this.Statements = Statements;
            }

            public List<Statement> Statements;
        }

        /// <summary>
        /// An expression that stands for an integer.
        /// </summary>
        public class IntegerLiteralExpression : Expression
        {
            public IntegerLiteralExpression(long Value)
            {
                this.Value = Value;
            }

            public long Value;
        }

        /// <summary>
        /// An instruction in a scope.
        /// </summary>
        public abstract class Statement
        {

        }

        /// <summary>
        /// A statement assigning a constant.
        /// </summary>
        public class ConstantStatement : Statement
        {
            public ConstantStatement(string Constant, Expression Value)
            {
                this.Constant = Constant;
                this.Value = Value;
            }

            public string Constant;
            public Expression Value;
        }

        /// <summary>
        /// A statement defining a variable.
        /// </summary>
        public class DefineStatement : Statement
        {
            public DefineStatement(Expression Type, string Variable, Expression Value)
            {
                this.Type = Type;
                this.Variable = Variable;
                this.Value = Value;
            }

            public Expression Type;
            public string Variable;
            public Expression Value;
        }

        /// <summary>
        /// A statement assigning a variable to an expression of its type.
        /// </summary>
        public class AssignStatement : Statement
        {
            public AssignStatement(string Variable, Expression Value)
            {
                this.Variable = Variable;
                this.Value = Value;
            }

            public string Variable;
            public Expression Value;
        }

        /// <summary>
        /// A statement giving a value from a scope.
        /// </summary>
        public class ReturnStatement : Statement
        {
            public ReturnStatement(Expression Value)
            {
                this.Value = Value;
            }

            public Expression Value;
        }

        /// <summary>
        /// A statement made up from many substatements.
        /// </summary>
        public class CompoundStatement : Statement
        {
            public CompoundStatement(IEnumerable<Statement> Statements)
            {
                this.Statements = new List<Statement>(Statements);
            }

            public List<Statement> Statements;
        }
    }
}