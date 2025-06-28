using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace StubGenerator
{
    public static class StubGenerator
    {
        public static void Generate(string assemblyPath, string outputDir)
        {
            var resolver = new PathAssemblyResolver(new[] { assemblyPath });
            using var mlc = new MetadataLoadContext(resolver);
            var asm = mlc.LoadFromAssemblyPath(assemblyPath);

            foreach (var type in asm.GetTypes().Where(t => t.IsPublic && !t.IsNested))
            {
                if (type.Namespace is null)
                    continue;

                string code;
                if (type.IsEnum)
                {
                    code = GenerateEnumStub(type);
                }
                else if (type.IsInterface)
                {
                    code = GenerateInterfaceStub(type);
                }
                else if (type.IsValueType && !type.IsEnum)
                {
                    code = GenerateStructStub(type);
                }
                else if (typeof(MulticastDelegate).IsAssignableFrom(type.BaseType))
                {
                    code = GenerateDelegateStub(type);
                }
                else if (type.IsClass)
                {
                    code = GenerateClassStub(type);
                }
                else
                {
                    continue;
                }

                var dir = Path.Combine(outputDir, type.Namespace.Replace('.', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(dir);
                var fileName = Path.Combine(dir, type.Name + ".cs");
                File.WriteAllText(fileName, code);
            }
        }

        public static string GenerateClassStub(Type type)
        {
            var currentNs = type.Namespace!;
            var usings = CollectNamespaces(type);

            var baseParts = new List<string>();
            if (type.BaseType != null && type.BaseType != typeof(object))
                baseParts.Add(GetTypeName(type.BaseType, currentNs));

            var interfaces = type.GetInterfaces();
            if (type.BaseType != null)
            {
                var baseInterfaces = new HashSet<Type>(type.BaseType.GetInterfaces());
                interfaces = interfaces.Where(i => !baseInterfaces.Contains(i)).ToArray();
            }
            foreach (var iface in interfaces)
                baseParts.Add(GetTypeName(iface, currentNs));

            var baseType = baseParts.Count > 0
                ? " : " + string.Join(", ", baseParts)
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
            var events = type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            int methodIndex = 0;
            foreach (var method in methods)
            {
                var genArgs = method.GetGenericArguments();
                var genericDecl = genArgs.Length > 0 ? "<" + string.Join(", ", genArgs.Select(a => a.Name)) + ">" : string.Empty;

                var parameters = string.Join(", ", method.GetParameters()
                    .Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
                var args = string.Join(", ", method.GetParameters().Select(p => p.Name));
                var typeArgsInvoke = string.Join(", ", genArgs.Select(a => $"typeof({a.Name})"));
                var invokeArgs = string.Join(", ", new[] { args, typeArgsInvoke }.Where(a => !string.IsNullOrEmpty(a)));

                var ret = GetTypeName(method.ReturnType, currentNs);
                var delegateName = $"{method.Name}_{methodIndex++}";

                writer.AppendLine();
                writer.AppendLine($"        public virtual {ret} {method.Name}{genericDecl}({parameters})");
                if (genArgs.Length > 0)
                {
                    foreach (var ga in genArgs)
                    {
                        var constraints = new List<string>();
                        var attrs = ga.GenericParameterAttributes;
                        if ((attrs & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                            constraints.Add("class");
                        if ((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                            constraints.Add("struct");
                        foreach (var c in ga.GetGenericParameterConstraints())
                            constraints.Add(GetTypeName(c, currentNs));
                        if ((attrs & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                            constraints.Add("new()");
                        if (constraints.Count > 0)
                            writer.AppendLine($"            where {ga.Name} : {string.Join(", ", constraints)}");
                    }
                }
                writer.AppendLine("        {");
                if (method.ReturnType == typeof(void))
                {
                    writer.AppendLine($"            var del = Configure.{delegateName};");
                    writer.AppendLine($"            if (del != null) {{ del({invokeArgs}); return; }}");
                    writer.AppendLine($"            throw new InvalidOperationException(\"{method.Name} not configured.\");");
                }
                else
                {
                    var call = $"Configure.{delegateName}?.Invoke({invokeArgs}) ?? throw new InvalidOperationException(\"{method.Name} not configured.\")";
                    if (UsesGenericParameter(method.ReturnType))
                        writer.AppendLine($"            return ({ret}){call};");
                    else
                        writer.AppendLine($"            return {call};");
                }
                writer.AppendLine("        }");
            }

            foreach (var prop in properties)
            {
                var typeName = GetTypeName(prop.PropertyType, currentNs);
                var indexParams = prop.GetIndexParameters();
                var paramDecl = string.Join(", ", indexParams.Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
                var paramNames = string.Join(", ", indexParams.Select(p => p.Name));

                writer.AppendLine();
                if (indexParams.Length == 0)
                {
                    writer.AppendLine($"        public virtual {typeName} {prop.Name}");
                }
                else
                {
                    writer.AppendLine($"        public virtual {typeName} this[{paramDecl}]");
                }
                writer.AppendLine("        {");
                if (prop.CanRead)
                    writer.AppendLine($"            get => Configure.get_{prop.Name}?.Invoke({paramNames}) ?? throw new InvalidOperationException(\"get_{prop.Name} not configured.\");");
                if (prop.CanWrite)
                {
                    var args = string.Join(", ", new[] { paramNames, "value" }.Where(a => !string.IsNullOrEmpty(a)));
                    writer.AppendLine($"            set => Configure.set_{prop.Name}?.Invoke({args});");
                }
                writer.AppendLine("        }");
            }

            foreach (var ev in events)
            {
                var evType = GetTypeName(ev.EventHandlerType!, currentNs);
                var invoke = ev.EventHandlerType!.GetMethod("Invoke")!;
                var evParams = string.Join(", ", invoke.GetParameters().Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
                var evArgs = string.Join(", ", invoke.GetParameters().Select(p => p.Name));

                writer.AppendLine();
                writer.AppendLine($"        public event {evType}? {ev.Name}");
                writer.AppendLine("        {");
                writer.AppendLine($"            add => Configure.{ev.Name} += value;");
                writer.AppendLine($"            remove => Configure.{ev.Name} -= value;");
                writer.AppendLine("        }");

                writer.AppendLine();
                writer.AppendLine($"        public void Raise{ev.Name}({evParams})");
                writer.AppendLine("        {");
                writer.AppendLine($"            Configure.{ev.Name}?.Invoke({evArgs});");
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
                var paramTypes = new List<string>();
                foreach (var p in method.GetParameters())
                {
                    var pt = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
                    paramTypes.Add(UsesGenericParameter(pt) ? "object?" : GetTypeName(pt, currentNs));
                }
                var genArgs = method.GetGenericArguments();
                for (int i = 0; i < genArgs.Length; i++)
                    paramTypes.Add("System.Type");
                string delegateType;
                if (method.ReturnType == typeof(void))
                {
                    delegateType = paramTypes.Count == 0 ? "Action" : $"Action<{string.Join(", ", paramTypes)}>";
                }
                else
                {
                    paramTypes.Add(UsesGenericParameter(method.ReturnType) ? "object?" : GetTypeName(method.ReturnType, currentNs));
                    delegateType = $"Func<{string.Join(", ", paramTypes)}>";
                }
                writer.AppendLine($"        public {delegateType}? {delegateName} {{ get; set; }}");
            }

            foreach (var prop in properties)
            {
                var tName = GetTypeName(prop.PropertyType, currentNs);
                var indexParams = prop.GetIndexParameters();
                var paramTypes = string.Join(", ", indexParams.Select(p => GetTypeName(p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType, currentNs)));
                if (prop.CanRead)
                {
                    var typeList = string.Join(", ", new[] { paramTypes, tName }.Where(s => !string.IsNullOrEmpty(s)));
                    writer.AppendLine($"        public Func<{typeList}>? get_{prop.Name} {{ get; set; }}");
                }
                if (prop.CanWrite)
                {
                    var typeList = string.Join(", ", new[] { paramTypes, tName }.Where(s => !string.IsNullOrEmpty(s)));
                    writer.AppendLine($"        public Action<{typeList}>? set_{prop.Name} {{ get; set; }}");
                }
            }

            foreach (var ev in events)
            {
                var evType = GetTypeName(ev.EventHandlerType!, currentNs);
                writer.AppendLine($"        public event {evType}? {ev.Name};");
            }

            writer.AppendLine("    }");
            writer.AppendLine("}");
            return writer.ToString();
        }

        public static string GenerateEnumStub(Type type)
        {
            var currentNs = type.Namespace!;
            var writer = new System.Text.StringBuilder();
            writer.AppendLine($"namespace {currentNs}");
            writer.AppendLine("{");
            var underlying = Enum.GetUnderlyingType(type);
            var baseDecl = underlying != typeof(int) ? $" : {GetTypeName(underlying, currentNs)}" : string.Empty;
            writer.AppendLine($"    public enum {type.Name}{baseDecl}");
            writer.AppendLine("    {");
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                var value = Convert.ChangeType(f.GetRawConstantValue(), underlying);
                var line = $"        {f.Name} = {value}";
                if (i < fields.Length - 1) line += ",";
                writer.AppendLine(line);
            }
            writer.AppendLine("    }");
            writer.AppendLine("}");
            return writer.ToString();
        }

        public static string GenerateInterfaceStub(Type type)
        {
            var currentNs = type.Namespace!;
            var usings = CollectNamespaces(type);

            var bases = type.GetInterfaces();
            var baseDecl = bases.Length > 0 ? " : " + string.Join(", ", bases.Select(i => GetTypeName(i, currentNs))) : string.Empty;

            var writer = new System.Text.StringBuilder();
            foreach (var ns in usings.OrderBy(u => u))
                writer.AppendLine($"using {ns};");

            writer.AppendLine();
            writer.AppendLine($"namespace {currentNs}");
            writer.AppendLine("{");
            writer.AppendLine($"    public interface {type.Name}{baseDecl}");
            writer.AppendLine("    {");

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var typeName = GetTypeName(prop.PropertyType, currentNs);
                var get = prop.CanRead ? " get;" : string.Empty;
                var set = prop.CanWrite ? " set;" : string.Empty;
                var indexParams = prop.GetIndexParameters();
                if (indexParams.Length == 0)
                {
                    writer.AppendLine($"        {typeName} {prop.Name} {{{get}{set}}}");
                }
                else
                {
                    var paramDecl = string.Join(", ", indexParams.Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
                    writer.AppendLine($"        {typeName} this[{paramDecl}] {{{get}{set}}}");
                }
            }

            foreach (var ev in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var evType = GetTypeName(ev.EventHandlerType!, currentNs);
                writer.AppendLine($"        event {evType} {ev.Name};");
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName) continue;
                var genArgs = method.GetGenericArguments();
                var genericDecl = genArgs.Length > 0 ? "<" + string.Join(", ", genArgs.Select(a => a.Name)) + ">" : string.Empty;
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
                var ret = GetTypeName(method.ReturnType, currentNs);
                writer.AppendLine($"        {ret} {method.Name}{genericDecl}({parameters});");
                if (genArgs.Length > 0)
                {
                    foreach (var ga in genArgs)
                    {
                        var constraints = new List<string>();
                        var attrs = ga.GenericParameterAttributes;
                        if ((attrs & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                            constraints.Add("class");
                        if ((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                            constraints.Add("struct");
                        foreach (var c in ga.GetGenericParameterConstraints())
                            constraints.Add(GetTypeName(c, currentNs));
                        if ((attrs & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                            constraints.Add("new()");
                        if (constraints.Count > 0)
                            writer.AppendLine($"        where {ga.Name} : {string.Join(", ", constraints)}");
                    }
                }
            }

            writer.AppendLine("    }");
            writer.AppendLine("}");
            return writer.ToString();
        }

        public static string GenerateStructStub(Type type)
        {
            var currentNs = type.Namespace!;
            var usings = CollectNamespaces(type);

            var writer = new System.Text.StringBuilder();
            foreach (var ns in usings.OrderBy(u => u))
                writer.AppendLine($"using {ns};");

            writer.AppendLine();
            writer.AppendLine($"namespace {currentNs}");
            writer.AppendLine("{");
            writer.AppendLine($"    public partial struct {type.Name}");
            writer.AppendLine("    {");

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var tName = GetTypeName(field.FieldType, currentNs);
                writer.AppendLine($"        public {tName} {field.Name};");
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var tName = GetTypeName(prop.PropertyType, currentNs);
                var get = prop.CanRead ? " get;" : string.Empty;
                var set = prop.CanWrite ? " set;" : string.Empty;
                var indexParams = prop.GetIndexParameters();
                if (indexParams.Length == 0)
                {
                    writer.AppendLine($"        public {tName} {prop.Name} {{{get}{set}}}");
                }
                else
                {
                    var paramDecl = string.Join(", ", indexParams.Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
                    writer.AppendLine($"        public {tName} this[{paramDecl}] {{{get}{set}}}");
                }
            }

            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                var parameters = string.Join(", ", ctor.GetParameters().Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
                writer.AppendLine($"        public {type.Name}({parameters}) {{ }}");
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName) continue;
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
                var ret = GetTypeName(method.ReturnType, currentNs);
                string body = method.ReturnType == typeof(void) ? "{}" : "=> throw new NotImplementedException();";
                writer.AppendLine($"        public {ret} {method.Name}({parameters}) {body}");
            }

            writer.AppendLine("    }");
            writer.AppendLine("}");
            return writer.ToString();
        }

        public static string GenerateDelegateStub(Type type)
        {
            var currentNs = type.Namespace!;
            var usings = CollectNamespaces(type);

            var invoke = type.GetMethod("Invoke")!;
            var parameters = string.Join(", ", invoke.GetParameters().Select(p => $"{GetTypeName(p.ParameterType, currentNs, p)} {p.Name}"));
            var ret = GetTypeName(invoke.ReturnType, currentNs);

            var writer = new System.Text.StringBuilder();
            foreach (var ns in usings.OrderBy(u => u))
                writer.AppendLine($"using {ns};");

            writer.AppendLine();
            writer.AppendLine($"namespace {currentNs}");
            writer.AppendLine("{");
            writer.AppendLine($"    public delegate {ret} {type.Name}({parameters});");
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

            if (type.IsEnum)
            {
                Add(Enum.GetUnderlyingType(type));
            }
            else if (typeof(MulticastDelegate).IsAssignableFrom(type.BaseType))
            {
                var invoke = type.GetMethod("Invoke");
                if (invoke != null)
                {
                    Add(invoke.ReturnType);
                    foreach (var p in invoke.GetParameters())
                        Add(p.ParameterType);
                }
            }
            else
            {
                if (type.IsClass)
                    Add(type.BaseType);
                if (type.IsInterface)
                {
                    foreach (var iface in type.GetInterfaces())
                        Add(iface);
                }

                foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.IsSpecialName) continue;
                    Add(m.ReturnType);
                    foreach (var p in m.GetParameters())
                        Add(p.ParameterType);
                }

                foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    Add(p.PropertyType);

                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    Add(f.FieldType);

                foreach (var ev in type.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    Add(ev.EventHandlerType);
            }

            return namespaces;
        }

        private static string GetTypeName(Type t, string currentNs, ParameterInfo? parameter = null)
        {
            string prefix = string.Empty;
            if (t.IsByRef)
            {
                if (parameter != null)
                    prefix = parameter.IsOut ? "out " : "ref ";
                t = t.GetElementType()!;
            }

            if (t == typeof(void))
                return prefix + "void";

            if (t.IsGenericParameter)
                return prefix + t.Name;

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
                return prefix + t.Namespace + "." + name;

            return prefix + name;
        }

        private static bool UsesGenericParameter(Type t)
        {
            if (t.IsGenericParameter)
                return true;
            if (t.IsByRef || t.IsPointer || t.IsArray)
                return UsesGenericParameter(t.GetElementType()!);
            if (t.IsGenericType)
                return t.GetGenericArguments().Any(UsesGenericParameter);
            return false;
        }
    }
}
