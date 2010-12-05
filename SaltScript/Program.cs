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
            while (true)
            {
                Console.Write(">>> ");
                string str = Console.ReadLine();
                Datum val = Interpret.Evaluate(str);
                Console.WriteLine(val.Value.ToString());
            }
        }
    }
}
