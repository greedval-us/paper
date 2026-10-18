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

        void ResetAlbumResult()
        {
            resultRewardTotals = resultCompletedWaves = resultRewardStatus = resultReceipt = resultProgress = null;
            resultRetrySave = resultHome = null;
        }

        void AlbumResult()
        {
            bool victory = game.Run.Session.Phase == RunPhase.Victory;
            NotebookPageBackdrop("Art/Player/SettingsLandscape");
            float margin = Mathf.Max(24, W * .065f), width = W - margin * 2;
            NotebookLabel(page, JourneyCopy(victory ? "result.victory" : "result.defeat"),
                new Rect(margin, 18, width, 70), 52, alignment: TextAnchor.MiddleCenter);

            var rewards = ui.Panel(page, "Result reward sheet", new Rect(margin, 100, width, 126), PlayerUi.Card);
            resultCompletedWaves = NotebookLabel(rewards, "", new Rect(18, 10, width * .32f - 30, 56), 27);
            resultCompletedWaves.name = "Result completed waves";
            resultRewardTotals = NotebookLabel(rewards, "", new Rect(width * .32f, 8, width * .68f - 20, 60), 35, alignment: TextAnchor.MiddleCenter);
            resultRewardTotals.name = "Result reward totals";
            var totals = RunRewardTotals.Calculate(game.Run.Session.CompletedWaves, victory);
            NotebookLabel(rewards, JourneyCopy("result.breakdown", totals.WaveGold, totals.WaveXp, totals.VictoryGold, totals.VictoryXp),
                new Rect(18, 74, width - 36, 42), 21, PlayerUi.Muted, TextAnchor.MiddleCenter);

            var progressSheet = ui.Panel(page, "Result progress sheet", new Rect(margin, 238, width, 56), PlayerUi.Card);
            resultProgress = NotebookLabel(progressSheet, "", new Rect(20, 0, width - 40, 56), 27, alignment: TextAnchor.MiddleCenter);
            resultProgress.name = "Result progress";

            float bodyY = 308, bodyHeight = H - 418, illustrationWidth = width * .49f;
            ui.Art(page, victory ? "object.castle" : "object.castle.damaged", new Rect(margin, bodyY, illustrationWidth, bodyHeight));
            var report = ui.Panel(page, "Result mission report", new Rect(margin + width * .51f, bodyY, width * .49f, bodyHeight), PlayerUi.Card);
            float reportWidth = width * .49f;
            NotebookAction(report, "Result statistics", new Rect(16, 12, reportWidth - 32, 44), PlayerIcon.Book,
                "Статистика миссии", OpenAlbumResultStatistics);
            var stats = game.Run.Session.Statistics;
            NotebookLabel(report, ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment, stats.RefundedInk,
                stats.LostInvestment, stats.PeakAliveEnemies), new Rect(18, 68, reportWidth - 36, Mathf.Max(86, bodyHeight - 210)), 21, PlayerUi.Muted);
            resultRewardStatus = NotebookLabel(report, "", new Rect(18, bodyHeight - 138, reportWidth - 36, 54), 20, alignment: TextAnchor.MiddleCenter);
            resultRewardStatus.name = "Result reward status";
            resultReceipt = NotebookLabel(report, "", new Rect(18, bodyHeight - 82, reportWidth - 36, 30), 17, PlayerUi.Muted, TextAnchor.MiddleCenter);
            resultReceipt.name = "Result receipt";
            resultRetrySave = NotebookAction(report, "Result retry save", new Rect(16, bodyHeight - 50, reportWidth - 32, 44),
                PlayerIcon.Save, "Повторить сохранение", () => { game.Run.RetrySave(out _); RefreshAlbumResult(); }, true);

            float gap = 12, buttonWidth = (width - gap * 3) / 4, footer = H - 82;
            resultHome = NotebookAction(page, "Result home", new Rect(margin, footer, buttonWidth, 56), PlayerIcon.Home,
                JourneyCopy("result.home"), game.ReturnToHome);
            NotebookAction(page, "Result map", new Rect(margin + buttonWidth + gap, footer, buttonWidth, 56), PlayerIcon.Map,
                "На карту", game.ReturnToMap);
            NotebookAction(page, "Result album", new Rect(margin + (buttonWidth + gap) * 2, footer, buttonWidth, 56), PlayerIcon.Book,
                JourneyCopy("result.album"), game.ReturnToAlbumFromResult);
            NotebookAction(page, "Result retry", new Rect(margin + (buttonWidth + gap) * 3, footer, buttonWidth, 56), PlayerIcon.Restart,
                "Ещё попытка", game.Retry, true);
            RefreshAlbumResult();
            EventSystem.current?.SetSelectedGameObject(resultHome.gameObject);
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
