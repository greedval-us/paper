using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Editor;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class NotebookArtImportTests
    {
        static NotebookArtSource Source() => NotebookArtImportTools.ParseAndValidate(File.ReadAllText(NotebookArtImportTools.SourcePath));

        [Test] public void CanonicalSourceCoversEveryPartEnemyPoseWallMaskAndRecoil()
        {
            var source = Source();
            Assert.That(source.Shapes.Count(shape => !string.IsNullOrEmpty(shape.TemplateId)), Is.EqualTo(15));
            Assert.That(source.Clips.Count(clip => clip.Id.StartsWith("enemy.", StringComparison.Ordinal)), Is.EqualTo(90));
            Assert.That(source.Recoils.Count, Is.EqualTo(7));
            for (int mask = 0; mask < 16; mask++) Assert.That(source.Shapes.Any(shape => shape.Id == "object.wall." + mask), Is.True);
            foreach (string id in new[] { "terrain.mountain", "terrain.ridge", "terrain.pine", "terrain.oak", "terrain.rock", "terrain.shrub",
                "object.tar", "object.repair", "object.entrance", "object.barricade-site", "ui.tool.tar", "ui.tool.repair" })
                Assert.That(source.Shapes.Any(shape => shape.Id == id), Is.True, id);
            foreach (var recoil in source.Recoils)
            {
                Assert.That(recoil.Sample(0), Is.EqualTo(0));
                Assert.That(recoil.Sample(recoil.Duration * .15f), Is.LessThan(0));
                Assert.That(recoil.Sample(recoil.Duration), Is.EqualTo(0));
            }
            foreach (var clip in source.Clips.Where(clip => clip.Id.EndsWith(".walk", StringComparison.Ordinal)))
            {
                Assert.That(clip.Loop, Is.True); Assert.That(clip.Frames.Select(frame => frame.ShapeId).Distinct().Count(), Is.GreaterThan(1));
                Assert.That(clip.Sample(clip.Duration).ShapeId, Is.EqualTo(clip.Sample(0).ShapeId));
            }
        }

        [Test] public void ImportValidationRejectsDuplicateUnknownFrameAndConcaveFillWithSourceAndId()
        {
            var source = Source(); source.Shapes.Add(source.Shapes[0]);
            var error = Assert.Throws<ArgumentException>(() => NotebookArtImportTools.Validate(source, "bad/notebook.json"));
            StringAssert.Contains("bad/notebook.json", error.Message); StringAssert.Contains("chassis.standard", error.Message);
            source = Source(); source.Clips[0].Frames[0].ShapeId = "missing.pose";
            error = Assert.Throws<ArgumentException>(() => NotebookArtImportTools.Validate(source));
            StringAssert.Contains(source.Clips[0].Id, error.Message);
            source = Source();
            source.Shapes[0].Marks.First(mark => mark.Primitive == NotebookArtPrimitive.Polygon).Points = new[] {
                new Vector2(0, 0), new Vector2(.5f, 0), new Vector2(.1f, .1f), new Vector2(.5f, .5f), new Vector2(0, .5f)
            };
            error = Assert.Throws<ArgumentException>(() => NotebookArtImportTools.Validate(source));
            StringAssert.Contains("convex", error.Message);
        }

        [Test] public void SelfIntersectingPentagramAndRepeatedPolygonVerticesAreRejected()
        {
            var source = Source();
            var polygon = source.Shapes[0].Marks.First(mark => mark.Primitive == NotebookArtPrimitive.Polygon);
            polygon.Points = Enumerable.Range(0, 5).Select(index => {
                float angle = index * 4 * Mathf.PI / 5;
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .4f;
            }).ToArray();
            var error = Assert.Throws<ArgumentException>(() => NotebookArtImportTools.Validate(source, "bad/pentagram.json"));
            StringAssert.Contains("bad/pentagram.json", error.Message);
            StringAssert.Contains(source.Shapes[0].Id, error.Message);
            StringAssert.Contains("simple convex", error.Message);
            polygon.Points = new[] { new Vector2(0, 0), new Vector2(.4f, 0), new Vector2(.4f, .4f), new Vector2(0, .4f), new Vector2(0, 0) };
            error = Assert.Throws<ArgumentException>(() => NotebookArtImportTools.Validate(source));
            StringAssert.Contains("distinct vertices", error.Message);
        }

        [Test] public void InvalidTemplateBindingsRejectCandidateAndRetainTheLastGoodTargetAndTheme()
        {
            WithTransientTarget(target => {
                string beforeOverrides = JsonUtility.ToJson(target.overrides), beforeDatabase = JsonUtility.ToJson(target.database.catalog);
                var theme = AssetDatabase.LoadAssetAtPath<NotebookVisualThemeAsset>(NotebookArtImportTools.ThemePath);
                Assert.That(theme, Is.Not.Null);
                string beforeTheme = JsonUtility.ToJson(theme.Data);
                for (int failure = 0; failure < 3; failure++)
                {
                    var source = Source(); var gun = source.Shapes.Single(shape => shape.Id == "weapon.gun");
                    if (failure == 0) gun.TemplateId = "template.weapon.piercer";
                    else if (failure == 1) gun.TemplateId = "";
                    else
                    {
                        var duplicate = JsonUtility.FromJson<NotebookArtShape>(JsonUtility.ToJson(gun));
                        duplicate.Id = "extra.gun"; source.Shapes.Add(duplicate);
                    }
                    var error = Assert.Throws<ArgumentException>(() => NotebookArtImportTools.CreateTargetOverrides(source, target));
                    StringAssert.Contains(failure == 2 ? "duplicate drawing template binding" : "template.weapon.gun", error.Message);
                    Assert.That(JsonUtility.ToJson(target.overrides), Is.EqualTo(beforeOverrides));
                    Assert.That(JsonUtility.ToJson(target.database.catalog), Is.EqualTo(beforeDatabase));
                    Assert.That(JsonUtility.ToJson(theme.Data), Is.EqualTo(beforeTheme));
                }
            });
        }

        [Test] public void ReimportPreservesEffectiveTargetTemplateMetadataAndDoesNotMutateSourceDatabase()
        {
            WithTransientTarget(target => {
                var source = Source();
                var customized = JsonUtility.FromJson<DrawingTemplateDefinitionInput>(JsonUtility.ToJson(
                    target.overrides.Templates.FirstOrDefault(template => template.Id == "template.weapon.gun")
                    ?? target.database.catalog.Templates.Single(template => template.Id == "template.weapon.gun")));
                customized.PivotX = .13f; customized.PivotY = -.09f; customized.Weight = 37;
                customized.DisplayName = "Override metadata fixture";
                target.overrides.Templates.RemoveAll(template => template.Id == customized.Id);
                target.overrides.Templates.Add(customized);
                string originalOverrides = JsonUtility.ToJson(target.overrides), originalDatabase = JsonUtility.ToJson(target.database.catalog);
                var first = NotebookArtImportTools.CreateTargetOverrides(source, target);
                Assert.That(JsonUtility.ToJson(target.overrides), Is.EqualTo(originalOverrides), "Candidate preparation is detached.");
                target.overrides = first;
                var second = NotebookArtImportTools.CreateTargetOverrides(source, target);
                Assert.That(JsonUtility.ToJson(second), Is.EqualTo(JsonUtility.ToJson(first)), "Reimport is idempotent.");
                var actual = second.Templates.Single(template => template.Id == customized.Id);
                Assert.That(actual.PivotX, Is.EqualTo(customized.PivotX)); Assert.That(actual.PivotY, Is.EqualTo(customized.PivotY));
                Assert.That(actual.Weight, Is.EqualTo(customized.Weight)); Assert.That(actual.DisplayName, Is.EqualTo(customized.DisplayName));
                Assert.That(actual.Revision, Is.EqualTo(3));
                Assert.That(actual.Contours.Sum(contour => contour.Points.Count), Is.EqualTo(
                    source.Shapes.Single(shape => shape.Id == "weapon.gun").Marks.Where(mark => mark.Reference).Sum(mark => mark.Points.Length)));
                Assert.That(JsonUtility.ToJson(target.database.catalog), Is.EqualTo(originalDatabase));
            });
        }

        static void WithTransientTarget(Action<BalancePresetAsset> assertion)
        {
            var persistent = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(NotebookArtImportTools.TargetPath);
            Assert.That(persistent, Is.Not.Null);
            var database = ScriptableObject.CreateInstance<ContentDatabaseAsset>();
            var target = ScriptableObject.CreateInstance<BalancePresetAsset>();
            try
            {
                database.catalog = persistent.database.catalog.CreateCopy(); target.database = database;
                target.overrides = JsonUtility.FromJson<ContentOverrides>(JsonUtility.ToJson(persistent.overrides));
                assertion(target);
            }
            finally { UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(database); }
        }

        [Test] public void ReferencePublicationExcludesDecorationAndRetainsHistoricalInputAndAnchors()
        {
            var source = Source();
            var historical = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>("Assets/PaperDefense/Configuration/Content/Historical.asset");
            Assert.That(historical, Is.Not.Null);
            foreach (var shape in source.Shapes.Where(shape => !string.IsNullOrEmpty(shape.TemplateId)))
            {
                var original = historical.database.catalog.Templates.Single(template => template.Id == shape.TemplateId);
                string before = JsonUtility.ToJson(original);
                var current = NotebookArtImportTools.CreateReference(shape, original);
                Assert.That(current.Revision, Is.EqualTo(3));
                Assert.That(current.PivotX, Is.EqualTo(original.PivotX)); Assert.That(current.PivotY, Is.EqualTo(original.PivotY));
                Assert.That(current.Weight, Is.EqualTo(original.Weight));
                Assert.That(current.Contours.Count, Is.EqualTo(shape.Marks.Count(mark => mark.Reference)));
                int contour = 0;
                foreach (var mark in shape.Marks.Where(mark => mark.Reference))
                {
                    Assert.That(current.Contours[contour].Points.Count, Is.EqualTo(mark.Points.Length));
                    for (int p = 0; p < mark.Points.Length; p++)
                    {
                        Assert.That(current.Contours[contour].Points[p].X, Is.EqualTo(mark.Points[p].x));
                        Assert.That(current.Contours[contour].Points[p].Y, Is.EqualTo(mark.Points[p].y));
                    }
                    contour++;
                }
                Assert.That(JsonUtility.ToJson(original), Is.EqualTo(before));
                Assert.That(shape.Marks.Count(mark => !mark.Reference), Is.GreaterThan(0));
            }
        }

        [Test] public void PublishedAssetAndPlayerCatalogShareRevisionAndContours()
        {
            var theme = AssetDatabase.LoadAssetAtPath<NotebookVisualThemeAsset>(NotebookArtImportTools.ThemePath);
            Assert.That(theme, Is.Not.Null, "Import notebook art before running the art acceptance suite.");
            NotebookArtImportTools.Validate(theme.Data, NotebookArtImportTools.ThemePath);
            var target = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(NotebookArtImportTools.TargetPath);
            Assert.That(target, Is.Not.Null);
            var expected = target.database.CreateSnapshot(target.overrides);
            const string playerPath = "Assets/PaperDefense/Configuration/Game/PlayerRules.asset";
            var player = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(playerPath);
            Assert.That(player, Is.Not.Null);
            var catalog = player.database.CreateSnapshot(player.overrides);
            var shapes = theme.Data.Shapes.Where(shape => !string.IsNullOrEmpty(shape.TemplateId)).ToArray();
            Assert.That(shapes, Is.Not.Empty, "Published notebook art must include drawing references.");
            foreach (var shape in shapes)
            {
                var reference = expected.GetTemplate(shape.TemplateId); var actual = catalog.GetTemplate(shape.TemplateId);
                Assert.That(actual.Revision, Is.EqualTo(reference.Revision), playerPath + " " + shape.Id);
                Assert.That(actual.Signature, Is.EqualTo(reference.Signature), playerPath + " " + shape.Id);
            }
        }

        [Test] public void InvalidCandidateCannotMutateTheLastGoodAsset()
        {
            var theme = AssetDatabase.LoadAssetAtPath<NotebookVisualThemeAsset>(NotebookArtImportTools.ThemePath);
            Assert.That(theme, Is.Not.Null);
            string before = JsonUtility.ToJson(theme.Data);
            var source = Source(); source.Shapes[0].Marks[0].Width = float.NaN;
            Assert.Throws<ArgumentException>(() => NotebookArtImportTools.Validate(source));
            Assert.That(JsonUtility.ToJson(theme.Data), Is.EqualTo(before));
        }

        [Test] public void EvenTheLargestThreeMountReferenceFitsDrawingBudgets()
        {
            var shapes = Source().Shapes.Where(shape => !string.IsNullOrEmpty(shape.TemplateId)).ToList();
            int Strokes(NotebookArtShape shape) => shape.Marks.Count(mark => mark.Reference);
            int Points(NotebookArtShape shape) => shape.Marks.Where(mark => mark.Reference).Sum(mark => mark.Points.Length);
            var chassis = shapes.Where(shape => shape.Id.StartsWith("chassis.", StringComparison.Ordinal));
            var weapons = shapes.Where(shape => shape.Id.StartsWith("weapon.", StringComparison.Ordinal));
            var modules = shapes.Where(shape => shape.Id.StartsWith("module.", StringComparison.Ordinal));
            // This deliberately exceeds the legal module selection, providing a conservative upper bound.
            int strokes = chassis.Max(Strokes) + TowerBlueprint.MaximumWeaponMounts * weapons.Max(Strokes) + modules.Sum(Strokes);
            int points = chassis.Max(Points) + TowerBlueprint.MaximumWeaponMounts * weapons.Max(Points) + modules.Sum(Points);
            Assert.That(strokes, Is.LessThanOrEqualTo(DrawingSnapshot.MaxStrokes));
            Assert.That(points, Is.LessThanOrEqualTo(DrawingSnapshot.MaxPoints));
        }
    }
}
