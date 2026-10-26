using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyRicochetVisualTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static TrialReport RealThreeVictimChain(out GameBalance balance, out RecipeSnapshot snapshot, out SimulationEvent[] chain)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var foundation = catalog.Starters.Single(entry => entry.Id == "album.guard").Snapshot.Recipe.Tower.Foundation;
            PartRef P(string id) => new PartRef(id, 1);
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, new[] {
                new WeaponMountRecipe(0, new WeaponRecipe(P("core.ricochet"), P("head.ricochet"), P("sight.iron"),
                    P("magazine.standard"), P("trigger.standard"))) }));
            snapshot = new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions);
            var entry = new AlbumEntry("test.ricochet-render", "Ricochet renderer", 1, snapshot);
            balance = GameBalance.CreateDefault(); balance.StartingInk = 10000;
            balance.Construction.AcquisitionSeconds = 0;
            balance.Trials.MaximumTicks = 10;
            balance.Trials.SpawnX = balance.Trials.CloseTowerX + 1.5f;
            balance.Trials.SpawnY = balance.Trials.CloseTowerY + .5f;
            balance.Trials.SpawnYVariation = .6f;
            var targets = balance.Trials.For(TrialScenario.Crowd);
            targets.Count = 3; targets.SpawnInterval = .01f; targets.Speed = .01f; targets.Health = 1000;
            var resolved = AssemblyResolver.Resolve(snapshot, balance, MaterialTier.Pencil);
            Assert.That(resolved.Valid, Is.True);
            var report = TrialRunner.Run(entry, balance, TrialScenario.Crowd, resolved.Value.Cost, seed: 1337);
            var action = report.Frames.SelectMany(frame => frame.Events)
                .Where(item => item.Kind == SimulationEventKind.Shot && item.Attack == AttackKind.Ricochet)
                .GroupBy(item => (item.SourceStructureId, item.MountSlotIndex, item.ActionId))
                .FirstOrDefault(group => group.Count() >= 3);
            Assert.That(action, Is.Not.Null, "The real production weapon system must produce a three-victim chain.");
            chain = action.OrderBy(item => item.Sequence).ToArray();
            for (int segment = 1; segment < chain.Length; segment++)
            {
                Assert.That(chain[segment].From, Is.EqualTo(chain[segment - 1].To));
                Assert.That(chain[segment].SourceStructureId, Is.EqualTo(chain[0].SourceStructureId));
                Assert.That(chain[segment].MountSlotIndex, Is.EqualTo(chain[0].MountSlotIndex));
            }
            return report;
        }

        [Test] public void BattleMuzzleAdapterKeepsRealRicochetContinuationOrigins()
        {
            var report = RealThreeVictimChain(out var balance, out var snapshot, out var chain);
            var source = report.Frames[0].Towers.Single();
            var session = new GameSession(balance, starterDefenses: false);
            var resolved = AssemblyResolver.Resolve(snapshot, balance, MaterialTier.Pencil).Value;
            Assert.That(session.TryBuildEntry(new[] { source.Cell }, resolved, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            var tower = session.StructureAt(source.Cell);
            Assert.That(tower.Id, Is.EqualTo(chain[0].SourceStructureId));
            tower.GetMountState(0).Aim = source.GetMountAim(0);
            var owner = new GameObject("Isolated ricochet world adapter");
            try
            {
                var world = owner.AddComponent<NotebookWorldView>();
                // Exercise the actual render adapter without allocating live world meshes in EditMode.
                typeof(NotebookWorldView).GetField("session", Private).SetValue(world, new RunReadModel(session));
                var method = typeof(NotebookWorldView).GetMethod("VisualMuzzle", Private);
                for (int segment = 0; segment < chain.Length; segment++)
                {
                    var item = chain[segment]; var recorded = new Vector2(item.From.X, item.From.Y);
                    var rendered = (Vector2)method.Invoke(world, new object[] { item.SourceStructureId, item.MountSlotIndex, item.To, recorded });
                    AssertOrigin(segment, recorded, rendered, "battle");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        [Test] public void TrialMuzzleAdapterKeepsRealRicochetContinuationOrigins()
        {
            var report = RealThreeVictimChain(out _, out _, out var chain);
            var playback = new NotebookTrialPlayback(); playback.Bind(report); playback.Seek(chain[0].SimulationSeconds + .01);
            Assert.That(playback.Effects.Count(item => item.Kind == SimulationEventKind.Shot && item.ActionId == chain[0].ActionId), Is.GreaterThanOrEqualTo(3));
            var method = typeof(NotebookTrialRenderer).GetMethod("Muzzle", BindingFlags.Static | BindingFlags.NonPublic);
            for (int segment = 0; segment < chain.Length; segment++)
            {
                var item = chain[segment]; var recorded = new Vector2(item.From.X, item.From.Y);
                var rendered = (Vector2)method.Invoke(null, new object[] { report.Frames[playback.FrameIndex], item, playback, NotebookVisualThemeAsset.Default });
                AssertOrigin(segment, recorded, rendered, "trial");
            }
        }

        static void AssertOrigin(int segment, Vector2 recorded, Vector2 rendered, string surface)
        {
            if (segment == 0)
                Assert.That(Vector2.Distance(rendered, recorded), Is.GreaterThan(.03f), surface + ": the first segment must start at its visible barrel.");
            else Assert.That(Vector2.Distance(rendered, recorded), Is.LessThan(.00001f),
                surface + ": continuation " + segment + " must start at the preceding victim, rather than the tower.");
        }
    }
}
