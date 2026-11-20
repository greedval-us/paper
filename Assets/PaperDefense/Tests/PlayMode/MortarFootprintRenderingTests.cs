using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class MortarFootprintRenderingTests
    {
        [TestCase(false, false)] [TestCase(true, false)]
        [TestCase(false, true)] [TestCase(true, true)]
        public void TelegraphAndImpactUseCapturedWorldOrientation(bool vertical, bool impact)
        {
            var footprint = new MortarFootprint(3);
            var geometry = FootprintGeometry.Resolve(footprint, 2, default, vertical ? new Point2(0, 1) : new Point2(1, 0));
            var bounds = Render(footprint, vertical, impact);
            Assert.That(vertical ? bounds.size.y / bounds.size.x : bounds.size.x / bounds.size.y, Is.InRange(2.6f, 3.2f));
            float scale = 1;
            Assert.That(vertical ? bounds.size.y : bounds.size.x, Is.EqualTo(2 * geometry.ForwardRadius * scale).Within(.14f));
            Assert.That(vertical ? bounds.size.x : bounds.size.y, Is.EqualTo(2 * geometry.SideRadius * scale).Within(.14f));
            Assert.That(bounds.center.x, Is.EqualTo(MortarRenderingFixture.Target.X).Within(.1f));
            Assert.That(bounds.center.y, Is.EqualTo(MortarRenderingFixture.Target.Y).Within(.1f));
        }
        [TestCase(false)] [TestCase(true)]
        public void LegacyViewsAndEventsWithoutGeometryRemainCircles(bool impact)
        {
            var bounds = Render(null, false, impact);
            Assert.That(bounds.size.x / bounds.size.y, Is.InRange(.95f, 1.05f));
        }
        [Test] public void SquareTelegraphKeepsEveryBoundaryRegardlessOfVertexCount()
        {
            var shape=MortarFootprint.FromContour(new[]{new Point2(-1,-1),new Point2(1,-1),new Point2(1,1),new Point2(-1,1)});
            var geometry=FootprintGeometry.Resolve(shape,2,default,new Point2(1,0));
            Render(shape,false,false,vertices=>
            {
                for(int side=0;side<4;side++)
                {
                    var a=geometry.OutlineOffset(side);var b=geometry.OutlineOffset(side+1);
                    var mid=new Vector2(MortarRenderingFixture.Target.X+(a.X+b.X)*.5f,
                        MortarRenderingFixture.Target.Y+(a.Y+b.Y)*.5f);
                    Assert.That(System.Array.Exists(vertices,p=>Vector2.Distance(p,mid)<.4f),Is.True,"Missing telegraph edge "+side);
                }
            });
        }
        static Bounds Render(MortarFootprint footprint, bool vertical, bool impact, System.Action<Vector3[]> inspect = null)
        {
            var root = new GameObject("Isolated footprint renderer");
            var telegraph = new Mesh();
            try
            {
                var model = new RunReadModel(MortarRenderingFixture.Fire(GameBalance.CreateDefault(), footprint, vertical));
                var shell = model.Projectiles[0];
                // Historical frames predate stored geometry; the renderer accepts a missing outline.
                var geometry = footprint == null ? null : shell.ImpactFootprint;
                Mesh mesh;
                if (impact)
                {
                    var view = root.AddComponent<NotebookWorldView>();
                    view.Initialize(new RunReadModel(new GameSession(GameBalance.CreateDefault(), starterDefenses: false)));
                    var events = new SimulationEventBuffer();
                    events.Publish(1, SimulationEventKind.ProjectileImpact, shell.From, shell.To,
                        shell.Weapon, shell.Radius, impactFootprint: geometry);
                    view.AddEvent(events[0]);
                    typeof(NotebookWorldView).GetMethod("DrawEffects", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                    var effects = typeof(NotebookWorldView).GetField("effectsPen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                    mesh = (Mesh)effects.GetType().GetField("mesh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effects);
                }
                else
                {
                    var pen = new MeshPen(telegraph);
                    var center = new Vector2(shell.To.X, shell.To.Y);
                    // Center the shell itself so the bounds measure its telegraph rather than its trail.
                    NotebookEffectRenderer.DrawProjectile(pen, center, center, 0, geometry, shell.Radius, shell.Id,
                        shell.ColorEffect.Color, NotebookVisualThemeAsset.Default, false);
                    pen.Upload();
                    mesh = telegraph;
                }
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                inspect?.Invoke(mesh.vertices);
                return mesh.bounds;
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(telegraph); }
        }
    }

    internal static class MortarRenderingFixture
    {
        internal static readonly Point2 Target = new Point2(10.5f, 10.5f);

        internal static GameSession Fire(GameBalance balance, MortarFootprint footprint = null, bool vertical = false)
        {
            balance.Structure(StructureKind.Mortar).Radius = 2;
            var session = GameSession.CreatePrepared(balance, 17, new[] { new PreparedEnemy(Target, 100) });
            var source = new TowerBlueprint("test.mortar", "Mortar", ChassisKind.Standard, StructureKind.Mortar)
                .WithMortarFootprint(0, footprint ?? MortarFootprint.Circle);
            var cell = vertical ? new Cell(10, 4) : new Cell(4, 10);
            Assert.That(session.TryBuildBlueprint(new[] { cell }, source, MaterialTier.Pencil, InkColor.None,
                out string reason, facingDegrees: vertical ? 90 : 0), Is.True, reason);
            for (int i = 0; i < 600 && session.Projectiles.Count == 0; i++) session.Tick(1f / 30);
            Assert.That(session.Projectiles.Count, Is.EqualTo(1), "The test mortar must launch at its stationary target.");
            return session;
        }
    }
}
