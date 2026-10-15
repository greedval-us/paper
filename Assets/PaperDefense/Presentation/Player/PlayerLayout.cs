using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Logical pixels shared by both canvases and the world-camera viewport.</summary>
    public readonly struct PlayerLayout
    {
        public readonly float Width, Height, Scale;
        public readonly Rect Field;
        public float Margin => Width < 1200 ? 24 : 48;
        public bool Compact => Width < 1200;
        public PlayerLayout(int width, int height, float uiScale = 1)
        {
            Scale = Mathf.Max(1, Mathf.Min(width / 1600f, height / 1000f)) * Mathf.Clamp(uiScale, 1, 1.5f);
            Width = Mathf.Max(1, width) / Scale; Height = Mathf.Max(1, height) / Scale;
            float dock = Width < 900 ? 136 : Width < 1100 ? 214 : 158;
            float top = Width < 900 ? 180 : 132;
            float right = Width < 1100 ? 20 : 270;
            Field = new Rect(100, top, Mathf.Max(100, Width - 100 - right), Mathf.Max(40, Height - top - dock - 60));
        }
        public Rect NormalizedField => new Rect(Field.x / Width, 1 - Field.yMax / Height, Field.width / Width, Field.height / Height);
    }
}
