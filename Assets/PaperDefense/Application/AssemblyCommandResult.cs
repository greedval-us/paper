using System;
using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed class AssemblyCommandResult
    {
        public bool Success { get; }
        public string Code { get; }
        public string ResultId { get; }
        public IReadOnlyList<AssemblyDiagnostic> Diagnostics { get; }
        internal AssemblyCommandResult(bool success, string code, string resultId = null, IEnumerable<AssemblyDiagnostic> diagnostics = null)
        {
            Success = success; Code = code; ResultId = resultId;
            Diagnostics = new List<AssemblyDiagnostic>(diagnostics ?? Array.Empty<AssemblyDiagnostic>()).AsReadOnly();
        }
        internal static AssemblyCommandResult Ok(string id) => new AssemblyCommandResult(true, "ok", id);
        internal static AssemblyCommandResult Fail(string code, string path = "command", params string[] args)
            => new AssemblyCommandResult(false, code, diagnostics: new[] { new AssemblyDiagnostic(code, path, args) });
    }
}
