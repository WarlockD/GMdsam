using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DFSListVisualizer;

namespace TestVisualizer
{
    class Program
    {
        static void Main(string[] args)
        {
            String myString = "Hello, World";
            List<string> happy_tree = new List<string>();
            happy_tree.Add("flkdfld");
            happy_tree.Add("MAMAM");

            DFSListVisualizer.SecondTest.TestShowVisualizer(happy_tree);
        }
    }
}
