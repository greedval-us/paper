using System.IO;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Offscreen evidence for UI when the editor Game View is not rendering in the foreground.</summary>
    public static class PlayerVisualCapture
    {
        public static string Capture(string name, int width = 1600, int height = 1000, string folder = "Captures/PlayerGame")
        {
            if (width <= 0 || height <= 0) throw new System.ArgumentOutOfRangeException(nameof(width));
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new System.ArgumentException("Capture requires a single file name.", nameof(name));
            var canvases = Object.FindObjectsByType<Canvas>();
            var modes = new RenderMode[canvases.Length]; var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            var captureObject = new GameObject("Temporary UI capture camera", typeof(Camera));
            var capture = captureObject.GetComponent<Camera>(); capture.enabled = false;
            capture.orthographic = true; capture.nearClipPlane = .1f; capture.farClipPlane = 100;
            capture.clearFlags = CameraClearFlags.SolidColor; capture.backgroundColor = Color.clear; capture.cullingMask = 1 << 5;
            var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
            var overlay = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var layers = new System.Collections.Generic.Dictionary<GameObject, int>();
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                var bootstrap = Object.FindAnyObjectByType<PaperDefense.Presentation.PaperDefenseBootstrap>();
                var world = bootstrap != null ? bootstrap.worldCamera : Camera.main;
                RenderTexture.active = texture; GL.Clear(true, true, world != null ? world.backgroundColor : new Color(.965f, .951f, .905f));
                if (world != null)
                {
                    var target = world.targetTexture;
                    try { world.targetTexture = texture; world.Render(); }
                    finally { world.targetTexture = target; }
                }
                capture.targetTexture = overlay;
                for (int i = 0; i < canvases.Length; i++)
                {
                    var canvas = canvases[i]; modes[i] = canvas.renderMode; cameras[i] = canvas.worldCamera; distances[i] = canvas.planeDistance;
                    foreach (var transform in canvas.GetComponentsInChildren<Transform>(true))
                    { if (!layers.ContainsKey(transform.gameObject)) layers.Add(transform.gameObject, transform.gameObject.layer); transform.gameObject.layer = 5; }
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = capture; canvas.planeDistance = 10;
                }
                Canvas.ForceUpdateCanvases(); capture.Render();
                RenderTexture.active = texture;
                GL.PushMatrix(); GL.LoadPixelMatrix(0, width, height, 0);
                Graphics.DrawTexture(new Rect(0, 0, width, height), overlay);
                GL.PopMatrix();
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory(folder);
                string path = folder + "/" + name + ".png"; File.WriteAllBytes(path, pixels.EncodeToPNG());
                File.WriteAllText(folder + "/" + name + ".json", JsonUtility.ToJson(new CaptureEvidence {
                    name = name, width = width, height = height, frame = Time.frameCount,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                    activeCanvases = canvases.Length, artRevision = PaperDefense.Presentation.AssemblyVisualComposer.Revision
                }, true));
                return path;
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
                foreach (var pair in layers) if (pair.Key != null) pair.Key.layer = pair.Value;
                Canvas.ForceUpdateCanvases(); RenderTexture.active = previous;
                Object.DestroyImmediate(pixels); capture.targetTexture = null;
                overlay.Release(); Object.DestroyImmediate(overlay);
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(captureObject);
            }
        }
        [System.Serializable] sealed class CaptureEvidence
        {
            public string name, scene, artRevision;
            public int width, height, frame, activeCanvases;
        }
    }
}
