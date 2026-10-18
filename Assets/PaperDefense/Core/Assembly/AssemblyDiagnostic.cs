using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed class AssemblyDiagnostic
    {
        public string Code { get; }
        public string Path { get; }
        public IReadOnlyList<string> Arguments { get; }
        public AssemblyDiagnostic(string code, string path, params string[] arguments)
        {
            Code = code; Path = path;
            Arguments = Array.AsReadOnly((string[])(arguments ?? Array.Empty<string>()).Clone());
        }
    }

    public sealed class AssemblyCatalogException : ArgumentException
    {
        public IReadOnlyList<AssemblyDiagnostic> Diagnostics { get; }
        public AssemblyCatalogException(IEnumerable<AssemblyDiagnostic> diagnostics) : base("Invalid assembly catalog.")
        { Diagnostics = new List<AssemblyDiagnostic>(diagnostics).AsReadOnly(); }
        public static AssemblyCatalogException At(string code, string path, params string[] args)
            => new AssemblyCatalogException(new[] { new AssemblyDiagnostic(code, path, args) });
    }
}
