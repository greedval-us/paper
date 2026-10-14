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

        public bool AddLine(string part,DrawingPoint from,DrawingPoint to,out string reason)
        {
            reason="";
            if(from.Equals(to)){reason=UserText.Message("drawing.command.empty_shape");return false;}
            return AddStroke(part,new[]{from,to},out reason);
        }
        public bool AddRectangle(string part,DrawingPoint from,DrawingPoint to,out string reason)
        {
            reason="";
            if(from.X==to.X||from.Y==to.Y){reason=UserText.Message("drawing.command.empty_shape");return false;}
            return AddStroke(part,new[]{from,new DrawingPoint(to.X,from.Y),to,new DrawingPoint(from.X,to.Y),from},out reason);
        }
        public bool AddEllipse(string part,DrawingPoint from,DrawingPoint to,out string reason)
        {
            reason="";
            if(from.X==to.X||from.Y==to.Y){reason=UserText.Message("drawing.command.empty_shape");return false;}
            const int segments=32;var points=new DrawingPoint[segments+1];
            float x=(from.X+to.X)*.5f,y=(from.Y+to.Y)*.5f,rx=Math.Abs(to.X-from.X)*.5f,ry=Math.Abs(to.Y-from.Y)*.5f;
            for(int i=0;i<segments;i++){double angle=i*Math.PI*2/segments;points[i]=new DrawingPoint((float)(x+rx*Math.Cos(angle)),(float)(y+ry*Math.Sin(angle)));}
            points[segments]=points[0];return AddStroke(part,points,out reason);
        }
        public bool ReplaceFromStencil(string part,DrawingUnlockDefinition stencil,out string reason)
        {
            reason="";
            if(stencil==null){reason=UserText.Message("drawing.unlock.unknown");return false;}
            if((stencil.Role==DrawingStencilRole.Body&&part!=DrawingBindings.Base)||(stencil.Role==DrawingStencilRole.Barrel&&!DrawingBindings.TryMount(part,out _)))
            {reason=UserText.Message("drawing.command.stencil_role");return false;}
            return ReplacePart(part,stencil.Strokes,out reason);
        }
        public bool TranslatePart(string part,float dx,float dy,out string reason)
            => TransformPart(part,1,1,dx,dy,out reason);
        /// <summary>Scale around the part-local origin (the mount pivot for barrels), never clip points.</summary>
        public bool ScalePart(string part,float scaleX,float scaleY,out string reason)
        {
            if(!Finite(scaleX)||!Finite(scaleY)||scaleX<=0||scaleY<=0){reason=UserText.Message("drawing.command.invalid_scale");return false;}
            return TransformPart(part,scaleX,scaleY,0,0,out reason);
        }
        public bool MirrorPartHorizontal(string part,out string reason)=>TransformPart(part,-1,1,0,0,out reason);
        bool TransformPart(string part,float sx,float sy,float dx,float dy,out string reason)
        {
            reason="";
            if(!Enabled(part)){reason=UserText.Message("copy.7ebe71725a540755");return false;}
            if(!Finite(dx)||!Finite(dy)){reason=UserText.Message("drawing.command.bounds");return false;}
            var transformed=new List<DrawingStroke>();
            foreach(var stroke in Snapshot.GetPart(part).Strokes)
            {
                var points=new DrawingPoint[stroke.Points.Count];
                for(int i=0;i<points.Length;i++)
                {
                    double x=(double)stroke.Points[i].X*sx+dx,y=(double)stroke.Points[i].Y*sy+dy;
                    if(x< -1||x>1||y< -1||y>1){reason=UserText.Message("drawing.command.bounds");return false;}
                    points[i]=new DrawingPoint((float)x,(float)y);
                }
                transformed.Add(new DrawingStroke(points));
            }
            return ReplacePart(part,transformed,out reason);
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        bool ReplacePart(string part,IReadOnlyList<DrawingStroke> strokes,out string reason)
        {
            reason="";
            if(!Enabled(part)){reason=UserText.Message("copy.7ebe71725a540755");return false;}
            try
            {
                var changed=WithPart(part,strokes);
                if(changed.GeometryHash==Snapshot.GeometryHash)return false;
                Commit(Blueprint,changed);return true;
            }
            catch(ArgumentException error){reason=error.Message;return false;}
        }
        /// <summary>Erase whole strokes swept by the pointer path in one history step.</summary>
        public bool ErasePath(string part,IReadOnlyList<DrawingPoint> path,float radius,out string reason)
        {
            reason="";
            if(!Enabled(part)){reason=UserText.Message("copy.7ebe71725a540755");return false;}
            if(path==null||path.Count==0)return false;
            if(path.Count>DrawingSnapshot.MaxPoints||!Finite(radius)||radius<=0||radius>2){reason=UserText.Message("drawing.command.bounds");return false;}
            var retained=new List<DrawingStroke>();
            foreach(var stroke in Snapshot.GetPart(part).Strokes)
                if(!TouchesPath(stroke,path,radius))retained.Add(stroke);
            return ReplacePart(part,retained,out reason);
        }
        static bool TouchesPath(DrawingStroke stroke,IReadOnlyList<DrawingPoint> path,float radius)
        {
            foreach(var point in path)if(Touches(stroke,point,radius))return true;
            for(int p=1;p<path.Count;p++)
            {
                var a=path[p-1];var b=path[p];
                var sweep=new DrawingStroke(new[]{a,b});
                foreach(var point in stroke.Points)if(Touches(sweep,point,radius))return true;
                for(int i=1;i<stroke.Points.Count;i++)
                {
                    var c=stroke.Points[i-1];var d=stroke.Points[i];
                    if(Cross(a,b,c)*Cross(a,b,d)<0&&Cross(c,d,a)*Cross(c,d,b)<0)return true;
                }
            }
            return false;
        }
        static double Cross(DrawingPoint a,DrawingPoint b,DrawingPoint c)=>(double)(b.X-a.X)*(c.Y-a.Y)-(double)(b.Y-a.Y)*(c.X-a.X);

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
