using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        Button BuildHomeMissionContinuation(Transform parent, float width, out Button discard)
        {
            bool ready = game.Run.CheckMissionCheckpoint(out var reason);
            var resume = NotebookAction(parent, "Home continue mission", new Rect(0, 0, width - 162, 54),
                PlayerIcon.Play, "Продолжить миссию", game.ResumeMissionCheckpoint, true);
            resume.interactable = ready;
            discard = NotebookAction(parent, "Home discard mission", new Rect(width - 150, 5, 150, 44),
                PlayerIcon.Trash, "Удалить", game.DiscardMissionCheckpoint);
            var checkpoint = game.Run.MissionCheckpoint;
            var mission = CampaignCatalog.Default.Find(checkpoint.MissionId);
            string title = mission?.Title ?? "Сохранённая миссия";
            string waves = checkpoint.CompletedWaves + (mission == null ? "" : " / " + mission.WaveCount);
            var summary = NotebookLabel(parent, title + " · завершено волн: " + waves,
                new Rect(2, 58, width - 4, 32), 18, FieldMenuSkin.Muted);
            summary.name = "Home checkpoint summary"; summary.resizeTextMinSize = 14;
            string message = game.MissionContinuationStatus;
            if (string.IsNullOrEmpty(message) && !ready) message = GameText.Resolve(reason);
            MissionContinuationFeedback(parent, "Home checkpoint status", message, new Rect(2, 92, width - 4, 36),
                NotebookPalette.Red, "Действие не выполнено. Подробности в подсказке.");
            MissionContinuationHint(resume, message);
            MissionContinuationHint(discard, message);
            EventSystem.current?.SetSelectedGameObject(ready ? resume.gameObject : discard.gameObject);
            return resume;
        }

        void BuildPauseMissionContinuation(Transform parent, Rect actionBounds, Rect statusBounds)
        {
            bool ready = game.Run.CanSuspendMission(out var reason);
            var save = PauseAction(parent, "Pause save and exit", actionBounds, PlayerIcon.Home,
                "Сохранить и выйти", game.SaveAndExitMission);
            save.interactable = ready;
            string message = game.MissionContinuationStatus;
            bool failed = !string.IsNullOrEmpty(message);
            if (!failed) message = ready ? "Миссию можно будет продолжить с главного экрана." : GameText.Resolve(reason);
            MissionContinuationFeedback(parent, "Pause checkpoint status", message, statusBounds,
                failed ? NotebookPalette.Red : FieldMenuSkin.Muted,
                failed ? "Сохранить не удалось. Подробности в подсказке." : "Сохранение недоступно. Подробности в подсказке.");
            MissionContinuationHint(save, message);
        }

        void MissionContinuationFeedback(Transform parent, string name, string message, Rect bounds, Color color, string compact)
        {
            var feedback = NotebookLabel(parent, message, bounds, 16, color);
            feedback.name = name; feedback.resizeTextMinSize = 14;
            // Keep a readable summary when a storage diagnostic includes several sentences.
            // The complete reason remains available on the label and the action's hint.
            if (feedback.preferredHeight > bounds.height) feedback.text = compact;
            if (!string.IsNullOrEmpty(message)) contextHints.Tip(feedback.gameObject, message);
        }

        void MissionContinuationHint(Button action, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var hint = action.GetComponent<PlayerTooltip>();
            if (hint != null) hint.Message = message;
            contextHints.Tip(action.gameObject, message);
        }

        internal void RefreshMissionContinuation(string focus)
        {
            Show(game.Page);
            foreach (var button in page.GetComponentsInChildren<Button>())
                if (button.name == focus && button.IsInteractable())
                { EventSystem.current?.SetSelectedGameObject(button.gameObject); break; }
        }
    }
}
