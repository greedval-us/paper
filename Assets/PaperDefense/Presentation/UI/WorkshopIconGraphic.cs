using UnityEngine;

namespace PaperDefense.Presentation
{
    public enum WorkshopIcon
    {
        None, Tower, Weapon, Pencil, Target, Book, Check, Next, Back, Eraser, Line,
        Rectangle, Ellipse, Move, Undo, Redo, Grid, Magnet, ZoomIn, ZoomOut, Fit,
        Mirror, Trash, Chevron, Damage, Range, Cost, Ink, Shield, Plus, Close,
        Help, Save, Settings, Copy,
        Weight, Compare, Pin, Clock, Lock, ChevronUp, Heart, Armor, Reload, Magazine,
        Play, Pause, Restart, FiringArc
    }

    /// <summary>Small, consistent line symbols. Geometry is independent of fonts and obeys uGUI masks.</summary>
    public sealed class WorkshopIconGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] WorkshopIcon icon;
        public WorkshopIcon Icon
        {
            get => icon;
            set { if (icon == value) return; icon = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
            => FieldIconDrawing.Draw(vh, rectTransform.rect, color, icon);
    }
}
