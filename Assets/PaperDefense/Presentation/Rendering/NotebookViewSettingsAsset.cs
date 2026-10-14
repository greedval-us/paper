using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Camera and screen readability tuning, independent of combat and authored drawings.</summary>
    [CreateAssetMenu(menuName = "Paper Defense/Notebook view settings")]
    public sealed class NotebookViewSettingsAsset : ScriptableObject
    {
        [Header("Camera")]
        [Range(1.05f, 2)] public float ZoomPerNotch = 1.28f;
        [Range(1, 8)] public float MaximumZoom = 4;
        [Header("Readability (screen pixels)")]
        [Range(.5f, 3)] public float MinimumStrokePixels = 1.25f;
        [Range(.5f, 2)] public float TerrainStrokePixels = 1;
        [Range(0, 20)] public float TowerMarkerPixels = 12;
        [Min(1)] public float MinorGridFadeStart = 14;
        [Min(2)] public float MinorGridFadeEnd = 24;

        static NotebookViewSettingsAsset fallback;
        public static NotebookViewSettingsAsset Default
        {
            get
            {
                if (fallback == null)
                {
                    fallback = Resources.Load<NotebookViewSettingsAsset>("NotebookViewSettings");
                    if (fallback == null)
                    {
                        fallback = CreateInstance<NotebookViewSettingsAsset>();
                        fallback.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return fallback;
            }
        }

        public float ZoomAfterScroll(float current, float notches)
            => Mathf.Clamp(current * Mathf.Pow(Mathf.Clamp(ZoomPerNotch, 1.05f, 2), Mathf.Clamp(notches, -32, 32)),
                1, Mathf.Clamp(MaximumZoom, 1, 8));

        public float MinorGridOpacity(float pixelsPerCell)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(MinorGridFadeStart, Mathf.Max(MinorGridFadeStart + 1, MinorGridFadeEnd), pixelsPerCell));
    }
}
