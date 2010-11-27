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
        /// The result of a parse function applied to some text.
        /// </summary>
        public struct ParseResult<TData>
        {
            public ParseResult(IEnumerable<KeyValuePair<TData, int>> Interpretations)
            {
                this.Interpretations = new List<KeyValuePair<TData, int>>(Interpretations);
            }

            public ParseResult(List<KeyValuePair<TData, int>> Interpretations)
            {
                this.Interpretations = Interpretations;
            }

            /// <summary>
            /// Creates a parse result with only one interpretation.
            /// </summary>
            public static ParseResult<TData> Singleton(TData Data, int LastChar)
            {
                List<KeyValuePair<TData, int>> ints = new List<KeyValuePair<TData, int>>();
                ints.Add(new KeyValuePair<TData, int>(Data, LastChar));
                return new ParseResult<TData>(ints);
            }

            /// <summary>
            /// A parse result with no interpretations.
            /// </summary>
            public static readonly ParseResult<TData> Empty = new ParseResult<TData>(new List<KeyValuePair<TData, int>>());

            /// <summary>
            /// Wraps a nullable interpretation pair into a parse result which is either empty, or contains the interpretation.
            /// </summary>
            public static ParseResult<TData> Wrap(KeyValuePair<TData, int>? Interpretation)
            {
                if (Interpretation == null)
                {
                    return Empty;
                }
                else
                {
                    var li = new List<KeyValuePair<TData, int>>();
                    li.Add(Interpretation.Value);
                    return new ParseResult<TData>(li);
                }
            }

            /// <summary>
            /// The different interpretations the parser has of the string so far. Each interpretation is given
            /// by the data it produces and the index of the character it ends on.
            /// </summary>
            public List<KeyValuePair<TData, int>> Interpretations;
        }

        /// <summary>
        /// A function that parses text in a certain way to produce data.
        /// </summary>
        public delegate ParseResult<TData> ParseFunction<TData>(string Text, int Start);

        /// <summary>
        /// Applies a parse function to every result of another parse.
        /// </summary>
        public static ParseResult<TNData> Concat<TData, TNData>(string Text, ParseResult<TData> Result, Func<TData, ParseFunction<TNData>> Function)
        {
            List<KeyValuePair<TNData, int>> ints = new List<KeyValuePair<TNData, int>>();
            foreach (var i in Result.Interpretations)
            {
                ints.AddRange(Function(i.Key)(Text, i.Value).Interpretations);
            }
            return new ParseResult<TNData>(ints);
        }

        /// <summary>
        /// Creates a function that accepts multiple "Atoms" as possible.
        /// </summary>
        /// <param name="Greedy">True to discard all interpretations that don't have as many atoms as possible in the string.</param>
        /// <param name="Min">The minimum amount of atoms parsed.</param>
        /// <param name="Max">The maximum amount of atmos parsed or -1 for no limit.</param>
        public static ParseFunction<TNData> AcceptMultiple<TData, TNData>(
            ParseFunction<TData> Atom, 
            Func<IEnumerable<TData>, TNData> Compress, 
            bool Greedy, int Min, int Max)
        {
            return delegate(string Text, int Start)
            {
                List<KeyValuePair<TNData, int>> res = new List<KeyValuePair<TNData,int>>();
                List<_AcceptMostIntermediate<TData>> intermediates = new List<_AcceptMostIntermediate<TData>>();
                intermediates.Add(new _AcceptMostIntermediate<TData>() { CurrentChain = new List<TData>(), LastChar = Start });
                while (intermediates.Count > 0)
                {
                    List<_AcceptMostIntermediate<TData>> nintermediates = new List<_AcceptMostIntermediate<TData>>();
                    foreach (_AcceptMostIntermediate<TData> intermediate in intermediates)
                    {
                        if (!Greedy && intermediate.CurrentChain.Count >= Min)
                        {
                            res.Add(new KeyValuePair<TNData, int>(Compress(intermediate.CurrentChain), intermediate.LastChar));
                        }

                        bool addednew = false;
                        foreach (var ires in Atom(Text, intermediate.LastChar).Interpretations)
                        {
                            if (!addednew)
                            {
                                intermediate.CurrentChain.Add(ires.Key);
                                nintermediates.Add(new _AcceptMostIntermediate<TData>()
                                {
                                    CurrentChain = intermediate.CurrentChain,
                                    LastChar = ires.Value
                                });
                                addednew = true;
                            }
                            else
                            {
                                List<TData> chain = new List<TData>();
                                chain.AddRange(intermediate.CurrentChain);
                                chain.RemoveAt(chain.Count - 1);
                                chain.Add(ires.Key);
                                nintermediates.Add(new _AcceptMostIntermediate<TData>()
                                {
                                    CurrentChain = chain,
                                    LastChar = ires.Value
                                });
                            }
                        }
                        if (!addednew && Greedy)
                        {
                            res.Add(new KeyValuePair<TNData, int>(Compress(intermediate.CurrentChain), intermediate.LastChar));
                        }
                    }
                    intermediates = nintermediates;
                }
                return new ParseResult<TNData>(res);
            };
        }

        /// <summary>
        /// Intermediate result for AcceptMost.
        /// </summary>
        private struct _AcceptMostIntermediate<TData>
        {
            public List<TData> CurrentChain;
            public int LastChar;
        }

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
        /// Greedily parses a word.
        /// </summary>
        public static ParseResult<string> AcceptWord(string Text, int Start)
        {
            return ParseResult<string>.Wrap(QuickAcceptWord(Text, Start));
        }

        /// <summary>
        /// Greedily parses a word, or returns null if there is no word at the specified position.
        /// </summary>
        public static KeyValuePair<string, int>? QuickAcceptWord(string Text, int Start)
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
                        return null;
                    }
                }
                else
                {
                    if (!ValidWordChar(ch))
                    {
                        return new KeyValuePair<string, int>(Text.Substring(Start, c - Start - 1), c);
                    }
                }
                c++;
            }
            return null;
        }

        /// <summary>
        /// Parses the specified target string in the text.
        /// </summary>
        public static KeyValuePair<string, int>? QuickAcceptString(string Text, int Start, string Target)
        {
            if (Text.Length - Start < Text.Length)
            {
                return null;
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
                return new KeyValuePair<string, int>(Target, Start);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Parses an expression.
        /// </summary>
        public static ParseResult<Expression> AcceptExpression(string Text, int Start)
        {

        }

        /// <summary>
        /// Parses a statement.
        /// </summary>
        public static ParseResult<Statement> AcceptStatement(string Text, int Start)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Parses a scope.
        /// </summary>
        public static ParseResult<Expression> AcceptScope(string Text, int Start)
        {
            return AcceptMultiple<Statement, Expression>(AcceptStatement, statements => new ScopeExpression(statements), true, 1, -1)(Text, Start);
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
        public class ConstantStatement
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
        public class DefineStatement
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
        public class AssignStatement
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
        public class ReturnStatement
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
        public class CompoundStatement
        {
            public CompoundStatement(IEnumerable<Statement> Statements)
            {
                this.Statements = new List<Statement>(Statements);
            }

            public List<Statement> Statements;
        }
    }
}