using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class ReleaseVisualCaptureTests
    {
        [Test] public void OffscreenCaptureContainsTheLiveOverlayAndRestoresItsState()
        {
            var type = typeof(PlayerGameRoot).Assembly.GetType("PaperDefense.Presentation.ReleaseVisualCapture");
            Assert.That(type,Is.Not.Null,"Hidden-player captures need explicit offscreen rendering rather than a black swapchain screenshot.");
            var root = new GameObject("Release capture fixture",typeof(RectTransform),typeof(Canvas));
            var image = new GameObject("Opaque blue overlay",typeof(RectTransform),typeof(Image));
            var pixels = new Texture2D(2,2);
            string folder = Path.Combine(Path.GetTempPath(),"PaperDefense-capture-"+Guid.NewGuid().ToString("N"));
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                image.transform.SetParent(root.transform,false);
                var rect = image.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                image.GetComponent<Image>().color = new Color(.1f,.2f,.8f,1);
                image.layer = 7;
                float distance = canvas.planeDistance;
                string path = (string)type.GetMethod("Capture",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[] {"fixture",64,64,folder});
                Assert.That(ImageConversion.LoadImage(pixels,File.ReadAllBytes(path)),Is.True);
                var center = pixels.GetPixel(32,32);
                Assert.That(center.b,Is.GreaterThan(center.r+.2f),"The opaque live UI must be visible in an offscreen frame.");
                Assert.That(canvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.worldCamera,Is.Null);
                Assert.That(canvas.planeDistance,Is.EqualTo(distance));
                Assert.That(image.layer,Is.EqualTo(7));
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(pixels);
                foreach (string name in new[] { "fixture.png","fixture.json" })
                    if (File.Exists(Path.Combine(folder,name))) File.Delete(Path.Combine(folder,name));
                if (Directory.Exists(folder)) Directory.Delete(folder);
            }
        }
    }
}
