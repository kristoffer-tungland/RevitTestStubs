using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Metadata;

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

            foreach (var type in asm.GetTypes().Where(t => t.IsPublic && !t.IsNested && t.IsClass))
            {
                if (type.Namespace is null)
                    continue;

                var code = GenerateStub(type);

                var dir = Path.Combine(outputDir, type.Namespace.Replace('.', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(dir);
                var fileName = Path.Combine(dir, type.Name + ".cs");
                File.WriteAllText(fileName, code);
            }
        }

        private static string GenerateStub(Type type)
        {
            var currentNs = type.Namespace!;
            var usings = CollectNamespaces(type);

            var baseType = type.BaseType != null && type.BaseType != typeof(object)
                ? $" : {GetTypeName(type.BaseType, currentNs)}"
                : string.Empty;

            var writer = new System.Text.StringBuilder();
            foreach (var ns in usings.OrderBy(u => u))
                writer.AppendLine($"using {ns};");

            writer.AppendLine();
            writer.AppendLine($"namespace {currentNs}");
            writer.AppendLine("{");
            writer.AppendLine($"    public partial class {type.Name}{baseType}");
            writer.AppendLine("    {");
            writer.AppendLine($"        public {type.Name}Configuration Configure {{ get; }} = new();");

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).ToArray();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            int methodIndex = 0;
            foreach (var method in methods)
            {
                var parameters = string.Join(", ", method.GetParameters()
                    .Select(p => $"{GetTypeName(p.ParameterType, currentNs)} {p.Name}"));
                var args = string.Join(", ", method.GetParameters().Select(p => p.Name));
                var ret = GetTypeName(method.ReturnType, currentNs);
                var delegateName = $"{method.Name}_{methodIndex++}";

                writer.AppendLine();
                writer.AppendLine($"        public virtual {ret} {method.Name}({parameters})");
                writer.AppendLine("        {");
                if (method.ReturnType == typeof(void))
                {
                    writer.AppendLine($"            var del = Configure.{delegateName};");
                    writer.AppendLine($"            if (del != null) {{ del({args}); return; }}");
                    writer.AppendLine($"            throw new InvalidOperationException(\"{method.Name} not configured.\");");
                }
                else
                {
                    writer.AppendLine($"            return Configure.{delegateName}?.Invoke({args}) ?? throw new InvalidOperationException(\"{method.Name} not configured.\");");
                }
                writer.AppendLine("        }");
            }

            foreach (var prop in properties)
            {
                var typeName = GetTypeName(prop.PropertyType, currentNs);
                writer.AppendLine();
                writer.AppendLine($"        public virtual {typeName} {prop.Name}");
                writer.AppendLine("        {");
                if (prop.CanRead)
                    writer.AppendLine($"            get => Configure.get_{prop.Name}?.Invoke() ?? throw new InvalidOperationException(\"get_{prop.Name} not configured.\");");
                if (prop.CanWrite)
                    writer.AppendLine($"            set => Configure.set_{prop.Name}?.Invoke(value);");
                writer.AppendLine("        }");
            }

            writer.AppendLine("    }");
            writer.AppendLine();
            writer.AppendLine($"    public partial class {type.Name}Configuration");
            writer.AppendLine("    {");

            methodIndex = 0;
            foreach (var method in methods)
            {
                var delegateName = $"{method.Name}_{methodIndex++}";
                var paramTypes = method.GetParameters().Select(p => GetTypeName(p.ParameterType, currentNs)).ToList();
                string delegateType;
                if (method.ReturnType == typeof(void))
                {
                    delegateType = paramTypes.Count == 0 ? "Action" : $"Action<{string.Join(", ", paramTypes)}>";
                }
                else
                {
                    paramTypes.Add(GetTypeName(method.ReturnType, currentNs));
                    delegateType = $"Func<{string.Join(", ", paramTypes)}>";
                }
                writer.AppendLine($"        public {delegateType}? {delegateName} {{ get; set; }}");
            }

            foreach (var prop in properties)
            {
                var tName = GetTypeName(prop.PropertyType, currentNs);
                if (prop.CanRead)
                    writer.AppendLine($"        public Func<{tName}>? get_{prop.Name} {{ get; set; }}");
                if (prop.CanWrite)
                    writer.AppendLine($"        public Action<{tName}>? set_{prop.Name} {{ get; set; }}");
            }

            writer.AppendLine("    }");
            writer.AppendLine("}");
            return writer.ToString();
        }

        private static HashSet<string> CollectNamespaces(Type type)
        {
            var namespaces = new HashSet<string> { "System" };
            void Add(Type? t)
            {
                if (t == null) return;
                if (t.IsGenericType)
                {
                    foreach (var arg in t.GetGenericArguments())
                        Add(arg);
                    t = t.GetGenericTypeDefinition();
                }
                if (!string.IsNullOrEmpty(t.Namespace) && t.Namespace != type.Namespace)
                    namespaces.Add(t.Namespace);
            }

            Add(type.BaseType);
            foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName) continue;
                Add(m.ReturnType);
                foreach (var p in m.GetParameters())
                    Add(p.ParameterType);
            }
            foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                Add(p.PropertyType);

            return namespaces;
        }

        private static string GetTypeName(Type t, string currentNs)
        {
            if (t == typeof(void))
                return "void";

            if (t.IsGenericParameter)
                return t.Name;

            string name;
            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition();
                name = def.Name.Substring(0, def.Name.IndexOf('`'));
                var args = t.GetGenericArguments().Select(a => GetTypeName(a, currentNs));
                name += "<" + string.Join(", ", args) + ">";
            }
            else
            {
                name = t.Name;
            }

            if (!string.IsNullOrEmpty(t.Namespace) && t.Namespace != currentNs)
                return t.Namespace + "." + name;

            return name;
        }
    }
}
