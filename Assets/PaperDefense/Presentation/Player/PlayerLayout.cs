using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Logical pixels shared by both canvases and the world-camera viewport.</summary>
    public readonly struct PlayerLayout
    {
        public readonly float Width, Height, Scale;
        public readonly Rect Field;
        public readonly Rect Header, Dock;
        public float Margin => Width < 1200 ? 24 : 48;
        public bool Compact => Width < 1200;
        public PlayerLayout(int width, int height, bool albumBattle = false)
        {
            Scale = Mathf.Max(1, Mathf.Min(width / 1600f, height / 1000f));
            Width = Mathf.Max(1, width) / Scale; Height = Mathf.Max(1, height) / Scale;
            bool narrow = Width < 1050;
            Header = new Rect(12, 8, Width - 24, narrow ? 92 : 52);
            Dock = albumBattle ? new Rect(12, Height - 208, Width - 24, 196)
                : new Rect(12, Height - (narrow ? 196 : 156), Width - 24, narrow ? 184 : 144);
            Field = new Rect(12, Header.yMax + 12, Width - 24, Mathf.Max(40, Dock.y - Header.yMax - 24));
        }
        public Rect NormalizedField => new Rect(Field.x / Width, 1 - Field.yMax / Height, Field.width / Width, Field.height / Height);
    }
}
