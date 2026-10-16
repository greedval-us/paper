using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Editor;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class NotebookSpriteImportTests
    {
        static NotebookSpriteImportTools.SourceManifest Fixture()
            => new NotebookSpriteImportTools.SourceManifest {
                revision = "test-blue-pen",
                sprites = new[] { new NotebookSpriteImportTools.SpriteSource {
                    id = "test.pose", source = "ArtSource/Notebook/NewStyle/EnemySprites.png",
                    x = 2, y = 3, width = 10, height = 12,
                    bounds = new NotebookSpriteImportTools.SpriteBounds { x = -.5f, y = -.6f, width = 1, height = 1.2f }
                } },
                aliases = new[] { new NotebookSpriteImportTools.SpriteAlias { id = "test.alias", alias = "test.pose" } }
            };

        [Test] public void SourceCropsMustStayInsideTheirPngBeforeAtlasPublication()
        {
            var source = Fixture();
            Assert.DoesNotThrow(() => NotebookSpriteImportTools.ValidateSource(source, _ => new Vector2Int(16, 16)));
            source.sprites[0].width = 15;
            var error = Assert.Throws<ArgumentException>(() => NotebookSpriteImportTools.ValidateSource(source, _ => new Vector2Int(16, 16)));
            StringAssert.Contains("test.pose", error.Message);
            StringAssert.Contains("crop", error.Message);
        }

        [Test] public void AliasCyclesUnknownTargetsAndDuplicateSemanticIdsAreRejected()
        {
            var source = Fixture();
            source.aliases[0].alias = "missing.pose";
            Assert.Throws<ArgumentException>(() => NotebookSpriteImportTools.ValidateSource(source));
            source.aliases = new[] {
                new NotebookSpriteImportTools.SpriteAlias { id = "test.a", alias = "test.b" },
                new NotebookSpriteImportTools.SpriteAlias { id = "test.b", alias = "test.a" }
            };
            Assert.Throws<ArgumentException>(() => NotebookSpriteImportTools.ValidateSource(source));
            source.aliases = new[] { new NotebookSpriteImportTools.SpriteAlias { id = "test.pose", alias = "test.pose" } };
            Assert.Throws<ArgumentException>(() => NotebookSpriteImportTools.ValidateSource(source));
        }

        [Test] public void SourceRejectsNonFiniteTransformsAndPathsOutsideTheProject()
        {
            var source = Fixture(); source.sprites[0].bounds.width = float.NaN;
            Assert.Throws<ArgumentException>(() => NotebookSpriteImportTools.ValidateSource(source));
            source = Fixture(); source.sprites[0].source = "../outside.png";
            Assert.Throws<ArgumentException>(() => NotebookSpriteImportTools.ValidateSource(source));
        }

        [Test] public void CanonicalSourceBindsEveryAnimationShapeWithoutLegacyDecorativeMarks()
        {
            var source = NotebookSpriteImportTools.ParseSource(File.ReadAllText(NotebookSpriteImportTools.SourcePath));
            var ids = new HashSet<string>(source.sprites.Select(sprite => sprite.id).Concat(source.aliases.Select(alias => alias.id)), StringComparer.Ordinal);
            var animation = NotebookSpriteImportTools.ParseAnimation(File.ReadAllText(NotebookSpriteImportTools.AnimationPath), ids);
            Assert.That(animation.Shapes, Is.Not.Empty);
            Assert.That(animation.Shapes.All(shape => shape.Marks == null || shape.Marks.Count == 0), Is.True);
            Assert.That(animation.Clips.Count(clip => clip.Id.StartsWith("enemy.", StringComparison.Ordinal)), Is.EqualTo(90));
            Assert.That(animation.Recoils.Count, Is.EqualTo(7));
            foreach (string id in new[] { "logo.home", "logo.settings", "tower.gun", "tower.double", "tower.piercer", "tower.mortar", "tower.fan", "hero.castle" })
                Assert.That(ids.Contains(id), Is.True, id);
        }

        [Test] public void MissingAnimationBindingsAndLegacyMarksCannotReplaceThePublishedTheme()
        {
            var source = NotebookSpriteImportTools.ParseSource(File.ReadAllText(NotebookSpriteImportTools.SourcePath));
            var ids = new HashSet<string>(source.sprites.Select(sprite => sprite.id).Concat(source.aliases.Select(alias => alias.id)), StringComparer.Ordinal);
            string json = File.ReadAllText(NotebookSpriteImportTools.AnimationPath);
            var animation = NotebookSpriteImportTools.ParseAnimation(json, ids);
            string before = JsonUtility.ToJson(NotebookVisualThemeAsset.Default.Data);
            ids.Remove(animation.Shapes[0].Id);
            Assert.Throws<ArgumentException>(() => NotebookSpriteImportTools.ValidateAnimation(animation, ids));
            ids.Add(animation.Shapes[0].Id);
            if (animation.Shapes[0].Marks == null) animation.Shapes[0].Marks = new List<NotebookArtMark>();
            animation.Shapes[0].Marks.Add(new NotebookArtMark());
            Assert.Throws<ArgumentException>(() => NotebookSpriteImportTools.ValidateAnimation(animation, ids));
            Assert.That(JsonUtility.ToJson(NotebookVisualThemeAsset.Default.Data), Is.EqualTo(before));
        }
    }
}
