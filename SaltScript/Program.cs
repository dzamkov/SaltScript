using System;
using System.Collections.Generic;
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
            Datum oval = Interpret.Evaluate(@"{
                    int a = 3;
                    int b = 6;
                    int c = a + b;
                    c = c + a;
                    return c;
                }");

            while (true)
            {
                Console.Write(">>> ");
                string str = Console.ReadLine();
                Datum val = Interpret.Evaluate(str);
                Console.WriteLine(val.Display);
            }
        }
    }
}
