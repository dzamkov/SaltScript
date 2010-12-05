using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SaltScript
{
    public class Program
    {
        /// <summary>
        /// Program main entry point.
        /// </summary>
        public static void Main(string[] args)
        {
            Parser.ProcedureExpression pe = Parser.Parse(File.OpenRead("test.salt"));
            if (pe != null)
            {
                Datum val = Interpret.Evaluate(pe, Default.Input);
                Console.WriteLine(val.Value.ToString());
            }
            else
            {
                Console.WriteLine("Syntax error... somewhere");
            }
            Console.ReadKey();
            /*
            while (true)
            {
                Console.Write(">>> ");
                string str = Console.ReadLine();
                Datum val = Interpret.Evaluate(str);
                Console.WriteLine(val.Value.ToString());
            }
             */
        }
    }
}
