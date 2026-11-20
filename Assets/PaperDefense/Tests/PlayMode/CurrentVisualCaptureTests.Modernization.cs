#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed partial class CurrentVisualCaptureTests
    {
        readonly List<ModernizationCaptureState> modernizationStates = new List<ModernizationCaptureState>();

        [UnityTest, Explicit("Shows a real wave reward and the resulting ink stock in the production HUD.")]
        public IEnumerator CaptureWaveInkFlask()
        {
            shell.ApplyLayout(Sizes[0].x, Sizes[0].y);
            StartBattle();
            var session = game.Run.Session;
            var home = session.BaseCell;
            foreach (int offset in new[] { 0, -2, 2 })
            {
                string page = offset == 2 ? "album.piercer" : "album.guard";
                int slot = Enumerable.Range(0, game.Run.RunLoadout.Entries.Count).First(i => game.Run.RunLoadout.Entries[i]?.Id == page);
                var cell = new Cell(home.X + 2, home.Y + offset);
                Assert.That(game.Run.BuildEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None, out string reason, 180), Is.True, reason);
            }
            for (int i = 0; i < 300 && session.Structures.Any(t => !t.IsComplete); i++) game.Run.StepSimulation();
            game.Run.NextWave();
            for (int i = 0; i < 12000 && session.Phase == RunPhase.Assault; i++) game.Run.StepSimulation();
            Assert.That(session.CompletedWaves, Is.EqualTo(1));
            Assert.That(session.PendingInkFlasks, Is.EqualTo(1));
            Assert.That(session.Wells.Count, Is.Zero);
            game.Drawing.Focus(home);
            foreach (var size in Sizes)
            {
                Size(size);
                Assert.That(Find<Button>("Claim flask Blue").IsInteractable(), Is.True);
                Capture("wave-flask-choice", size, preserveBattleView: true);
            }
            Press("Claim flask Blue");
            Hud.Refresh();
            Assert.That(session.PendingInkFlasks, Is.Zero);
            Assert.That(session.ColoredInk(InkColor.Blue), Is.EqualTo(session.InkPerFlask));
            foreach (var size in Sizes)
            {
                Size(size);
                Capture("wave-flask-claimed", size, preserveBattleView: true);
            }
            yield return null;
        }

        [UnityTest, Explicit("Captures real paid modernization state and isolated color presentation at three resolutions; memory-only profiles.")]
        public IEnumerator CaptureFieldModernization()
        {
            captures.Clear(); modernizationStates.Clear();
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1280, 720), new Vector2Int(1600, 900) })
            {
                yield return TearDown();
                // Recreate a fresh production run for each resolution so paid development
                // and its memory-only profile are independent across captures.
                var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
                balance = Object.Instantiate(source);
                Assert.That(balance.mechanics.FieldModernization, Is.True);
                root = new GameObject("Modernization capture — production balance, memory only"); root.SetActive(false);
                game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true); root.SetActive(true);
                game.Run.AutomaticSimulation = false;
                shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
                foreach (var responsive in root.GetComponentsInChildren<WorkshopResponsiveLayout>(true)) responsive.enabled = false;
                yield return null;
                shell.ApplyLayout(size.x, size.y); StartBattle();
                Assert.That(game.Run.Session.Balance.Mechanics.MaximumModernizationRank, Is.EqualTo(1));

                var first = PlaceTower(); var second = PlaceTower();
                for (int step = 0; step < 1000 && game.Run.Session.Structures.Any(t => !t.IsComplete); step++) game.Run.StepSimulation();
                Assert.That(game.Run.Session.Structures.All(t => t.IsComplete), Is.True);
                game.Run.TogglePause(); game.Drawing.Inspect(null); Hud.Refresh();
                ModernizationCapture("modernization-upgrade-entry", size, first);
                ModernizationCapture("modernization-field-overview", size, first, overview: true);
                game.Drawing.SelectMode(DrawingMode.Erase);
                Press("Battle upgrades");
                Assert.That(game.Drawing.Mode, Is.EqualTo(DrawingMode.Build));
                ModernizationCapture("modernization-upgrade-prompt", size, first);
                game.Drawing.Inspect(first); Hud.Refresh();
                ModernizationCapture("modernization-base", size, first);
                Press("Battle details toggle");
                Assert.That(Find<Text>("Album battle structure details").gameObject.activeInHierarchy, Is.True);
                ModernizationCapture("minimal-details-open", size, first);
                Press("Battle details toggle");
                var slotHint = Find<Button>("Build tool 7").GetComponent<PlayerTooltip>();
                slotHint.OnPointerEnter(new PointerEventData(EventSystem.current));
                ModernizationCapture("minimal-slot-hint", size, first);
                slotHint.OnPointerExit(new PointerEventData(EventSystem.current));
                EventSystem.current.SetSelectedGameObject(Find<Button>("Modernize Power").transform.parent.gameObject);
                ModernizationCapture("minimal-direction-focus", size, first);
                EventSystem.current.SetSelectedGameObject(null); Hud.ShowHint("");
                foreach (string direction in new[] { "Power", "Mechanics", "Range", "Resilience" })
                {
                    RevealModernizationControl("Modernize " + direction);
                    ModernizationCapture("modernization-row-" + direction.ToLowerInvariant(), size, first);
                }

                RevealModernizationControl("Modernize Power");
                var tower = game.Run.Session.StructureAt(first).Value;
                var quote = game.Run.QuoteModernization(tower.Id, ModernizationDirection.Power);
                Assert.That(quote.Valid, Is.True, quote.Reason);
                int ordinaryBefore = game.Run.Session.Ink;
                Press("Modernize Power");
                Assert.That(game.Run.Session.Ink, Is.EqualTo(ordinaryBefore - quote.Cost));
                game.Run.TogglePause();
                for (int step = 0; step < 1000 && game.Run.Session.StructureAt(first).Value.Modernization.WorkRemaining > quote.WorkSeconds * .5f; step++)
                    game.Run.StepSimulation();
                game.Run.TogglePause();
                tower = game.Run.Session.StructureAt(first).Value;
                Assert.That(tower.Modernization.IsWorking, Is.True);
                Assert.That(tower.Modernization.WorkRemaining, Is.EqualTo(quote.WorkSeconds * .5f).Within(FixedStepClock.Step + .001f));
                ModernizationInspectorTop();
                ModernizationCapture("modernization-work-half", size, first);

                CompleteModernization(first); ModernizationInspectorTop();
                ModernizationCapture("modernization-complete", size, first);
                var locked = game.Run.QuoteModernization(tower.Id, ModernizationDirection.Power);
                Assert.That(locked.Valid, Is.False, "The fresh profile cannot buy rank II.");
                RevealModernizationControl("Modernize Power");
                Assert.That(Find<Button>("Modernize Power").IsInteractable(), Is.False);
                ModernizationCapture("modernization-technology-locked", size, first);

                game.Drawing.Inspect(second); Hud.Refresh(); RevealModernizationControl("Modernize Resilience");
                Press("Modernize Resilience"); CompleteModernization(second); ModernizationInspectorTop();
                Assert.That(game.Run.Session.StructureAt(first).Value.Modernization.Rank(ModernizationDirection.Power), Is.EqualTo(1));
                Assert.That(game.Run.Session.StructureAt(second).Value.Modernization.Rank(ModernizationDirection.Resilience), Is.EqualTo(1));
                ModernizationCapture("modernization-copy-resilience", size, second);
                game.Drawing.Inspect(first); Hud.Refresh(); ModernizationInspectorTop();
                ModernizationCapture("modernization-copy-power", size, first);

                // Presentation-only resource fixture: the real campaign must earn this ink.
                // Quotes, confirmation, payment, time and fixed-color state remain production paths.
                var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
                var ink = (int[])typeof(GameSession).GetField("coloredInk", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                ink[(int)InkColor.Red] = 100;
                Hud.Refresh(); RevealModernizationControl("Modernize color Red");
                var colorQuote = game.Run.QuoteColorModernization(tower.Id, InkColor.Red);
                Assert.That(colorQuote.Valid, Is.True, colorQuote.Reason);
                Press("Modernize color Red");
                Assert.That(shell.HasConfirmation, Is.True);
                ModernizationCapture("modernization-color-confirm", size, first, syntheticColorInk: true);
                Press("Confirm action");
                Assert.That(ink[(int)InkColor.Red], Is.EqualTo(100 - colorQuote.Cost));
                CompleteModernization(first); ModernizationInspectorTop();
                Assert.That(game.Run.Session.StructureAt(first).Value.Color, Is.EqualTo(InkColor.Red));
                Assert.That(game.Run.Session.StructureAt(first).Value.Modernization.ColorRank, Is.EqualTo(1));
                Assert.That(game.Run.QuoteColorModernization(tower.Id, InkColor.Blue).Valid, Is.False);
                ModernizationCapture("modernization-color-fixed", size, first, syntheticColorInk: true);
                RevealModernizationControl("Modernize color Red");
                ModernizationCapture("modernization-color-fixed-row", size, first, syntheticColorInk: true);
                game.Drawing.Inspect(null); game.Drawing.SetMessage(""); Hud.ShowHint(""); Hud.Refresh();
                ModernizationCapture("modernization-field-color", size, first, syntheticColorInk: true);
                ModernizationCapture("modernization-field-color-overview", size, first, syntheticColorInk: true, overview: true);
                for (int extra = 0; extra < 3; extra++) PlaceTower();
                game.Run.TogglePause();
                for (int step = 0; step < 1000 && game.Run.Session.Structures.Any(t => !t.IsComplete); step++) game.Run.StepSimulation();
                game.Run.TogglePause();
                Assert.That(game.Run.Session.Structures.Count(t => t.ResolvedEntry?.Kind == AlbumEntryKind.Tower), Is.EqualTo(5));
                ModernizationCapture("modernization-field-dense", size, first, syntheticColorInk: true);
                ModernizationCapture("modernization-field-dense-overview", size, first, syntheticColorInk: true, overview: true);
                Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            }
            WriteManifest("modernization-manifest.json");
            File.WriteAllText(Path.Combine(Folder, "modernization-state-manifest.json"), JsonUtility.ToJson(new ModernizationCaptureManifest {
                note = "Production balance and fresh memory-only profile. Baseline copies and ordinary rank-I works use real prices and simulation time. No outcome, health, technology or work duration is injected. Only color-confirm/fixed captures inject 100 red ink for isolated UI presentation; those screenshots are marked presentationOnly. Not campaign balance evidence.",
                captures = modernizationStates.ToArray()
            }, true));
        }

        void RevealModernizationControl(string name)
        {
            Hud.Refresh(); Canvas.ForceUpdateCanvases();
            EventSystem.current.SetSelectedGameObject(null);
            var button = Find<Button>(name);
            // Reveal the full row so the stat preview and disabled reason accompany its button.
            var row = (RectTransform)button.transform.parent;
            UiScrollFocus.Reveal(row);
            Canvas.ForceUpdateCanvases();
            var viewport = button.GetComponentInParent<ScrollRect>().viewport;
            var corners = new Vector3[4]; row.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var at = viewport.InverseTransformPoint(corner);
                Assert.That(at.y, Is.InRange(viewport.rect.yMin - 1, viewport.rect.yMax + 1), name + " row must be fully visible.");
            }
        }

        void ModernizationInspectorTop()
        {
            EventSystem.current.SetSelectedGameObject(null); Hud.Refresh(); Canvas.ForceUpdateCanvases();
            Find<Button>("Modernize Power").GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
        }

        void CompleteModernization(Cell cell)
        {
            Assert.That(game.Run.TacticalPaused, Is.True);
            game.Run.TogglePause();
            for (int step = 0; step < 1000 && game.Run.Session.StructureAt(cell).Value.Modernization.IsWorking; step++) game.Run.StepSimulation();
            game.Run.TogglePause();
            Assert.That(game.Run.Session.StructureAt(cell).Value.Modernization.IsWorking, Is.False);
            Hud.Refresh(); Canvas.ForceUpdateCanvases();
        }

        void ModernizationCapture(string name, Vector2Int size, Cell focus, bool syntheticColorInk = false, bool overview = false)
        {
            Capture(name, size, presentationOnly: syntheticColorInk, focus: overview ? (Cell?)null : focus);
            modernizationStates.Add(new ModernizationCaptureState {
                name = name + "-" + size.x + "x" + size.y, presentationOnly = syntheticColorInk,
                injectedRedInk = syntheticColorInk ? 100 : 0, ordinaryInk = game.Run.Session.Ink,
                technologyMaximum = game.Run.Session.Balance.Mechanics.MaximumModernizationRank,
                tacticalPaused = game.Run.TacticalPaused, modal = shell.HasConfirmation,
                towers = game.Run.Session.Structures.Where(t => t.ResolvedEntry?.Kind == AlbumEntryKind.Tower).Select(t => new ModernizationTowerState {
                    id = t.Id, x = t.Cell.X, y = t.Cell.Y, color = t.Color.ToString(), colorRank = t.Modernization.ColorRank,
                    ranks = new[] { t.Modernization.Ranks.Power, t.Modernization.Ranks.Mechanics, t.Modernization.Ranks.Range, t.Modernization.Ranks.Resilience },
                    working = t.Modernization.IsWorking, workRemaining = t.Modernization.WorkRemaining, workDuration = t.Modernization.WorkDuration
                }).ToArray()
            });
        }

        [Serializable] sealed class ModernizationCaptureManifest
        { public string note; public ModernizationCaptureState[] captures; }
        [Serializable] sealed class ModernizationCaptureState
        {
            public string name; public bool presentationOnly, tacticalPaused, modal;
            public int injectedRedInk, ordinaryInk, technologyMaximum; public ModernizationTowerState[] towers;
        }
        [Serializable] sealed class ModernizationTowerState
        {
            public int id, x, y, colorRank; public string color; public int[] ranks;
            public bool working; public float workRemaining, workDuration;
        }
    }
}
#endif
