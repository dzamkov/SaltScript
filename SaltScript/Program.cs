using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaltScript
{
    public class Program
    {
        /// <summary>
        /// Program main entry point.
        /// </summary>
        public static void Main(string[] args)
        {
            Parser.Statement state;
            int lastchar;
            Parser.AcceptStatement("const lol = omg;", 0, out state, out lastchar);

            // Test
            KeyValuePair<Parser.ScopeExpression, int>? res = Parser.AcceptScope(@"
                int test = 2 + 4;
                return test;
", 0);

        }
    }
}
