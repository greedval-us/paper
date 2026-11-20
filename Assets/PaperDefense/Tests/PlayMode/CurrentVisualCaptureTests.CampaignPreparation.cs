#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed partial class CurrentVisualCaptureTests
    {
        [UnityTest, Explicit("Shows the armored canyon forecast before deployment and on the battle HUD.")]
        public IEnumerator CaptureHordeScout()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var profile = WorkshopProfile.CreateNew(fixture.Catalog, fixture.Balance.ResolveContent(), fixture.Balance);
                foreach (var mission in CampaignCatalog.Default.Missions.Take(7))
                    Assert.That(profile.TryApplyMissionReward(mission, Guid.NewGuid(), mission.WaveCount, true, 1, out var reason), Is.True, reason);
                Assert.That(fixture.Store.Save(profile).Success, Is.True);
                game.Run.InitializeWorkshop(fixture.Store);
                Size(Sizes[0]); game.OpenCampaign();
                Press("Campaign notebook 3"); Press("Campaign page notebook-3-page-2");
                Press("Campaign wave scout"); Capture("horde-scout-canyon-briefing", Sizes[0], presentationOnly: true);
                Press("Close context help"); Press("Open mission"); Press("Prepare mission"); Press("Start battle");
                Size(Sizes[1]);
                EventSystem.current.SetSelectedGameObject(Find<Button>("Launch wave").gameObject);
                yield return null;
                Capture("horde-scout-canyon-battle", Sizes[1], preserveBattleView: true);
            }
        }

        [UnityTest, Explicit("Shows an earned module offer and its editable weapon in the production workshop.")]
        public IEnumerator CaptureEarnedWeaponModule()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store);
                Size(Sizes[0]); StartBattle(); SetOutcome(RunPhase.Victory);
                Capture("earned-module-offer", Sizes[0], presentationOnly: true);
                Press("Result inspect module");
                Assert.That(Find<Button>("Sequence install head.short").IsInteractable(), Is.True);
                Capture("earned-module-workshop", Sizes[0], presentationOnly: true);
                yield return null;
            }
        }

        [UnityTest, Explicit("Captures compact campaign rewards and all eight preparation positions; repeated-victory state is an isolated presentation fixture.")]
        public IEnumerator CaptureCompactCampaignPreparation()
        {
            captures.Clear();
            for (int index = 0; index < Sizes.Length; index++)
            {
                if (index > 0) { yield return TearDown(); yield return SetUp(); }
                var size = Sizes[index]; shell.ApplyLayout(size.x, size.y);
                game.OpenCampaign(); Capture("route-campaign-first", size);
                Press("Campaign notebook 3"); Press("Campaign page notebook-3-page-3");
                Capture("route-campaign-locked", size);
                Press("Screen help"); Capture("route-campaign-help", size); Press("Close context help");
                Press("Campaign notebook 1"); Press("Campaign page notebook-1-page-1"); Press("Open mission");
                Capture("route-mission", size);
                Press("Prepare mission"); Capture("route-preparation-full", size);
                Press("Replace album entry 7"); Capture("route-preparation-picker", size); Press("Album close");
                var gun = game.Run.Workshop.LoadoutEntries[0];
                var wall = game.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Structure &&
                    entry.Snapshot.Recipe.Structure.Family == AssemblyStructureFamily.Wall);
                for (int slot = 1; slot < ProfileLimits.LoadoutSlotCount; slot++) game.AssignAlbumEntry(slot, null);
                Assert.That(game.Run.CanStartAlbumRun(out _), Is.True);
                Capture("route-preparation-sparse", size);
                game.AssignAlbumEntry(0, wall.Id);
                Assert.That(game.Run.CanStartAlbumRun(out _), Is.False);
                Capture("route-preparation-blocked", size);
                game.AssignAlbumEntry(0, gun.Id);
                var named = new AlbumEntry("capture.long.preparation", "Полевая батарея дальнего подавления — резерв", 1, gun.Snapshot);
                var saved = game.Run.AssemblyCommands.SaveEntryAsVariant(named, Guid.NewGuid());
                Assert.That(saved.Success, Is.True, saved.Code);
                game.AssignAlbumEntry(7, saved.ResultId);
                Capture("route-preparation-long-name", size);
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(Find<Button>("Replace album entry 7").gameObject,
                    new BaseEventData(EventSystem.current));
                yield return null; // Let the live tooltip position itself before the offscreen capture.
                Canvas.ForceUpdateCanvases();
                Assert.That(Find<Text>("Hint").text, Does.Contain(named.Name));
                Capture("route-preparation-keyboard", size);
                Assert.That(Find<Text>("Hint").text, Does.Contain(named.Name));
                EventSystem.current.SetSelectedGameObject(null);
                Assert.That(game.StartBattle(), Is.True, game.Status);
                SetOutcome(RunPhase.Victory);
                game.OpenCampaign(); Press("Campaign page notebook-1-page-1");
                Capture("route-campaign-repeat", size, presentationOnly: true);
                Press("Campaign page notebook-1-page-2");
                Capture("route-campaign-unlocked", size, presentationOnly: true);
                Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            }
            File.WriteAllText(Folder + "/compact-campaign-preparation-manifest.json", JsonUtility.ToJson(new Manifest {
                capturedUtc = DateTime.UtcNow.ToString("O"), artRevision = NightNotebookArt.Revision,
                productionBalance = "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset", captures = captures.ToArray(),
                note = "Native production UI, real reward previews, loadout commands and memory-only profiles. Long-name variant is a synthetic caption fixture with a real starter recipe. Repeat/unlocked screenshots follow an injected first-mission victory and are marked presentationOnly; they prove presentation, not campaign completion or balance."
            }, true));
        }
    }
}
#endif
