using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Presentation;

namespace PaperDefense.Tests
{
    public sealed class TemporaryProfileLaunchTests
    {
        static string Resolve(bool configured, params string[] args)
        {
            var method = typeof(PaperDefenseBootstrap).GetMethod("ResolveTemporaryProfilePath",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null,"Explicit temporary stores must be resolved before any default profile can be loaded.");
            try { return (string)method.Invoke(null,new object[] { configured,args,Path.GetTempPath() }); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }
        [Test] public void ExplicitTemporaryStoreIsCanonicalAndMemoryIsolationWins()
        {
            var path = Path.Combine(Path.GetTempPath(),"PaperDefense-release-acceptance","profile.json");
            Assert.That(Resolve(false,"game.exe","-paper-profile-path",path),Is.EqualTo(Path.GetFullPath(path)));
            Assert.That(Resolve(false,"game.exe","-paper-profile-path",path,"-paper-memory-only"),Is.Null);
            Assert.That(Resolve(true,"game.exe","-paper-profile-path",path),Is.Null);
            Assert.That(Resolve(false,"game.exe"),Is.Null);
        }
        [Test] public void InvalidStoreOrProbeCannotFallBackToPersonalProfile()
        {
            var sibling = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+"-other/profile.json";
            Assert.Throws<ArgumentException>(()=>Resolve(false,"game.exe","-paper-profile-path",sibling));
            Assert.Throws<ArgumentException>(()=>Resolve(false,"game.exe","-paper-profile-path"));
            Assert.Throws<ArgumentException>(()=>Resolve(false,"game.exe","-paper-release-probe","report.json"));
            Assert.Throws<ArgumentException>(()=>Resolve(false,"game.exe","-paper-release-probe","-paper-memory-only"));
        }
    }
}
