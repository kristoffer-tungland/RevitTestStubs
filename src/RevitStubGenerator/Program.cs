using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

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

            var resolver = new PathAssemblyResolver(new[] { assemblyPath });
            using var mlc = new MetadataLoadContext(resolver);
            var asm = mlc.LoadFromAssemblyPath(assemblyPath);

            foreach (var type in asm.GetTypes().Where(t => t.IsPublic && !t.IsNested))
            {
                if (type.Namespace is null) continue;
                var code = GenerateStub(type);
                var fileName = Path.Combine(outputDir, type.Name + ".cs");
                File.WriteAllText(fileName, code);
            }
        }

        private static string GenerateStub(Type type)
        {
            var ns = type.Namespace;
            var baseType = type.BaseType != null && type.BaseType != typeof(object)
                ? $" : {type.BaseType.FullName}"
                : string.Empty;

            var writer = new System.Text.StringBuilder();
            writer.AppendLine("using System;");
            writer.AppendLine();
            writer.AppendLine($"namespace {ns}");
            writer.AppendLine("{");
            writer.AppendLine($"    public partial class {type.Name}{baseType}");
            writer.AppendLine("    {");

            if (type.Name == "Element")
            {
                writer.AppendLine("        public ElementId Id { get; }");
                writer.AppendLine("        public ElementConfiguration Configure { get; } = new();");
                writer.AppendLine();
                writer.AppendLine("        public Element(ElementId id)");
                writer.AppendLine("        {");
                writer.AppendLine("            Id = id;");
                writer.AppendLine("        }");
                writer.AppendLine();
                writer.AppendLine("        public Element(int id) : this(new ElementId(id)) {}");
                writer.AppendLine("    }");
                writer.AppendLine();
                writer.AppendLine("    public partial class ElementConfiguration");
                writer.AppendLine("    {");
                writer.AppendLine("        public Func<Guid, Parameter>? GetParameter { get; set; }");
                writer.AppendLine("        public Action? Dispose { get; set; }");
                writer.AppendLine("    }");
                writer.AppendLine("}");
                return writer.ToString();
            }
            else if (type.Name == "Parameter")
            {
                writer.AppendLine("        public new ParameterConfiguration Configure { get; } = new();");
                writer.AppendLine();
                writer.AppendLine("        public Parameter(ElementId id) : base(id) { }");
                writer.AppendLine("        public Parameter(int id) : base(id) { }");
                writer.AppendLine();
                writer.AppendLine("        public Guid GUID => Configure.get_Guid?.Invoke() ?? throw new InvalidOperationException(\"get_Guid not configured.\");");
                writer.AppendLine("    }");
                writer.AppendLine();
                writer.AppendLine("    public partial class ParameterConfiguration : ElementConfiguration");
                writer.AppendLine("    {");
                writer.AppendLine("        public Func<Guid>? get_Guid { get; set; }");
                writer.AppendLine("    }");
                writer.AppendLine("}");
                return writer.ToString();
            }
            else if (type.Name == "ElementId")
            {
                writer.AppendLine("        public int IntegerValue { get; }");
                writer.AppendLine();
                writer.AppendLine("        public ElementId(int value)");
                writer.AppendLine("        {");
                writer.AppendLine("            IntegerValue = value;");
                writer.AppendLine("        }");
                writer.AppendLine("    }");
                writer.AppendLine("}");
                return writer.ToString();
            }
            else
            {
                writer.AppendLine("    }");
                writer.AppendLine("}");
                return writer.ToString();
            }
        }
    }
}
