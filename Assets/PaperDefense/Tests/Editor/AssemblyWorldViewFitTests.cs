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
            // The reference tree below is drawn directly in its authored +Y coordinate system.
            Assert.That(session.TryBuildEntry(new[] { cell }, resolution.Value, MaterialTier.Pencil, InkColor.None, out string reason, facingDegrees: 90), Is.True, reason);
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
                    Assert.That(vertices.Length, Is.GreaterThanOrEqualTo(native.Fragments.Count * 4));
                    float scale = NotebookAssemblyView.ScaleFor(tower.ResolvedEntry.Snapshot);
                    Assert.That(scale, Is.GreaterThan(0));
                    if (poseIndex++ == 0) firstScale = scale;
                    else Assert.That(scale, Is.EqualTo(firstScale).Within(.00001f), "Aiming must never pump the complete tree's view scale.");
                    var bounds = Rect.MinMaxRect(vertices.Min(point => point.x), vertices.Min(point => point.y), vertices.Max(point => point.x), vertices.Max(point => point.y));
                    Assert.That(bounds.width, Is.LessThanOrEqualTo(2.00001f), "The actual full battery art must fit neighbours with the real two-unit spacing.");
                    Assert.That(bounds.height, Is.LessThanOrEqualTo(2.00001f));
                    foreach (var point in vertices)
                    {
                        Assert.That(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y), Is.False);
                        Assert.That(Mathf.Abs(point.x - origin.x), Is.LessThanOrEqualTo(1.00001f));
                        Assert.That(Mathf.Abs(point.y - origin.y), Is.LessThanOrEqualTo(1.00001f));
                    }
                    if (poseIndex == 1) enclosure = bounds;
                    else enclosure = Rect.MinMaxRect(Mathf.Min(enclosure.xMin, bounds.xMin), Mathf.Min(enclosure.yMin, bounds.yMin),
                        Mathf.Max(enclosure.xMax, bounds.xMax), Mathf.Max(enclosure.yMax, bounds.yMax));
                    var consumed = new bool[vertices.Length];
                    for (int fragment = 0; fragment < native.Fragments.Count; fragment++)
                    {
                        var item = native.Fragments[fragment];
                        var drawn = DrawFragment(item, origin, scale);
                        Assert.That(drawn, Is.Not.Empty, item.PartId + " must contribute its own visible strokes.");
                        int found = -1;
                        for (int firstVertex = 0; firstVertex + drawn.Length <= vertices.Length; firstVertex++)
                        {
                            bool matches = true;
                            for (int vertex = 0; vertex < drawn.Length && matches; vertex++)
                                matches = !consumed[firstVertex + vertex] &&
                                    Vector2.Distance(vertices[firstVertex + vertex], drawn[vertex]) < .00001f;
                            if (matches) { found = firstVertex; break; }
                        }
                        Assert.That(found, Is.GreaterThanOrEqualTo(0), "Every fixed and moving fragment must share one uniform transform: " + item.PartId + "/" + item.SlotIndex);
                        for (int vertex = 0; vertex < drawn.Length; vertex++) consumed[found + vertex] = true;
                    }
                    Assert.That(consumed, Is.All.True, "The completed battery must contain exactly its fixed and moving fragment streams.");
                    foreach (var anchor in native.Anchors)
                        Assert.That(Vector2.Distance(DoodleGlyphs.SegmentOrigin(tower, anchor.SlotIndex, tower.GetMountAim(anchor.SlotIndex), origin, 0, null, null),
                            origin + anchor.Muzzle * scale), Is.LessThan(.00001f), "The muzzle follows the same fitted tree.");
                }
                Assert.That(enclosure.xMax, Is.LessThanOrEqualTo(enclosure.xMin + 2.00001f), "The union of aimed poses cannot overlap its neighbour placed exactly two units away.");
                Assert.That(state.Cell, Is.EqualTo(cell), "The fit changes presentation, not simulation placement.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        static Vector2[] DrawFragment(AssemblyVisualFragment fragment, Vector2 origin, float scale)
        {
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                fragment.Draw(pen, origin, 1, Color.white, scale); pen.Upload();
                return mesh.vertices.Select(point => (Vector2)point).ToArray();
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
