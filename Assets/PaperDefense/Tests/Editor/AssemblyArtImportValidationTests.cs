using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Editor;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyArtImportValidationTests
    {
        [TestCase("visual_revision")]
        [TestCase("both_revisions")]
        [TestCase("both_identities")]
        [TestCase("both_categories")]
        [TestCase("socket_mismatch")]
        [TestCase("socket_schema")]
        [TestCase("collision_mode")]
        [TestCase("layout_id")]
        [TestCase("layout_dimension")]
        [TestCase("layout_overlap")]
        [TestCase("layout_missing_white")]
        public void MalformedFullSource_RejectsBeforePublishingAndPreservesTheAcceptedResources(string mutation)
        {
            string originalSource = File.ReadAllText(NotebookSpriteImportTools.AssemblyPath);
            const string layoutPath = "Tools/ArtAssembly13/packing-4096.json";
            string originalLayout = File.ReadAllText(layoutPath);
            var paths = new[] { NotebookSpriteImportTools.AtlasPath, NotebookSpriteImportTools.CatalogPath,
                NotebookSpriteImportTools.MountsPath, NotebookSpriteImportTools.ThemePath };
            var acceptedBytes = paths.Select(File.ReadAllBytes).ToArray();
            var acceptedGuids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var accepted = AssemblyVisualComposer.Compose(AssemblyVisualPilotTests.Pilot(), new AssemblyVisualPose());
            var transforms = accepted.Fragments.Select(f => (f.PartId, f.SpriteId, f.SlotIndex, f.Layer, f.Origin, f.Scale, f.Radians)).ToArray();
            var source = JsonUtility.FromJson<AssemblyVisualComposer.Manifest>(originalSource);
            var visual = source.visualParts.Single(p => p.partId == "head.long");
            var fit = source.fits.Parts.Single(p => p.Id == "head.long");
            var layout = JObject.Parse(originalLayout);
            if (mutation.StartsWith("layout_", StringComparison.Ordinal))
            {
                var rectangles = (JArray)layout["rectangles"];
                switch (mutation)
                {
                    case "layout_id": rectangles[0]["id"] = "unregistered.sprite"; break;
                    case "layout_dimension": rectangles[0]["width"] = (int)rectangles[0]["width"] + 1; break;
                    case "layout_overlap": rectangles[1]["x"] = rectangles[0]["x"].DeepClone(); rectangles[1]["y"] = rectangles[0]["y"].DeepClone(); break;
                    default: rectangles.RemoveAt(rectangles.Count - 1); break;
                }
            }
            else switch (mutation)
            {
                case "visual_revision": visual.revision = 99; break;
                case "both_revisions": visual.revision = fit.Revision = 99; break;
                case "both_identities": visual.partId = fit.Id = "head.unregistered"; break;
                case "both_categories": visual.category = fit.Category = ComponentCategory.Sight; break;
                case "socket_mismatch": fit.AttachType = "wrong.socket"; break;
                case "socket_schema": fit.SocketSchemaVersion = 99; break;
                default: source.visualParts.Single(p => p.partId == "frame.heavy").fragments[0].collisionMode = "solidd"; break;
            }
            Exception error = null;
            try
            {
                File.WriteAllText(NotebookSpriteImportTools.AssemblyPath, JsonUtility.ToJson(source, true));
                File.WriteAllText(layoutPath, layout.ToString());
                try { NotebookSpriteImportTools.Rebuild(); }
                catch (Exception rejected) { error = rejected; }
                Assert.That(error, Is.TypeOf<FormatException>().Or.TypeOf<AssemblyCatalogException>(), mutation + " must fail validation, before resource publication.");
                for (int i = 0; i < paths.Length; i++)
                {
                    Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(acceptedBytes[i]), paths[i]);
                    Assert.That(AssetDatabase.AssetPathToGUID(paths[i]), Is.EqualTo(acceptedGuids[i]), paths[i]);
                }
                var current = AssemblyVisualComposer.Compose(AssemblyVisualPilotTests.Pilot(), new AssemblyVisualPose());
                Assert.That(current.Fragments.Select(f => (f.PartId, f.SpriteId, f.SlotIndex, f.Layer, f.Origin, f.Scale, f.Radians)), Is.EqualTo(transforms));
            }
            finally
            {
                // Negative fixtures never leave their source or a wrongly accepted publication behind.
                File.WriteAllText(NotebookSpriteImportTools.AssemblyPath, originalSource);
                File.WriteAllText(layoutPath, originalLayout);
                string staging = Path.GetFullPath(Path.Combine("Temp", "PaperDefenseArtNegative", Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(staging);
                try
                {
                    for (int i = 0; i < paths.Length; i++)
                    {
                        if (File.ReadAllBytes(paths[i]).SequenceEqual(acceptedBytes[i])) continue;
                        string file = Path.Combine(staging, i + ".accepted"); File.WriteAllBytes(file, acceptedBytes[i]);
                        FileUtil.ReplaceFile(file, Path.GetFullPath(paths[i]));
                        AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceSynchronousImport);
                        if (File.Exists(file)) File.Delete(file);
                    }
                }
                finally { Directory.Delete(staging, false); }
                NotebookVisualThemeAsset.ResetDefaultCache(); NotebookSpriteCatalog.ResetCache();
                NotebookMountLayout.ResetCache(); AssemblyVisualComposer.ResetCache();
            }
        }
    }
}
