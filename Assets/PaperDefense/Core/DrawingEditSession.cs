using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Editor-only command history; saved snapshots and deployed copies cannot be changed by later commands.</summary>
    public sealed class DrawingEditSession
    {
        const int HistoryLimit = 128;
        readonly List<EditorState> undo = new List<EditorState>();
        readonly List<EditorState> redo = new List<EditorState>();
        public TowerBlueprint Blueprint { get; private set; }
        public DrawingSnapshot Snapshot { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        public DrawingEditSession(TowerBlueprint blueprint, DrawingSnapshot snapshot = null)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Snapshot = (snapshot ?? blueprint.Drawing ?? DrawingSnapshot.Empty).DeepCopy();
        }

        public bool AddStroke(DrawingPartKey part, IReadOnlyList<DrawingPoint> points, out string reason)
            => AddStroke(DrawingBindings.For(part), points, out reason);
        public bool AddStroke(string part, IReadOnlyList<DrawingPoint> points, out string reason)
        {
            reason = "";
            if (!Enabled(part)) { reason = UserText.Message("copy.7ebe71725a540755"); return false; }
            try
            {
                var strokes = new List<DrawingStroke>(Snapshot.GetPart(part).Strokes);
                strokes.Add(new DrawingStroke(points));
                Commit(Blueprint, WithPart(part, strokes));
                return true;
            }
            catch (ArgumentException exception) { reason = exception.Message; return false; }
        }

        public bool Erase(DrawingPartKey part, DrawingPoint point, float radius)
            => Erase(DrawingBindings.For(part), point, radius);
        public bool Erase(string part, DrawingPoint point, float radius)
        {
            if (!Enabled(part)) return false;
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0 || radius > 2)
                throw new ArgumentOutOfRangeException(nameof(radius));
            var retained = new List<DrawingStroke>();
            foreach (DrawingStroke stroke in Snapshot.GetPart(part).Strokes)
                if (!Touches(stroke, point, radius)) retained.Add(stroke);
            if (retained.Count == Snapshot.GetPart(part).Strokes.Count) return false;
            Commit(Blueprint, WithPart(part, retained));
            return true;
        }

        public bool ClearPart(DrawingPartKey part)
            => ClearPart(DrawingBindings.For(part));
        public bool ClearPart(string part)
        {
            DrawingPart current = Snapshot.GetPart(part);
            if (current.Strokes.Count == 0) return false;
            Commit(Blueprint, WithPart(part, Array.Empty<DrawingStroke>()));
            return true;
        }

        public bool Clear()
        {
            if (Snapshot.PointCount == 0) return false;
            Commit(Blueprint, DrawingSnapshot.Empty);
            return true;
        }

        public bool Undo() => Restore(undo, redo);
        public bool Redo() => Restore(redo, undo);

        public bool ReplaceBlueprint(TowerBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (SameAssembly(Blueprint, blueprint) && Blueprint.Id == blueprint.Id && Blueprint.Name == blueprint.Name) return false;
            var parts = new List<DrawingPart>(Snapshot.Parts.Count);
            foreach (DrawingPart part in Snapshot.Parts)
                if (DrawingPartPolicy.ShouldRetain(part.BindingId, Blueprint, blueprint)) parts.Add(part);
            Commit(blueprint, new DrawingSnapshot(parts));
            return true;
        }

        DrawingSnapshot WithPart(string key, IReadOnlyList<DrawingStroke> strokes)
        {
            var parts = new List<DrawingPart>(Snapshot.Parts.Count); bool replaced = false;
            for (int i = 0; i < Snapshot.Parts.Count; i++)
            {
                if (Snapshot.Parts[i].BindingId == key) { parts.Add(new DrawingPart(key, strokes)); replaced = true; }
                else parts.Add(Snapshot.Parts[i]);
            }
            if (!replaced) parts.Add(new DrawingPart(key, strokes));
            return new DrawingSnapshot(parts);
        }

        bool Enabled(string key) => DrawingTemplateCatalog.IsEnabled(Blueprint, key);

        void Commit(TowerBlueprint blueprint, DrawingSnapshot snapshot)
        {
            undo.Add(new EditorState(Blueprint, Snapshot));
            if (undo.Count > HistoryLimit) undo.RemoveAt(0);
            redo.Clear();
            Blueprint = blueprint; Snapshot = snapshot;
        }

        bool Restore(List<EditorState> from, List<EditorState> to)
        {
            if (from.Count == 0) return false;
            to.Add(new EditorState(Blueprint, Snapshot));
            if (to.Count > HistoryLimit) to.RemoveAt(0);
            EditorState state = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            Blueprint = state.Blueprint; Snapshot = state.Snapshot;
            return true;
        }

        static bool SameAssembly(TowerBlueprint first, TowerBlueprint second)
        {
            if (first.ChassisId != second.ChassisId || first.WeaponMounts.Count != second.WeaponMounts.Count || first.ModuleSelections.Count != second.ModuleSelections.Count) return false;
            for(int i=0;i<first.WeaponMounts.Count;i++)
                if(first.WeaponMounts[i].SlotIndex!=second.WeaponMounts[i].SlotIndex||first.WeaponMounts[i].WeaponId!=second.WeaponMounts[i].WeaponId||first.WeaponMounts[i].Footprint.AspectRatio!=second.WeaponMounts[i].Footprint.AspectRatio)return false;
            for (int i = 0; i < first.ModuleSelections.Count; i++)
                if (first.ModuleSelections[i].InstanceId != second.ModuleSelections[i].InstanceId || first.ModuleSelections[i].DefinitionId != second.ModuleSelections[i].DefinitionId) return false;
            return true;
        }

        static bool Touches(DrawingStroke stroke, DrawingPoint point, float radius)
        {
            float squared = radius * radius;
            for (int i = 1; i < stroke.Points.Count; i++)
            {
                DrawingPoint from = stroke.Points[i - 1], to = stroke.Points[i];
                float dx = to.X - from.X, dy = to.Y - from.Y;
                float length = dx * dx + dy * dy;
                float t = length > 0 ? ((point.X - from.X) * dx + (point.Y - from.Y) * dy) / length : 0;
                t = Math.Max(0, Math.Min(1, t));
                float offsetX = point.X - (from.X + t * dx), offsetY = point.Y - (from.Y + t * dy);
                if (offsetX * offsetX + offsetY * offsetY <= squared) return true;
            }
            return false;
        }

        readonly struct EditorState
        {
            public readonly TowerBlueprint Blueprint;
            public readonly DrawingSnapshot Snapshot;
            public EditorState(TowerBlueprint blueprint, DrawingSnapshot snapshot) { Blueprint = blueprint; Snapshot = snapshot; }
        }
    }
}
