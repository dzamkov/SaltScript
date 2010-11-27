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
            Word = null;
            LastChar = c;
            return false;
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
            }
            LastChar = c;
            return LastChar - Start;
        }

        /// <summary>
        /// Parses an expression.
        /// </summary>
        public static bool AcceptExpression(string Text, int Start, out Expression Expression, out int LastChar)
        {
            throw new NotImplementedException();
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

            Statement = null;
            return false;
        }

        /// <summary>
        /// Parses a scope.
        /// </summary>
        public static KeyValuePair<ScopeExpression, int>? AcceptScope(string Text, int Start)
        {
            throw new NotImplementedException();
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
        public class FunctionExpression : Expression
        {
            public FunctionExpression(Expression Function, IEnumerable<Expression> Arguments)
            {
                this.Function = Function;
                this.Arguments = new List<Expression>(Arguments);
            }

            public FunctionExpression(string FunctionName, IEnumerable<Expression> Arguments)
            {
                this.FunctionName = FunctionName;
                this.Arguments = new List<Expression>(Arguments);
            }

            public Expression Function;
            public string FunctionName;
            public List<Expression> Arguments;
        }

        /// <summary>
        /// An expression that acts as a function defined by a series of statements that must be performed to obtain the result.
        /// </summary>
        public class ScopeExpression : Expression
        {
            public ScopeExpression(IEnumerable<Statement> Statements)
            {
                this.Statements = new List<Statement>(Statements);
            }

            public ScopeExpression(List<Statement> Statements)
            {
                this.Statements = Statements;
            }

            public List<Statement> Statements;
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