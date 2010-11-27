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
            // Test
            Parser.ParseResult<Parser.Expression> res = Parser.AcceptScope(@"
                int test = 2 + 4;
                return test;
", 0);

        }
    }
}
