using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        Text resultRewardTotals, resultCompletedWaves, resultRewardStatus, resultReceipt, resultProgress;
        Button resultRetrySave, resultHome;
        Rect resultStatusFullBounds, resultStatusRetryBounds;

        void ResetAlbumResult()
        {
            resultRewardTotals = resultCompletedWaves = resultRewardStatus = resultReceipt = resultProgress = null;
            resultRetrySave = resultHome = null;
        }

        void AlbumResult()
        {
            bool victory = game.Run.Session.Phase == RunPhase.Victory;
            NotebookPageBackdrop("Art/Player/FoundationPageDecor", "Result landscape border");
            float scale = Mathf.Min(W / 1600f, H / 900f);
            var composition = ui.Panel(page, "Result composition", new Rect((W - 1600 * scale) * .5f,
                (H - 900 * scale) * .5f, 1600 * scale, 900 * scale), Color.clear);
            Rect R(float x, float y, float width, float height) => new Rect(x * scale, y * scale, width * scale, height * scale);
            int F(float size) => Mathf.Max(16, Mathf.RoundToInt(size * scale));
            ResultLandscape(composition, scale, victory);

            var banner = ui.Panel(composition, "Result title ribbon", victory ? R(385, 14, 830, 100) : R(140, 62, 810, 126), PlayerUi.Card);
            NotebookLabel(banner, JourneyCopy(victory ? "result.victory" : "result.defeat"),
                new Rect(12 * scale, 0, (victory ? 806 : 786) * scale, (victory ? 100 : 126) * scale),
                F(victory ? 64 : 66), alignment: TextAnchor.MiddleCenter);
            NotebookLabel(composition, ui.T("mission"), victory ? R(520, 108, 560, 42) : R(200, 188, 690, 50),
                F(32), alignment: TextAnchor.MiddleCenter);

            var rewards = ui.Panel(composition, "Result reward sheet", victory ? R(390, 156, 820, 80) : R(1010, 86, 510, 184), PlayerUi.Card);
            resultCompletedWaves = NotebookLabel(rewards, "", victory ? R(18, 8, 268, 58) : R(20, 8, 470, 46), F(28));
            resultCompletedWaves.name = "Result completed waves";
            resultRewardTotals = NotebookLabel(rewards, "", victory ? R(286, 4, 510, 64) : R(20, 62, 470, 92),
                F(victory ? 35 : 38), alignment: TextAnchor.MiddleCenter);
            resultRewardTotals.name = "Result reward totals";
            var totals = RunRewardTotals.Calculate(game.Run.Session.CompletedWaves, victory);
            if (victory)
            {
                var breakdown = ui.Panel(composition, "Result reward breakdown", R(350, 244, 900, 48), PlayerUi.Card);
                NotebookLabel(breakdown, JourneyCopy("result.breakdown", totals.WaveGold, totals.WaveXp, totals.VictoryGold, totals.VictoryXp),
                    R(16, 0, 868, 48), F(23), PlayerUi.Ink, TextAnchor.MiddleCenter);
            }
            var progressSheet = ui.Panel(composition, "Result progress sheet", victory ? R(350, 302, 900, 60) : R(1010, 282, 510, 58), PlayerUi.Card);
            resultProgress = NotebookLabel(progressSheet, "", victory ? R(20, 0, 860, 60) : R(20, 0, 470, 58),
                F(27), alignment: TextAnchor.MiddleCenter);
            resultProgress.name = "Result progress";
            var report = ui.Panel(composition, "Result mission report", victory ? R(350, 608, 900, 192) : R(1010, 352, 510, 360), PlayerUi.Card);
            float reportWidth = victory ? 900 : 510, reportHeight = victory ? 192 : 360;
            NotebookAction(report, "Result statistics", R(16, 8, reportWidth - 32, 40), PlayerIcon.Book,
                "Статистика миссии", OpenAlbumResultStatistics);
            var stats = game.Run.Session.Statistics;
            string reportCopy = ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment, stats.RefundedInk,
                stats.LostInvestment, stats.PeakAliveEnemies);
            if (victory)
            {
                string[] financialRows = reportCopy.Split('\n');
                for (int metric = 0; metric < 3 && metric < financialRows.Length; metric++)
                    NotebookLabel(report, financialRows[metric], R(18 + metric * 292, 50, 280, 58), F(20), PlayerUi.Ink, TextAnchor.MiddleCenter);
            }
            else NotebookLabel(report, reportCopy, R(18, 52, reportWidth - 36, 180), F(21), PlayerUi.Ink);
            resultRewardStatus = NotebookLabel(report, "", R(18, reportHeight - 80, reportWidth - 36, 48), F(20), alignment: TextAnchor.MiddleCenter);
            resultRewardStatus.name = "Result reward status";
            resultStatusFullBounds = R(18, reportHeight - 80, reportWidth - 36, 48);
            float saveWidth = victory ? 320 : 210;
            resultStatusRetryBounds = R(18, reportHeight - (victory ? 80 : 98), reportWidth - saveWidth - 54, victory ? 48 : 64);
            resultReceipt = NotebookLabel(report, "", R(18, reportHeight - 31, reportWidth - 36, 27), F(17), PlayerUi.Muted, TextAnchor.MiddleCenter);
            resultReceipt.name = "Result receipt";
            resultRetrySave = NotebookAction(report, "Result retry save", R(reportWidth - saveWidth - 18, reportHeight - 80, saveWidth, 46),
                PlayerIcon.Save, "Повторить сохранение", () => { game.Run.RetrySave(out _); RefreshAlbumResult(); }, true);
            const float buttonWidth = 260, gap = 18, footer = 816, left = 253;
            resultHome = NotebookAction(composition, "Result home", R(left, footer, buttonWidth, 64), PlayerIcon.Home,
                JourneyCopy("result.home"), game.ReturnToHome);
            NotebookAction(composition, "Result map", R(left + buttonWidth + gap, footer, buttonWidth, 64), PlayerIcon.Map,
                "На карту", game.ReturnToMap);
            NotebookAction(composition, "Result album", R(left + (buttonWidth + gap) * 2, footer, buttonWidth, 64), PlayerIcon.Book,
                JourneyCopy("result.album"), game.ReturnToAlbumFromResult);
            NotebookAction(composition, "Result retry", R(left + (buttonWidth + gap) * 3, footer, buttonWidth, 64), PlayerIcon.Restart,
                "Ещё попытка", game.Retry, true);
            RefreshAlbumResult();
            EventSystem.current?.SetSelectedGameObject(resultHome.gameObject);
        }

        void ResultLandscape(Transform parent, float scale, bool victory)
        {
            Rect R(float x, float y, float width, float height) => new Rect(x * scale, y * scale, width * scale, height * scale);
            // Reuse the separate game sprites. No concept screen is baked behind live reward labels.
            for (int side = 0; side < 2; side++)
            {
                float edge = side == 0 ? -34 : 1320;
                ui.Art(parent, "terrain.mountain", R(edge, 30, 320, 240));
                ui.Art(parent, "terrain.ridge", R(edge + (side == 0 ? 85 : -90), 194, 260, 120));
                for (int tree = 0; tree < 5; tree++)
                    ui.Art(parent, "terrain.pine", R(edge + tree % 2 * 74, 250 + tree * 82, 120 + tree % 2 * 30, 170));
            }
            if (victory)
            {
                var frozen = game.Run.RunLoadout?.Entries.Where(entry => entry != null).ToArray() ?? System.Array.Empty<AlbumEntry>();
                var towers = frozen.Where(entry => entry.Kind == AlbumEntryKind.Tower || entry.Kind == AlbumEntryKind.Legacy).Take(4).ToArray();
                Rect[] towerBounds = { R(270, 435, 230, 150), R(425, 365, 245, 170), R(1100, 435, 230, 150), R(940, 365, 245, 170) };
                for (int i = 0; i < towers.Length; i++) ResultFrozenAssembly(parent, towers[i], towerBounds[i]);
                var wall = frozen.FirstOrDefault(entry => entry.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Wall);
                ResultFrozenAssembly(parent, wall, R(398, 514, 138, 90));
                ResultFrozenAssembly(parent, wall, R(1064, 514, 138, 90));
                ui.Art(parent, "object.castle", R(562, 360, 476, 240));
                ResultFrozenAssembly(parent, frozen.FirstOrDefault(entry => entry.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Repair), R(496, 506, 162, 100));
                ResultFrozenAssembly(parent, frozen.FirstOrDefault(entry => entry.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Generator), R(950, 510, 136, 96));
                ui.Art(parent, "icon.flag", R(260, 388, 80, 100));
                ui.Art(parent, "icon.flag", R(1260, 400, 80, 100));
            }
            else
            {
                ui.Art(parent, "object.castle.damaged", R(130, 262, 800, 474));
                ui.Art(parent, "enemy.doodle.idle", R(94, 570, 98, 138));
                ui.Art(parent, "enemy.brute.idle", R(795, 560, 126, 154));
                ui.Art(parent, "object.wall", R(180, 700, 154, 92));
                ui.Art(parent, "terrain.rock", R(720, 720, 144, 84));
            }
        }

        void ResultFrozenAssembly(Transform parent, AlbumEntry entry, Rect bounds)
        {
            if (entry == null) return;
            if (entry.Kind == AlbumEntryKind.Legacy)
            { NotebookBlueprintIllustration(parent, entry.LegacyBlueprint, bounds); return; }
            var drawing = PlayerUi.Graphic<WeaponAssemblyGraphic>(parent, "Result frozen assembly " + entry.Id, bounds);
            drawing.SetRecipe(entry.Snapshot);
        }

        void RefreshAlbumResult()
        {
            if (resultRewardTotals == null || game.Page != PlayerPage.Result) return;
            var session = game.Run.Session;
            bool victory = session.Phase == RunPhase.Victory;
            var totals = RunRewardTotals.Calculate(session.CompletedWaves, victory);
            var receipt = game.Run.Workshop.FindRunReward(session.RunId);
            bool saved = receipt != null && receipt.Waves >= session.CompletedWaves && (!victory || receipt.VictoryBonus);
            resultCompletedWaves.text = JourneyCopy("result.waves", session.CompletedWaves, session.Balance.Waves.Count);
            resultRewardTotals.text = JourneyCopy("result.totals", totals.Gold, totals.TotalXp);
            resultProgress.text = ProgressCopy();
            resultRewardStatus.text = session.CompletedWaves == 0 ? JourneyCopy("result.no_reward") : saved
                ? game.Run.Workshop.SaveState == ProfileSaveState.MemoryOnly ? GameText.Resolve(game.Run.Workshop.SaveMessage) : JourneyCopy("result.saved")
                : JourneyCopy("result.pending");
            resultRewardStatus.color = saved ? new Color32(32, 103, 60, 255) : PlayerUi.Ink;
            resultReceipt.text = receipt != null ? JourneyCopy("result.receipt", receipt.Gold, receipt.TotalXp) : "";
            bool retry = game.Run.HasPendingRunRewards || game.Run.Workshop.HasUnsavedChanges;
            PlayerUi.Place(resultRewardStatus.rectTransform, retry ? resultStatusRetryBounds : resultStatusFullBounds);
            if (resultRetrySave.gameObject.activeSelf != retry)
            {
                bool hadFocus = EventSystem.current?.currentSelectedGameObject == resultRetrySave.gameObject;
                resultRetrySave.gameObject.SetActive(retry);
                if (!retry && hadFocus) EventSystem.current?.SetSelectedGameObject(resultHome.gameObject);
            }
        }

        void OpenAlbumResultStatistics()
        {
            var content = Sheet("Статистика миссии", 1040, 590, out float sheetWidth);
            float width = sheetWidth - 48;
            var modal = content.parent.parent.gameObject;
            var shield = page.Find("Modal input shield").gameObject;
            System.Action close = () =>
            {
                modal.SetActive(false); shield.SetActive(false); Destroy(modal); Destroy(shield);
            };
            var stats = game.Run.Session.Statistics;
            NotebookLabel(content, ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment,
                stats.RefundedInk, stats.LostInvestment, stats.PeakAliveEnemies), new Rect(16, 8, width * .5f - 24, 240), 23);
            NotebookLabel(content, ui.T("result_stats_right", Mathf.RoundToInt(stats.TotalDamage), stats.TotalStructuresBuilt,
                stats.DestroyedStructures, stats.Upgrades.Count), new Rect(width * .5f + 8, 8, width * .5f - 24, 240), 23);
            NotebookAction(content, "Close result statistics", new Rect(16, 310, width - 32, 52), PlayerIcon.Back,
                "Вернуться к итогам", close, true);
            ScopeSheet(content, close);
        }
    }
}
