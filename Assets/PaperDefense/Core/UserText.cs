using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace PaperDefense.Core
{
    /// <summary>Semantic messages travel through the existing reason-string APIs without choosing a language.</summary>
    public static class UserText
    {
        internal const string Prefix = "\u001ePD1:";
        internal const char End = '\u001f';
        public static string Message(string key, params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A text key is required.", nameof(key));
            arguments = arguments ?? Array.Empty<object>();
            using (var data = new MemoryStream())
            {
                using (var writer = new BinaryWriter(data, Encoding.UTF8, true))
                {
                    writer.Write(key); writer.Write(arguments.Length);
                    foreach (object value in arguments) Write(writer, value);
                }
                return Prefix + Convert.ToBase64String(data.ToArray()) + End;
            }
        }
        static void Write(BinaryWriter writer, object value)
        {
            if (value == null) { writer.Write((byte)0); return; }
            if (value is string text) { writer.Write((byte)1); writer.Write(text); return; }
            if (value is bool flag) { writer.Write((byte)2); writer.Write(flag); return; }
            if (value is sbyte || value is byte || value is short || value is ushort || value is int || value is long)
            { writer.Write((byte)3); writer.Write(Convert.ToInt64(value, CultureInfo.InvariantCulture)); return; }
            if (value is uint || value is ulong)
            { writer.Write((byte)4); writer.Write(Convert.ToUInt64(value, CultureInfo.InvariantCulture)); return; }
            if (value is float single) { writer.Write((byte)5); writer.Write(single); return; }
            if (value is double number) { writer.Write((byte)6); writer.Write(number); return; }
            if (value is decimal precise) { writer.Write((byte)7); writer.Write(precise); return; }
            if (value is DateTime date) { writer.Write((byte)8); writer.Write(date.ToBinary()); return; }
            if (value is char character) { writer.Write((byte)1); writer.Write(character.ToString()); return; }
            if (value is Enum enumeration) { writer.Write((byte)1); writer.Write(enumeration.ToString()); return; }
            // Domain facts such as Cell are immutable display values, never culture-selected templates.
            if (value is Cell cell) { writer.Write((byte)1); writer.Write(cell.ToString()); return; }
            throw new ArgumentException("Unsupported semantic text argument type: " + value.GetType().FullName, nameof(value));
        }
        internal static object Read(BinaryReader reader)
        {
            switch (reader.ReadByte())
            {
                case 0: return null;
                case 1: return reader.ReadString();
                case 2: return reader.ReadBoolean();
                case 3: return reader.ReadInt64();
                case 4: return reader.ReadUInt64();
                case 5: return reader.ReadSingle();
                case 6: return reader.ReadDouble();
                case 7: return reader.ReadDecimal();
                case 8: return DateTime.FromBinary(reader.ReadInt64());
                default: throw new FormatException("Invalid semantic text argument tag.");
            }
        }
    }

    /// <summary>An owned, validated snapshot. It never reads Resources or changes the process culture.</summary>
    public sealed class UserTextResolver
    {
        readonly IReadOnlyDictionary<string, string> entries;
        readonly Dictionary<string, TemplateContract> contracts = new Dictionary<string, TemplateContract>(StringComparer.Ordinal);
        readonly CultureInfo culture;
        public IReadOnlyDictionary<string, string> Entries => entries;
        public CultureInfo Culture => culture;
        public UserTextResolver(IEnumerable<KeyValuePair<string, string>> entries, CultureInfo culture = null)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            this.culture = CultureInfo.ReadOnly((CultureInfo)(culture ?? CultureInfo.GetCultureInfo("ru-RU")).Clone());
            var owned = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) throw new ArgumentException("Text keys and templates must be present.", nameof(entries));
                if (owned.ContainsKey(entry.Key)) throw new ArgumentException("Duplicate text key: " + entry.Key, nameof(entries));
                contracts.Add(entry.Key, Validate(entry.Key, entry.Value));
                owned.Add(entry.Key, entry.Value);
            }
            this.entries = new ReadOnlyDictionary<string, string>(owned);
        }
        sealed class TemplateContract
        {
            public int ArgumentCount;
            public readonly HashSet<int> FormattedArguments = new HashSet<int>();
            public readonly Dictionary<int, FormatKinds> ArgumentFormats = new Dictionary<int, FormatKinds>();
        }
        [Flags]
        enum FormatKinds { None = 0, SignedInteger = 1, UnsignedInteger = 2, Single = 4, Double = 8, Decimal = 16, DateTime = 32 }
        static FormatKinds SupportedFormats(string format)
        {
            FormatKinds kinds = FormatKinds.None;
            if (AcceptsFormat(1L, format)) kinds |= FormatKinds.SignedInteger;
            if (AcceptsFormat(1UL, format)) kinds |= FormatKinds.UnsignedInteger;
            if (AcceptsFormat(1f, format)) kinds |= FormatKinds.Single;
            if (AcceptsFormat(1d, format)) kinds |= FormatKinds.Double;
            if (AcceptsFormat(1m, format)) kinds |= FormatKinds.Decimal;
            if (AcceptsFormat(new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc), format)) kinds |= FormatKinds.DateTime;
            return kinds;
        }
        static bool AcceptsFormat(IFormattable value, string format)
        {
            try { value.ToString(format, CultureInfo.InvariantCulture); return true; }
            catch (FormatException) { return false; }
        }
        static TemplateContract Validate(string key, string template)
        {
            var contract = new TemplateContract();
            int maximum = -1;
            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] != '{') continue;
                if (i + 1 < template.Length && template[i + 1] == '{') { i++; continue; }
                int start = i + 1, end = start;
                while (end < template.Length && char.IsDigit(template[end])) end++;
                if (end == start || !int.TryParse(template.Substring(start, end - start), out int index) || index > 1024)
                    throw new FormatException("Invalid text template: " + key);
                maximum = Math.Max(maximum, index);
                int closing = template.IndexOf('}', end);
                if (closing >= end && template.Substring(end, closing - end).IndexOf(':') >= 0)
                    contract.FormattedArguments.Add(index);
            }
            var values = new object[maximum + 1];
            for (int i = 0; i < values.Length; i++) values[i] = new FormatProbe(contract, i);
            try { string.Format(CultureInfo.InvariantCulture, template, values); }
            catch (FormatException error) { throw new FormatException("Invalid text template: " + key, error); }
            contract.ArgumentCount = maximum + 1;
            return contract;
        }
        sealed class FormatProbe : IFormattable
        {
            readonly TemplateContract contract;
            readonly int index;
            public FormatProbe(TemplateContract contract, int index) { this.contract = contract; this.index = index; }
            public string ToString(string format, IFormatProvider provider)
            {
                if (!contract.FormattedArguments.Contains(index)) return "0";
                FormatKinds supported = SupportedFormats(format);
                if (contract.ArgumentFormats.TryGetValue(index, out FormatKinds previous)) supported &= previous;
                if (supported == FormatKinds.None) throw new FormatException("No semantic argument type supports this format.");
                contract.ArgumentFormats[index] = supported;
                return "0";
            }
            public override string ToString() => ToString(null, CultureInfo.InvariantCulture);
        }
        public void RequireKeys(IEnumerable<string> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            foreach (string key in keys) if (!entries.ContainsKey(key)) throw new ArgumentException("Missing required text key: " + key, nameof(keys));
        }
        public void RequireCompatible(UserTextResolver canonical)
        {
            if (canonical == null) throw new ArgumentNullException(nameof(canonical));
            RequireKeys(canonical.entries.Keys);
            foreach (var pair in canonical.contracts)
            {
                TemplateContract candidate = contracts[pair.Key], required = pair.Value;
                if (candidate.ArgumentCount != required.ArgumentCount || !candidate.FormattedArguments.SetEquals(required.FormattedArguments))
                    throw new ArgumentException("Incompatible text template: " + pair.Key, nameof(canonical));
                foreach (var format in required.ArgumentFormats)
                    if ((candidate.ArgumentFormats[format.Key] & format.Value) != format.Value)
                        throw new ArgumentException("Incompatible text template: " + pair.Key, nameof(canonical));
            }
        }
        public void RequireContract(string key, int argumentCount, params int[] formattedArguments)
        {
            if (!contracts.TryGetValue(key, out TemplateContract candidate)) throw new ArgumentException("Missing required text key: " + key, nameof(key));
            if (candidate.ArgumentCount != argumentCount || !candidate.FormattedArguments.SetEquals(formattedArguments ?? Array.Empty<int>()))
                throw new ArgumentException("Incompatible text template: " + key, nameof(key));
        }
        public void RequireFormatContract(string key, int argumentIndex, params string[] formats)
        {
            if (!contracts.TryGetValue(key, out TemplateContract candidate)) throw new ArgumentException("Missing required text key: " + key, nameof(key));
            if (formats == null || formats.Length == 0) throw new ArgumentException("A canonical argument format is required.", nameof(formats));
            FormatKinds required = SupportedFormats(formats[0]);
            for (int i = 1; i < formats.Length; i++) required &= SupportedFormats(formats[i]);
            if (required == FormatKinds.None || !candidate.ArgumentFormats.TryGetValue(argumentIndex, out FormatKinds actual) || (actual & required) != required)
                throw new ArgumentException("Incompatible text template: " + key, nameof(key));
        }
        public string Resolve(string value) => Resolve(value ?? "", 0);
        string Resolve(string value, int depth)
        {
            if (depth > 32) return "[invalid:recursive]";
            int start = value.IndexOf(UserText.Prefix, StringComparison.Ordinal);
            if (start < 0) return value;
            var output = new StringBuilder(); int copied = 0;
            while (start >= 0)
            {
                int end = value.IndexOf(UserText.End, start + UserText.Prefix.Length);
                if (end < 0) break;
                output.Append(value, copied, start - copied);
                output.Append(ResolveToken(value.Substring(start + UserText.Prefix.Length, end - start - UserText.Prefix.Length), depth));
                copied = end + 1;
                start = value.IndexOf(UserText.Prefix, copied, StringComparison.Ordinal);
            }
            output.Append(value, copied, value.Length - copied);
            return output.ToString();
        }
        string ResolveToken(string payload, int depth)
        {
            string key = "token";
            try
            {
                byte[] bytes = Convert.FromBase64String(payload);
                using (var data = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(data, Encoding.UTF8))
                {
                    key = reader.ReadString();
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 1024) return "[invalid:" + key + "]";
                    var arguments = new object[count];
                    for (int i = 0; i < count; i++)
                    {
                        object argument = UserText.Read(reader);
                        arguments[i] = argument is string text ? Resolve(text, depth + 1) : argument;
                    }
                    if (data.Position != data.Length) return "[invalid:" + key + "]";
                    if (!entries.TryGetValue(key, out string template)) return "[missing:" + key + "]";
                    TemplateContract contract = contracts[key];
                    if (count != contract.ArgumentCount) return "[invalid:" + key + "]";
                    foreach (int index in contract.FormattedArguments)
                        if (!(arguments[index] is IFormattable)) return "[invalid:" + key + "]";
                    return string.Format(culture, template, arguments);
                }
            }
            catch (Exception error) when (error is FormatException || error is IOException || error is ArgumentException)
            { return "[invalid:" + key + "]"; }
        }
    }
}
