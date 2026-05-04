using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NexusFramework
{
    public partial class Library
    {
        public Definitions.TargetInfo Target
        { get; } = new Definitions.TargetInfo();

        public List<Definitions.ProcessList> ProcessList
        { get; set; } = new List<Definitions.ProcessList>();

        public Definitions.ProcessInfo Process
        { get; } = new Definitions.ProcessInfo();

        public Definitions.Versioning LibraryVersion
        { get; } = new Definitions.Versioning();

        public Definitions.Versioning APIVersion
        { get; } = new Definitions.Versioning();

        internal void UpdateTargetIPAddress(string? address = null)
        {
            if (!string.IsNullOrWhiteSpace(address))
                Target.IP = address!;
        }

        internal void ThrowNotConnectedException()
        {
            if (!Target.Connected)
                throw new TargetNotConnectedException();
        }

        internal void ThrowVersioMismatchException()
        {
            if (APIVersion.Version != LibraryVersion.Version)
                throw new Exception($"Libray/API mismatch. Library v{LibraryVersion.Version:F2} != API v{APIVersion.Version:F2}. Please correct this and try again.");
        }

        internal void PrintEndUserDocumentation(bool log)
        {
            if (!log) return;

            var sb = new StringBuilder();
            void WriteLine(string line = "") => sb.AppendLine(line);
            void WriteIndented(string line) => sb.AppendLine("    " + line);

            string CleanTypeName(Type t)
            {
                if (t.IsGenericType)
                {
                    var genericArgs = t.GetGenericArguments();
                    var baseName = t.Name.Split('`')[0];
                    var args = string.Join(", ", genericArgs.Select(CleanTypeName));
                    return $"{baseName}<{args}>";
                }
                return t.Name;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                .Where(t => (t.IsPublic || t.IsNestedPublic) && !t.IsSubclassOf(typeof(Exception)))
                .OrderBy(t => t.FullName);

            foreach (var type in types)
            {
                WriteLine($"==== {CleanTypeName(type)} ====");

                if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(Enum))
                    WriteLine($"Base Class: {CleanTypeName(type.BaseType)}");

                if (type.IsEnum)
                {
                    WriteLine("Enum Values:");
                    var underlyingType = Enum.GetUnderlyingType(type);
                    foreach (var val in Enum.GetValues(type))
                    {
                        var numericValue = Convert.ChangeType(val, underlyingType);
                        WriteIndented($"const {CleanTypeName(type)} {val} = {numericValue}");
                    }
                    WriteLine();
                    continue;
                }

                var nestedTypes = type.GetNestedTypes(BindingFlags.Public)
                    .Where(t => !t.Name.StartsWith("<") && !t.IsSubclassOf(typeof(Exception)))
                    .OrderBy(t => t.Name);
                foreach (var nestedType in nestedTypes)
                    WriteIndented($"Nested Type: {CleanTypeName(nestedType)}");

                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(f => !f.IsSpecialName)
                    .OrderBy(f => f.Name);
                foreach (var f in fields)
                {
                    var prefix = f.IsLiteral && !f.IsInitOnly ? "const " :
                                 f.IsInitOnly ? "readonly " : "";
                    WriteIndented($"{prefix}{CleanTypeName(f.FieldType)} {f.Name}");
                }

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(p => (p.GetMethod?.IsPublic == true || p.SetMethod?.IsPublic == true))
                    .OrderBy(p => p.Name);
                foreach (var p in props)
                {
                    var accessors = new List<string>();
                    if (p.CanRead && p.GetMethod.IsPublic) accessors.Add("get");
                    if (p.CanWrite && p.SetMethod.IsPublic) accessors.Add("set");
                    WriteIndented($"{CleanTypeName(p.PropertyType)} {p.Name} {{ {string.Join(", ", accessors)} }}");
                }

                var objectMethods = typeof(object).GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name).ToHashSet();
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName && !objectMethods.Contains(m.Name))
                    .OrderBy(m => m.Name);
                foreach (var method in methods)
                {
                    var modifiers = new List<string>();
                    if (method.IsStatic) modifiers.Add("static");

                    var parameters = method.GetParameters();
                    var paramList = string.Join(", ", parameters.Select(p =>
                    {
                        var def = p.HasDefaultValue ? $" = {p.DefaultValue ?? "null"}" : "";
                        return $"{CleanTypeName(p.ParameterType)} {p.Name}{def}";
                    }));

                    WriteIndented($"{string.Join(" ", modifiers)} {CleanTypeName(method.ReturnType)} {method.Name}({paramList})");
                }

                WriteLine();
            }

            Debug.WriteLine(sb.ToString());
        }

        public Library()
        {
            PrintEndUserDocumentation(true);

            string versionStr = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                .Split(new[] { '+', 'b' }, StringSplitOptions.RemoveEmptyEntries)[0] ?? "0.0";

            float versionFloat = 0f;
            var versionParts = versionStr.Split('.');
            if (versionParts.Length >= 2)
                float.TryParse($"{versionParts[0]}.{versionParts[1]}", out versionFloat);

            LibraryVersion.Version = versionFloat;

            string buildNumberStr = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                .Version ?? "0";

            int buildNumberInt = 0;
            var buildParts = buildNumberStr.Split('.');
            if (buildParts.Length > 0)
                int.TryParse(buildParts[buildParts.Length - 1], out buildNumberInt); 

            LibraryVersion.BuildNumber = buildNumberInt;

            string? value = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuildDate")?.Value;

            _ = DateTime.TryParse(value, out var utcDate);
            DateTime localDate = utcDate.ToLocalTime();
            LibraryVersion.BuildDate = localDate.ToString("MMMM d yyyy");
        }

    }
}