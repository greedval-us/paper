using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public enum PlayerIcon { Play, Back, Pause, Fast, Home, Map, Book, Settings, Help, Exit, Restart, Check, Heart, Drop, Target, Flag, Trophy, Save, Plus, Trash, Pencil, Next, Copy,
        Shield, Bolt, Eraser, Blotter, Info, Close, ChevronDown, ChevronUp, Lock }

    /// <summary>Resolution-independent control symbols; never dependent on font glyph coverage.</summary>
    public sealed class PlayerIconGraphic : MaskableGraphic
    {
        public PlayerIcon Icon;
        public bool Sketch;
        protected override void OnPopulateMesh(VertexHelper vh)
            => FieldIconDrawing.Draw(vh, rectTransform.rect, color, Icon, Sketch);
    }
}
