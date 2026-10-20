using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PaperDefense.Editor;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class NotebookNativeAtlasTests
    {
        [Test] public void CompleteAtlasPng_FitsDeclaredNativeSizeWithFourPixelGuttersAndPreservesEveryNativeRgbaPixel()
        {
            var source = NotebookSpriteImportTools.ParseSource(File.ReadAllText(NotebookSpriteImportTools.SourcePath));
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var images = new Dictionary<string, (int width, Color32[] pixels)>(); var placed = new List<Rect>();
            try
            {
                Assert.That(atlas.LoadImage(File.ReadAllBytes(NotebookSpriteImportTools.AtlasPath)), Is.True);
                Assert.That(atlas.width, Is.EqualTo(4096)); Assert.That(atlas.height, Is.EqualTo(4352));
                var imported = Resources.Load<Texture2D>("Art/NotebookAtlas");
                Assert.That(imported.width, Is.EqualTo(atlas.width)); Assert.That(imported.height, Is.EqualTo(atlas.height),
                    "The non-square atlas must remain native-sized in the player resource.");
                Assert.That(imported.mipmapCount, Is.EqualTo(1), "Whole-atlas mipmaps mix neighbouring native crops.");
                Assert.That(imported.filterMode, Is.EqualTo(FilterMode.Bilinear));
                var actual = atlas.GetPixels32();
                foreach (var definition in source.sprites)
                {
                    Assert.That(NotebookSpriteCatalog.TryGet(definition.id, out var sprite), Is.True, definition.id);
                    var rect = sprite.rect;
                    Assert.That(rect.width, Is.EqualTo(definition.width)); Assert.That(rect.height, Is.EqualTo(definition.height));
                    Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(rect.xMax, Is.LessThanOrEqualTo(atlas.width)); Assert.That(rect.yMax, Is.LessThanOrEqualTo(atlas.height));
                    foreach (var other in placed)
                    {
                        float dx = Mathf.Max(rect.xMin - other.xMax, other.xMin - rect.xMax);
                        float dy = Mathf.Max(rect.yMin - other.yMax, other.yMin - rect.yMax);
                        Assert.That(Mathf.Max(dx, dy), Is.GreaterThanOrEqualTo(4), definition.id + ": atlas gutter");
                    }
                    placed.Add(rect);
                    if (!images.TryGetValue(definition.source, out var image))
                    {
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        try
                        {
                            Assert.That(texture.LoadImage(File.ReadAllBytes(definition.source)), Is.True);
                            image = (texture.width, texture.GetPixels32()); images.Add(definition.source, image);
                        }
                        finally { Object.DestroyImmediate(texture); }
                    }
                    for (int y = 0; y < definition.height; y++) for (int x = 0; x < definition.width; x++)
                    {
                        var a = actual[((int)rect.y + y) * atlas.width + (int)rect.x + x];
                        var b = image.pixels[(definition.y + y) * image.width + definition.x + x];
                        if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a)
                            Assert.Fail(definition.id + ": native RGBA differs at " + x + "," + y);
                    }
                }
            }
            finally { Object.DestroyImmediate(atlas); }
        }
    }
}
