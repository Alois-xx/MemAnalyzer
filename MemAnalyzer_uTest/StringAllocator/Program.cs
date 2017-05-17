using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StringAllocator
{
    class Program
    {
        static void Main(string[] args)
        {
            int n = args.Length == 0 ? 0 : int.Parse(args[0]);

            Console.WriteLine($"Allocating {n} strings");
            List<string> list = new List<string>();
            for(int i=0;i<n;i++)
            {
                list.Add(String.Format($"String {i/10}"));
            }
            Console.WriteLine("All strings allocated");
            Thread.Sleep(30 * 1000);
        }
    }
}
