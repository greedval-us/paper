using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyWorldViewFitTests
    {
        [Test] public void WideBatteryUsesOneAimIndependentWholeTreeFitInsideAdjacentTwoUnitCells()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            PartRef P(string id) => new PartRef(id, 1);
            var foundation = new FoundationRecipe(P("frame.battery"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
            var weapon = catalog.Starters.Single(entry => entry.Id == "album.guard").Snapshot.Recipe.Tower.Mounts[0].Weapon;
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, Enumerable.Range(0, 3).Select(slot => new WeaponMountRecipe(slot, weapon))));
            var snapshot = new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions);
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 10000;
            var resolution = AssemblyResolver.Resolve(snapshot, balance, MaterialTier.Pencil);
            Assert.That(resolution.Valid, Is.True);
            var session = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(12, 12);
            Assert.That(session.TryBuildEntry(new[] { cell }, resolution.Value, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            var state = session.StructureAt(cell); state.IsComplete = true; state.BuildRemaining = 0;
            Vector2 origin = new Vector2(cell.X + .5f, cell.Y + .5f);
            var mesh = new Mesh(); float firstScale = 0; Rect enclosure = default;
            try
            {
                int poseIndex = 0;
                foreach (var direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
                {
                    for (int slot = 0; slot < 3; slot++)
                        state.GetMountState(slot).Aim = new Point2(origin.x + direction.x * 100, origin.y + direction.y * 100);
                    var tower = new RunReadModel(session).Structures.Single();
                    var native = DoodleGlyphs.ComposeAssembly(tower);
                    var pen = new MeshPen(mesh);
                    DoodleGlyphs.StructureDynamic(pen, tower, 1); pen.Upload();
                    var vertices = mesh.vertices;
                    Assert.That(vertices.Length, Is.EqualTo(native.Fragments.Count * 4));
                    var first = native.Fragments[0];
                    Vector2 source = first.Transform(new Vector2(Mathf.Max(first.Trim.xMin, first.Region.xMin), Mathf.Max(first.Trim.yMin, first.Region.yMin)));
                    float scale = Vector2.Dot((Vector2)vertices[0] - origin, source) / source.sqrMagnitude;
                    Assert.That(scale, Is.GreaterThan(0));
                    if (poseIndex++ == 0) firstScale = scale;
                    else Assert.That(scale, Is.EqualTo(firstScale).Within(.00001f), "Aiming must never pump the complete tree's view scale.");
                    var bounds = Rect.MinMaxRect(vertices.Min(point => point.x), vertices.Min(point => point.y), vertices.Max(point => point.x), vertices.Max(point => point.y));
                    Assert.That(bounds.width, Is.LessThanOrEqualTo(2.00001f), "The actual full battery art must fit neighbours with the real two-unit spacing.");
                    Assert.That(bounds.height, Is.LessThanOrEqualTo(2.00001f));
                    foreach (var point in vertices)
                    {
                        Assert.That(Mathf.Abs(point.x - origin.x), Is.LessThanOrEqualTo(1.00001f));
                        Assert.That(Mathf.Abs(point.y - origin.y), Is.LessThanOrEqualTo(1.00001f));
                    }
                    if (poseIndex == 1) enclosure = bounds;
                    else enclosure = Rect.MinMaxRect(Mathf.Min(enclosure.xMin, bounds.xMin), Mathf.Min(enclosure.yMin, bounds.yMin),
                        Mathf.Max(enclosure.xMax, bounds.xMax), Mathf.Max(enclosure.yMax, bounds.yMax));
                    for (int fragment = 0; fragment < native.Fragments.Count; fragment++)
                    {
                        var item = native.Fragments[fragment];
                        Rect drawn = Rect.MinMaxRect(Mathf.Max(item.Trim.xMin, item.Region.xMin), Mathf.Max(item.Trim.yMin, item.Region.yMin),
                            Mathf.Min(item.Trim.xMax, item.Region.xMax), Mathf.Min(item.Trim.yMax, item.Region.yMax));
                        Vector2[] corners = { drawn.min, new Vector2(drawn.xMax, drawn.yMin), drawn.max, new Vector2(drawn.xMin, drawn.yMax) };
                        for (int corner = 0; corner < 4; corner++)
                            Assert.That(Vector2.Distance(vertices[fragment * 4 + corner], origin + item.Transform(corners[corner]) * scale), Is.LessThan(.00001f),
                                "Every fixed and moving fragment must share one uniform transform.");
                    }
                    foreach (var anchor in native.Anchors)
                        Assert.That(Vector2.Distance(DoodleGlyphs.SegmentOrigin(tower, anchor.SlotIndex, tower.GetMountAim(anchor.SlotIndex), origin, 0, null, null),
                            origin + anchor.Muzzle * scale), Is.LessThan(.00001f), "The muzzle follows the same fitted tree.");
                }
                Assert.That(enclosure.xMax, Is.LessThanOrEqualTo(enclosure.xMin + 2.00001f), "The union of aimed poses cannot overlap its neighbour placed exactly two units away.");
                Assert.That(state.Cell, Is.EqualTo(cell), "The fit changes presentation, not simulation placement.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
