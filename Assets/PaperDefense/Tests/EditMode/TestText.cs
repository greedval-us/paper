using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    /// <summary>Pure Core tests read the same canonical resources without a Unity dependency.</summary>
    internal static class TestText
    {
        static readonly Lazy<UserTextResolver> Snapshot = new Lazy<UserTextResolver>(Load);
        public static UserTextResolver Resources => Snapshot.Value;
        public static string Resolve(string value)
        {
            string result = Snapshot.Value.Resolve(value);
            if (result.Contains("[missing:") || result.Contains("[invalid:")) throw new InvalidOperationException("Text resource contract failed: " + result);
            return result;
        }
        public static string ExceptionMessage<T>(TestDelegate action) where T : Exception => Resolve(Assert.Throws<T>(action).Message);
        static UserTextResolver Load()
        {
            string manifest = FindManifest();
            string json = File.ReadAllText(manifest, Encoding.UTF8);
            var entries = new List<KeyValuePair<string, string>>();
            int position = 0;
            Skip(json, ref position);
            Expect(json, ref position, '{');
            while (true)
            {
                Skip(json, ref position);
                if (json[position] == '}') break;
                string key = ReadString(json, ref position);
                Skip(json, ref position); Expect(json, ref position, ':'); Skip(json, ref position);
                entries.Add(new KeyValuePair<string, string>(key, ReadString(json, ref position)));
                Skip(json, ref position);
                if (json[position] == ',') { position++; continue; }
                if (json[position] != '}') throw new FormatException("Invalid canonical text manifest.");
            }
            return new UserTextResolver(entries, CultureInfo.GetCultureInfo("ru-RU"));
        }
        static string FindManifest()
        {
            string[] starts = { Directory.GetCurrentDirectory(), Path.GetDirectoryName(typeof(TestText).Assembly.Location) };
            foreach (string start in starts)
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    string path = Path.Combine(directory.FullName, "Tools", "user-texts.json");
                    if (File.Exists(path)) return path;
                }
            throw new FileNotFoundException("Cannot locate Tools/user-texts.json for Core text assertions.");
        }
        static void Skip(string json, ref int position) { while (position < json.Length && char.IsWhiteSpace(json[position])) position++; }
        static void Expect(string json, ref int position, char expected)
        { if (position >= json.Length || json[position++] != expected) throw new FormatException("Invalid canonical text manifest."); }
        static string ReadString(string json, ref int position)
        {
            Expect(json, ref position, '"');
            var result = new StringBuilder();
            while (position < json.Length)
            {
                char value = json[position++];
                if (value == '"') return result.ToString();
                if (value != '\\') { result.Append(value); continue; }
                char escaped = json[position++];
                switch (escaped)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'u': result.Append((char)int.Parse(json.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); position += 4; break;
                    default: throw new FormatException("Invalid JSON string escape in text manifest.");
                }
            }
            throw new FormatException("Unterminated text manifest string.");
        }
    }
}
