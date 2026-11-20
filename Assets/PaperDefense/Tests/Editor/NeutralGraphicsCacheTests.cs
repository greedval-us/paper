using NUnit.Framework;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class NeutralGraphicsCacheTests
    {
        [Test] public void ResetDestroysTheOwnedRuntimeThemeAndLeavesCallerOwnedThemesAlive()
        {
            var custom = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            var owned = NotebookVisualThemeAsset.Default;
            try
            {
                NotebookVisualThemeAsset.ResetDefaultCache();
                Assert.That(owned == null, Is.True, "Reset must release the native HideAndDontSave theme, not only its managed reference.");
                Assert.That(custom != null, Is.True, "The cache owns only its runtime default.");
                Assert.That(NotebookVisualThemeAsset.Default, Is.Not.SameAs(owned));
            }
            finally { Object.DestroyImmediate(custom); }
        }

        [Test] public void ReplacingDefaultGeometryRefitsTheSameSymbolId()
        {
            const string id = "object.castle";
            var theme = NotebookVisualThemeAsset.Default;
            var original = theme.Data;
            Assert.That(NotebookSpriteCatalog.TryGetBounds(id, out var before), Is.True);
            var replacement = new NotebookArtSource { Palette = original.Palette };
            replacement.Shapes.Add(new NotebookArtShape { Id = id, Marks = {
                new NotebookArtMark { Primitive = NotebookArtPrimitive.Polygon, Channel = NotebookInkChannel.Navy,
                    Points = new[] { new Vector2(2, -.5f), new Vector2(6, -.5f), new Vector2(6, .5f), new Vector2(2, .5f) } } } });
            try
            {
                theme.ReplaceData(replacement);
                Assert.That(NotebookSpriteCatalog.TryGetBounds(id, out var after), Is.True);
                Assert.That(after.xMin, Is.EqualTo(2).Within(.00001f));
                Assert.That(after.width, Is.EqualTo(4).Within(.00001f));
                Assert.That(after.height, Is.EqualTo(1).Within(.00001f));
                Assert.That(after, Is.Not.EqualTo(before));
            }
            finally { theme.ReplaceData(original); }
        }
    }
}
