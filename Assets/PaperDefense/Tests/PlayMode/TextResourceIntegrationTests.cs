using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class TextResourceIntegrationTests
    {
        [Test]
        public void InstanceOverrideChangesWorkshopControlsAndRealCoreReasonWithoutLeakingToOtherUi()
        {
            var asset=ScriptableObject.CreateInstance<TextResourcesAsset>();
            var first=new GameObject("Text override fixture",typeof(RectTransform));
            var second=new GameObject("Independent text fixture",typeof(RectTransform));
            try
            {
                foreach(var pair in TextResourcesAsset.Default.Entries)asset.entries.Add(new TextResourcesAsset.Entry(pair.Key,pair.Value));
                Replace(asset,"wizard.new_name","WORKSHOP SENTINEL");
                Replace(asset,"copy.a8ce8499831aa26c","CORE SENTINEL");
                var resolver=asset.CreateResolver();
                var label=new WorkshopUi(resolver).Label(first.transform,"Resolved workshop control",UserText.Message("wizard.new_name"),new Rect(0,0,300,60),20);
                var input=first.AddComponent<DrawingInput>();input.TextResolver=resolver;
                var session=new GameSession(GameBalance.CreateDefault());
                Assert.That(session.TryPaint(new Cell(-1,-1),InkColor.None,out string reason),Is.False);
                input.SetMessage(reason);Assert.That(input.Message,Is.EqualTo("CORE SENTINEL"));
                Assert.That(label.text,Is.EqualTo("WORKSHOP SENTINEL"));
                Assert.That(label.text.Contains("\u001ePD1:"),Is.False);
                var independent=new WorkshopUi().Label(second.transform,"Independent workshop control",UserText.Message("wizard.new_name"),new Rect(0,0,300,60),20);
                Assert.That(independent.text,Does.Not.Contain("SENTINEL"));
            }
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(asset);}
        }

        [Test]
        public void AssetSnapshotRejectsDuplicateKeysAndMalformedCompositeTemplates()
        {
            var asset = ScriptableObject.CreateInstance<TextResourcesAsset>();
            try
            {
                asset.entries.Add(new TextResourcesAsset.Entry("test", "{0:0.00}"));
                asset.entries.Add(new TextResourcesAsset.Entry("test", "duplicate"));
                Assert.Throws<ArgumentException>(() => asset.CreateResolver());
                asset.entries.RemoveAt(1);
                asset.entries[0].value = "{0";
                Assert.Throws<FormatException>(() => asset.CreateResolver());
                asset.entries[0].value = "{{literal}} {0,6:N1}";
                Assert.That(asset.CreateResolver().Resolve(UserText.Message("test", 2.5)), Is.EqualTo("{literal}    2,5"));
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void TextResourceChangesDoNotAlterPlayerNameDrawingHashOrAssessment()
        {
            var blueprint = TowerBlueprint.NewDraft("text-invariant", "Моё имя {0}", ChassisKind.Standard, StructureKind.Gun);
            string hash = blueprint.Drawing.GeometryHash;
            float quality = blueprint.Assessment.Quality;
            var first = new UserTextResolver(new Dictionary<string, string> { { "drawing.label", "Original {0}" } });
            var second = new UserTextResolver(new Dictionary<string, string> { { "drawing.label", "Sentinel {0}" } });
            Assert.That(first.Resolve(UserText.Message("drawing.label", blueprint.Name)), Is.EqualTo("Original Моё имя {0}"));
            Assert.That(second.Resolve(UserText.Message("drawing.label", blueprint.Name)), Is.EqualTo("Sentinel Моё имя {0}"));
            Assert.That(blueprint.Name, Is.EqualTo("Моё имя {0}"));
            Assert.That(blueprint.Drawing.GeometryHash, Is.EqualTo(hash));
            Assert.That(blueprint.Assessment.Quality, Is.EqualTo(quality));
        }

        [Test]
        public void IncompatibleOverrideIsRejectedBeforeProfileStoreLoad()
        {
            var asset = ScriptableObject.CreateInstance<TextResourcesAsset>();
            var root = new GameObject("Invalid text preparation fixture"); root.SetActive(false);
            try
            {
                foreach (var pair in TextResourcesAsset.Default.Entries) asset.entries.Add(new TextResourcesAsset.Entry(pair.Key, pair.Value));
                Replace(asset, "copy.159b7c81e8a7413c", "Unexpected argument {0}");
                var store = new CountingTextStore();
                var bootstrap = root.AddComponent<PaperDefenseBootstrap>();
                bootstrap.textResources = asset;
                bootstrap.ProfileStore = store;
                bootstrap.ArgumentProvider = () => new[] { "PaperDefense.exe" };
                LogAssert.Expect(LogType.Exception, new Regex("ArgumentException: Incompatible text template: copy\\.159b7c81e8a7413c"));
                root.SetActive(true);
                Assert.That(store.LoadCalls, Is.Zero);
                Assert.That(bootstrap.Controller, Is.Null);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }

        [TestCase("Q", "FormatException: No semantic argument type supports this format\\.")]
        [TestCase("D", "ArgumentException: Incompatible text template: copy\\.034c4ac46d600a47")]
        public void InvalidNumericOverrideIsRejectedBeforeProfileStoreLoad(string format, string diagnostic)
        {
            var asset = ScriptableObject.CreateInstance<TextResourcesAsset>();
            var root = new GameObject("Invalid numeric text fixture"); root.SetActive(false);
            try
            {
                foreach (var pair in TextResourcesAsset.Default.Entries) asset.entries.Add(new TextResourcesAsset.Entry(pair.Key, pair.Value));
                Replace(asset, "copy.034c4ac46d600a47", "Quality {0:" + format + "}");
                var store = new CountingTextStore();
                var bootstrap = root.AddComponent<PaperDefenseBootstrap>();
                bootstrap.textResources = asset;
                bootstrap.ProfileStore = store;
                bootstrap.ArgumentProvider = () => new[] { "PaperDefense.exe" };
                LogAssert.Expect(LogType.Exception, new Regex(diagnostic));
                root.SetActive(true);
                Assert.That(store.LoadCalls, Is.Zero);
                Assert.That(bootstrap.Controller, Is.Null);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }

        sealed class CountingTextStore : IWorkshopProfileStore
        {
            public int LoadCalls;
            public ProfileLoadResult Load()
            {
                LoadCalls++;
                return new ProfileLoadResult(new WorkshopProfile(), ProfileLoadSource.Primary, "In-memory test store", true);
            }
            public ProfileSaveResult Save(WorkshopProfile profile) => new ProfileSaveResult(true, "In-memory test write");
        }

        static string Key(string template) => TextResourcesAsset.Default.Entries.Single(pair => pair.Value == template).Key;
        static void Replace(TextResourcesAsset asset, string key, string value) => asset.entries.Single(entry => entry.key == key).value = value;
    }
}
