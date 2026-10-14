using System.Collections.Generic;
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
            var geometry = FootprintGeometry.Resolve(new MortarFootprint(3), 2, default, vertical ? new Point2(0, 1) : new Point2(1, 0));
            var bounds = Render(geometry, impact);
            Assert.That(vertical ? bounds.size.y / bounds.size.x : bounds.size.x / bounds.size.y, Is.InRange(2.6f, 3.2f));
            float scale = 1;
            Assert.That(vertical ? bounds.size.y : bounds.size.x, Is.EqualTo(2 * geometry.ForwardRadius * scale).Within(.14f));
            Assert.That(vertical ? bounds.size.x : bounds.size.y, Is.EqualTo(2 * geometry.SideRadius * scale).Within(.14f));
            Assert.That(bounds.center.x, Is.EqualTo(10).Within(.1f));
            Assert.That(bounds.center.y, Is.EqualTo(10).Within(.1f));
        }
        [TestCase(false)] [TestCase(true)]
        public void LegacyViewsAndEventsWithoutGeometryRemainCircles(bool impact)
        {
            var bounds = Render(null, impact);
            Assert.That(bounds.size.x / bounds.size.y, Is.InRange(.95f, 1.05f));
        }
        [Test] public void SquareTelegraphKeepsEveryBoundaryRegardlessOfVertexCount()
        {
            var shape=MortarFootprint.FromContour(new[]{new Point2(-1,-1),new Point2(1,-1),new Point2(1,1),new Point2(-1,1)});
            var geometry=FootprintGeometry.Resolve(shape,2,default,new Point2(1,0));
            Render(geometry,false,vertices=>
            {
                for(int side=0;side<4;side++)
                {
                    var a=geometry.OutlineOffset(side);var b=geometry.OutlineOffset(side+1);
                    var mid=new Vector2(10+(a.X+b.X)*.5f,10+(a.Y+b.Y)*.5f);
                    Assert.That(System.Array.Exists(vertices,p=>Vector2.Distance(p,mid)<.4f),Is.True,"Missing telegraph edge "+side);
                }
            });
        }
        static Bounds Render(FootprintGeometry? geometry, bool impact, System.Action<Vector3[]> inspect = null)
        {
            var root = new GameObject("Isolated footprint renderer");
            try
            {
                var session = new GameSession(GameBalance.CreateDefault(), starterDefenses: false);
                var view = root.AddComponent<NotebookWorldView>();
                view.Initialize(new RunReadModel(session));
                if (impact)
                {
                    var events = new SimulationEventBuffer();
                    events.Publish(1, SimulationEventKind.ProjectileImpact, default, new Point2(10, 10), StructureKind.Mortar, 2, impactFootprint: geometry);
                    view.AddEvent(events[0]);
                }
                else
                {
                    var projectile = new MortarProjectile();
                    void Set(string name, object value) => typeof(MortarProjectile).GetProperty(name).SetValue(projectile, value);
                    Set("From", new Point2(10, 10)); Set("To", new Point2(10, 10));
                    Set("Duration", 1f); Set("Remaining", 1f); Set("Radius", 2f); Set("ImpactFootprint", geometry);
                    var weapons = typeof(GameSession).GetField("weapons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    var projectiles = (List<MortarProjectile>)weapons.GetType().GetField("projectiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapons);
                    projectiles.Add(projectile);
                }
                typeof(NotebookWorldView).GetMethod("DrawEffects", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                var pen = typeof(NotebookWorldView).GetField("effectsPen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var mesh = (Mesh)pen.GetType().GetField("mesh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pen);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                inspect?.Invoke(mesh.vertices);
                return mesh.bounds;
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
