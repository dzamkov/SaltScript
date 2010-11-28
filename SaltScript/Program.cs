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
                try
                {
                    Datum val = Interpret.Evaluate(str);
                    Console.WriteLine(val.Type.Display(val.Value) + " : " + val.Type.Name);
                }
                catch
                {
                    Console.WriteLine("Error: Try again");
                }
            }
        }
    }
}
