using System;
using System.IO;

namespace RevitStubGenerator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: RevitStubGenerator <assemblyPath> <outputDir>");
                return;
            }

            var assemblyPath = Path.GetFullPath(args[0]);
            var outputDir = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(outputDir);

            StubGenerator.Generate(assemblyPath, outputDir);
        }
    }
}
