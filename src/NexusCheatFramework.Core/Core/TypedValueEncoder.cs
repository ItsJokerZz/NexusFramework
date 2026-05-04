using System;
using System.Globalization;
using System.Text;

namespace NexusCheatFramework.Core
{
    internal static class TypedValueEncoder
    {
        public static byte[] Encode(string? valueType, string? value)
        {
            if (string.IsNullOrWhiteSpace(valueType)) throw new FormatException("Typed write requires 'valueType'.");
            if (value is null) throw new FormatException("Typed write requires 'value'.");
            var inv = CultureInfo.InvariantCulture;
            switch (valueType!.Trim().ToLowerInvariant())
            {
                case "byte":   case "u8":  return new[] { byte.Parse(value, NumberStyles.Integer, inv) };
                case "sbyte":  case "i8":  return new[] { (byte)sbyte.Parse(value, NumberStyles.Integer, inv) };
                case "short":  case "i16": return BitConverter.GetBytes(short.Parse(value, NumberStyles.Integer, inv));
                case "ushort": case "u16": return BitConverter.GetBytes(ushort.Parse(value, NumberStyles.Integer, inv));
                case "int":    case "i32": return BitConverter.GetBytes(int.Parse(value, NumberStyles.Integer, inv));
                case "uint":   case "u32": return BitConverter.GetBytes(uint.Parse(value, NumberStyles.Integer, inv));
                case "long":   case "i64": return BitConverter.GetBytes(long.Parse(value, NumberStyles.Integer, inv));
                case "ulong":  case "u64": return BitConverter.GetBytes(ulong.Parse(value, NumberStyles.Integer, inv));
                case "float":  case "f32": return BitConverter.GetBytes(float.Parse(value, NumberStyles.Float, inv));
                case "double": case "f64": return BitConverter.GetBytes(double.Parse(value, NumberStyles.Float, inv));
                case "string": case "utf8": return Encoding.UTF8.GetBytes(value);
                default: throw new FormatException($"Unknown valueType '{valueType}'.");
            }
        }
    }
}
