using System;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    [Flags]
    public enum BattleEnemyRole { None = 0, Armor = 1, Spitter = 2, Runner = 4 }

    public readonly struct BattleReadabilityMark
    {
        public int EnemyId { get; }
        public BattleEnemyRole Roles { get; }
        public Rect WorldRect { get; }
        internal BattleReadabilityMark(int id, BattleEnemyRole roles, Rect bounds)
        { EnemyId = id; Roles = roles; WorldRect = bounds; }
    }

    /// <summary>Bounded presentation-only emphasis. Proximity never claims an enemy's attack target.</summary>
    public sealed class BattleReadability
    {
        public const int MaximumRoleMarkers = 24, MaximumThreatBrackets = 3;
        const int MaximumCandidates = 128;
        const float NearCastleRadius = 6, MarkerPixels = 10, SeparationPixels = 18;
        readonly Candidate[] candidates = new Candidate[MaximumCandidates];
        readonly Candidate[] nearby = new Candidate[MaximumThreatBrackets];
        readonly BattleReadabilityMark[] roles = new BattleReadabilityMark[MaximumRoleMarkers];
        readonly BattleReadabilityMark[] threats = new BattleReadabilityMark[MaximumThreatBrackets];
        int candidateCount;
        Vector2 castle;
        Rect viewport;
        bool showCastle;

        struct Candidate
        {
            internal BattleReadabilityMark Mark;
            internal float Distance;
            internal int Priority;
        }

        public Guid RunId { get; private set; }
        public int RoleMarkerCount { get; private set; }
        public int ThreatBracketCount { get; private set; }
        public int NearCastleCount { get; private set; }
        public bool LowCastleHealth { get; private set; }
        public float WorldUnitsPerPixel { get; private set; }
        public BattleReadabilityMark GetRoleMarker(int index)
            => index >= 0 && index < RoleMarkerCount ? roles[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public BattleReadabilityMark GetThreatBracket(int index)
            => index >= 0 && index < ThreatBracketCount ? threats[index] : throw new ArgumentOutOfRangeException(nameof(index));

        public void Refresh(IRunReadModel session, Rect worldViewport, float worldUnitsPerPixel)
        {
            RoleMarkerCount = ThreatBracketCount = NearCastleCount = candidateCount = 0;
            LowCastleHealth = showCastle = false;
            RunId = session?.RunId ?? Guid.Empty;
            WorldUnitsPerPixel = Finite(worldUnitsPerPixel) && worldUnitsPerPixel > 0 ? worldUnitsPerPixel : 0;
            viewport = worldViewport;
            if (session == null || session.Phase != RunPhase.Assault) return;
            castle = new Vector2(session.BaseCell.X + .5f, session.BaseCell.Y + .5f);
            LowCastleHealth = session.BaseHealth > 0 && session.Balance.BaseHealth > 0 &&
                session.BaseHealth <= session.Balance.BaseHealth * .3f;
            bool canDraw = WorldUnitsPerPixel > 0 && viewport.width > 0 && viewport.height > 0 &&
                Finite(viewport.xMin) && Finite(viewport.xMax) && Finite(viewport.yMin) && Finite(viewport.yMax);
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                var enemy = session.Enemies[i];
                if (!(enemy.Health > 0) || !Finite(enemy.Position.X) || !Finite(enemy.Position.Y)) continue;
                var at = new Vector2(enemy.Position.X, enemy.Position.Y);
                float distance = (at - castle).sqrMagnitude;
                bool near = distance <= NearCastleRadius * NearCastleRadius;
                if (near) NearCastleCount++;
                if (!canDraw || !viewport.Contains(at)) continue;
                float bodySize = DoodleGlyphs.VisualScale(enemy.VisualScale) * NotebookEnemyView.ModernScale;
                BattleEnemyRole role = Role(enemy);
                if (near)
                {
                    float size = Mathf.Max(14 * WorldUnitsPerPixel, bodySize * 1.2f);
                    var rect = new Rect(at - Vector2.one * size * .5f, Vector2.one * size);
                    if (Contains(viewport, rect))
                    {
                        var candidate = new Candidate { Mark = new BattleReadabilityMark(enemy.Id, role, rect), Distance = distance };
                        int count = ThreatBracketCount;
                        Insert(nearby, ref count, candidate);
                        ThreatBracketCount = count;
                    }
                }
                if (role == BattleEnemyRole.None) continue;
                // The mark sits beside the head, away from the existing health bar. Its screen
                // size is applied after the enemy's presentation transform, never to its body.
                float markerSize = MarkerPixels * WorldUnitsPerPixel;
                var center = at + new Vector2(-bodySize * .6f - markerSize * .65f, bodySize * .45f);
                var bounds = new Rect(center - Vector2.one * markerSize * .5f, Vector2.one * markerSize);
                if (!Contains(viewport, bounds)) continue;
                Insert(candidates, ref candidateCount, new Candidate {
                    Mark = new BattleReadabilityMark(enemy.Id, role, bounds), Distance = distance,
                    Priority = near ? 0 : enemy.WindupRemaining > 0 ? 1 : 2
                });
            }
            float separation = SeparationPixels * WorldUnitsPerPixel;
            for (int i = 0; i < candidateCount && RoleMarkerCount < MaximumRoleMarkers; i++)
            {
                var mark = candidates[i].Mark; bool crowded = false;
                for (int j = 0; j < RoleMarkerCount; j++)
                    if ((mark.WorldRect.center - roles[j].WorldRect.center).sqrMagnitude < separation * separation)
                    { crowded = true; break; }
                if (!crowded) roles[RoleMarkerCount++] = mark;
            }
            for (int i = 0; i < ThreatBracketCount; i++) threats[i] = nearby[i].Mark;
            float halo = Mathf.Max(2.2f, 16 * WorldUnitsPerPixel) + 3 * WorldUnitsPerPixel;
            showCastle = canDraw && (NearCastleCount > 0 || LowCastleHealth) &&
                Contains(viewport, new Rect(castle - Vector2.one * halo, Vector2.one * halo * 2));
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin && inner.yMin >= outer.yMin &&
            inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
        static BattleEnemyRole Role(EnemyView enemy)
        {
            BattleEnemyRole role = enemy.DrawingTier == DrawingTier.Outlined || enemy.DrawingTier == DrawingTier.Hatched
                ? BattleEnemyRole.Armor : BattleEnemyRole.None;
            if (enemy.Kind == EnemyKind.Spitter) role |= BattleEnemyRole.Spitter;
            if (enemy.Kind == EnemyKind.Runner) role |= BattleEnemyRole.Runner;
            return role;
        }
        static bool Before(Candidate a, Candidate b) => a.Priority != b.Priority ? a.Priority < b.Priority :
            a.Distance != b.Distance ? a.Distance < b.Distance : a.Mark.EnemyId < b.Mark.EnemyId;
        static void Insert(Candidate[] buffer, ref int count, Candidate candidate)
        {
            if (count == buffer.Length && !Before(candidate, buffer[count - 1])) return;
            int index = Mathf.Min(count, buffer.Length - 1);
            while (index > 0 && Before(candidate, buffer[index - 1]))
            { buffer[index] = buffer[index - 1]; index--; }
            buffer[index] = candidate;
            if (count < buffer.Length) count++;
        }

        public void Draw(MeshPen pen, NotebookVisualThemeAsset theme)
        {
            if (pen == null || WorldUnitsPerPixel <= 0) return;
            theme = theme ?? NotebookVisualThemeAsset.Default;
            var warning = theme.Color(LowCastleHealth ? NotebookInkChannel.Danger : NotebookInkChannel.Amber);
            if (showCastle)
                pen.Ring(castle, Mathf.Max(2.2f, 16 * WorldUnitsPerPixel), 1.5f * WorldUnitsPerPixel,
                    MeshPen.Alpha(warning, .8f), 24);
            for (int i = 0; i < ThreatBracketCount; i++) DrawBracket(pen, threats[i].WorldRect, WorldUnitsPerPixel, warning);
            for (int i = 0; i < RoleMarkerCount; i++) DrawRole(pen, roles[i], WorldUnitsPerPixel, theme);
        }

        static void DrawBracket(MeshPen pen, Rect bounds, float pixel, Color color)
        {
            float inset = 2 * pixel, reach = Mathf.Min(bounds.width * .25f, 4 * pixel);
            float left = bounds.xMin + inset, right = bounds.xMax - inset;
            float bottom = bounds.yMin + inset, top = bounds.yMax - inset;
            float width = 1.4f * pixel;
            pen.Line(new Vector2(left, top - reach), new Vector2(left, top), width, color);
            pen.Line(new Vector2(left, top), new Vector2(left + reach, top), width, color);
            pen.Line(new Vector2(right - reach, top), new Vector2(right, top), width, color);
            pen.Line(new Vector2(right, top), new Vector2(right, top - reach), width, color);
            pen.Line(new Vector2(left, bottom + reach), new Vector2(left, bottom), width, color);
            pen.Line(new Vector2(left, bottom), new Vector2(left + reach, bottom), width, color);
            pen.Line(new Vector2(right - reach, bottom), new Vector2(right, bottom), width, color);
            pen.Line(new Vector2(right, bottom), new Vector2(right, bottom + reach), width, color);
        }
        static void DrawRole(MeshPen pen, BattleReadabilityMark mark, float pixel, NotebookVisualThemeAsset theme)
        {
            Vector2 at = mark.WorldRect.center;
            Color edge = theme.Color(NotebookInkChannel.Ivory);
            Color role = theme.Color((mark.Roles & BattleEnemyRole.Spitter) != 0 ? NotebookInkChannel.Danger : NotebookInkChannel.Amber);
            pen.Disc(at, 5 * pixel, theme.Color(NotebookInkChannel.Surface), 8);
            bool armor = (mark.Roles & BattleEnemyRole.Armor) != 0;
            if (armor)
            {
                Stroke(pen, at, pixel, -3, 3, 3, 3, edge);
                Stroke(pen, at, pixel, 3, 3, 2.7f, -1, edge);
                Stroke(pen, at, pixel, 2.7f, -1, 0, -3.2f, edge);
                Stroke(pen, at, pixel, 0, -3.2f, -2.7f, -1, edge);
                Stroke(pen, at, pixel, -2.7f, -1, -3, 3, edge);
            }
            if ((mark.Roles & BattleEnemyRole.Spitter) != 0)
            {
                float size = armor ? .65f : 1;
                pen.Triangle(at + new Vector2(0, 3) * pixel * size, at + new Vector2(-2, -.8f) * pixel * size,
                    at + new Vector2(2, -.8f) * pixel * size, role);
                pen.Disc(at + new Vector2(0, -.8f) * pixel * size, 2 * pixel * size, role, 8);
            }
            else if ((mark.Roles & BattleEnemyRole.Runner) != 0)
            {
                float size = armor ? .65f : 1;
                Stroke(pen, at, pixel, -3 * size, 2 * size, -1 * size, 0, role);
                Stroke(pen, at, pixel, -1 * size, 0, -3 * size, -2 * size, role);
                Stroke(pen, at, pixel, 0, 2 * size, 2 * size, 0, role);
                Stroke(pen, at, pixel, 2 * size, 0, 0, -2 * size, role);
            }
        }
        static void Stroke(MeshPen pen, Vector2 at, float pixel, float ax, float ay, float bx, float by, Color color)
            => pen.Line(at + new Vector2(ax, ay) * pixel, at + new Vector2(bx, by) * pixel, 1.1f * pixel, color);
    }
}
