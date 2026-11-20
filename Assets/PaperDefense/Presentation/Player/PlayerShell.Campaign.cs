using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        int campaignNotebook;
        string campaignPreviewId;

        public void ResetCampaignSelection() { campaignNotebook = 0; campaignPreviewId = null; }

        public void ShowCampaignMissionFailure(string reason)
        {
            UiHelp.Show(page, "Не удалось открыть страницу", reason);
        }

        void BrowseNotebook(int index)
        {
            campaignNotebook = index; campaignPreviewId = null;
            RefreshCampaignFocus("Campaign notebook " + index);
        }

        void PreviewCampaignPage(string id)
        {
            campaignPreviewId = id;
            RefreshCampaignFocus("Campaign page " + id);
        }

        void RefreshCampaignFocus(string name)
        {
            Show(PlayerPage.Campaign);
            var target = page.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == name && button.IsInteractable());
            if (target != null) EventSystem.current?.SetSelectedGameObject(target.gameObject);
        }

        void Campaign()
        {
            if (!game.Run.CampaignEnabled) { Map(); return; }
            var campaign = game.Run.Campaign; var missions = campaign.Missions;
            var selected = missions.FirstOrDefault(item => item.Definition.Id == campaignPreviewId)
                ?? missions.FirstOrDefault(item => item.Definition.Id == campaign.SelectedMissionId) ?? missions.First(item => item.IsUnlocked);
            if (campaignNotebook == 0) campaignNotebook = selected.Definition.NotebookIndex;
            if (selected.Definition.NotebookIndex != campaignNotebook) selected = missions.First(item => item.Definition.NotebookIndex == campaignNotebook);
            var mission = selected.Definition; campaignPreviewId = mission.Id;
            CompactMenuHeader("Кампания", () => game.Navigate(PlayerPage.Home), missions.Count(item => item.IsCompleted) + " / " + missions.Count + " страниц",
                MissionBriefingHelp(mission, selected.IsCompleted));
            bool wide = W >= 1180 && H >= 710;
            float margin = Mathf.Min(40, W * .025f), gap = wide ? 16 : 10, contentWidth = W - margin * 2;
            float tabY = W >= 1120 ? 86 : 68, tabHeight = wide ? 64 : 42;
            float tabWidth = (contentWidth - gap * 2) / 3;
            for (int book = 1; book <= 3; book++)
            {
                int notebook = book; var entries = missions.Where(item => item.Definition.NotebookIndex == notebook).ToArray();
                var tab = CompactMenuAction(page, "Campaign notebook " + notebook, new Rect(margin + (notebook - 1) * (tabWidth + gap), tabY, tabWidth, tabHeight),
                    PlayerIcon.Book, entries[0].Definition.NotebookTitle + " · " + entries.Count(item => item.IsCompleted) + "/3", () => BrowseNotebook(notebook));
                tab.GetComponentInChildren<PlayerIconGraphic>().enabled = false;
                var cover = PlayerUi.Graphic<CampaignNotebookGraphic>(tab.transform, "Notebook cover " + notebook, wide ? new Rect(14, 7, 42, 48) : new Rect(8, 5, 25, 30));
                cover.NotebookIndex = notebook;
                if (wide)
                {
                    var copy = tab.GetComponentInChildren<Text>();
                    copy.text = entries[0].Definition.NotebookTitle;
                    PlayerUi.Place(copy.rectTransform, new Rect(70, 4, tabWidth - 145, tabHeight - 8));
                    copy.resizeTextMaxSize = 22; copy.resizeTextMinSize = 16;
                    NotebookLabel(tab.transform, entries.Count(item => item.IsCompleted) + " / " + entries.Length,
                        new Rect(tabWidth - 66, 5, 48, tabHeight - 10), 16, PlayerUi.Muted, TextAnchor.MiddleRight);
                }
                FieldMenuSkin.Tab(tab);
                ui.MarkSelected(tab, notebook == campaignNotebook);
            }
            float bodyY = tabY + tabHeight + gap, bodyHeight = H - bodyY - 72;
            float listWidth = wide ? Mathf.Clamp(contentWidth * .21f, 252, 336) : Mathf.Clamp(contentWidth * .31f, 248, 320);
            float dossierWidth = wide ? Mathf.Clamp(contentWidth * .24f, 292, 380) : 0;
            float detailsWidth = contentWidth - listWidth - gap - (wide ? dossierWidth + gap : 0);
            var list = ui.Panel(page, "Campaign pages", new Rect(margin, bodyY, listWidth, bodyHeight), Color.clear);
            var pages = missions.Where(item => item.Definition.NotebookIndex == campaignNotebook).ToArray();
            float rowHeight = Mathf.Clamp(bodyHeight * .19f, 76, wide ? 112 : 84);
            for (int i = 0; i < pages.Length; i++)
            {
                var item = pages[i]; var definition = item.Definition;
                var button = ui.Button(list, "Campaign page " + definition.Id, new Rect(0, i * (rowHeight + 8), listWidth, rowHeight), PlayerIcon.Book, "", () => PreviewCampaignPage(definition.Id));
                button.GetComponentInChildren<PlayerIconGraphic>().enabled = false;
                var number = CampaignText(button.transform, definition.PageIndex.ToString("00"), new Rect(8, 6, wide ? 42 : 30, wide ? 42 : 30), wide ? 27 : 20, item.IsUnlocked ? FieldMenuSkin.Accent : PlayerUi.Muted); number.name = "Campaign page number " + definition.Id;
                float titleX = wide ? 56 : 44;
                var title = CampaignText(button.transform, definition.Title, new Rect(titleX, 6, listWidth - titleX - 12, wide ? 44 : 34), wide ? 20 : 16); title.name = "Campaign page title " + definition.Id;
                title.fontStyle = FontStyle.Bold;
                var waves = CampaignText(button.transform, WaveCountLabel(definition.WaveCount) + (definition.PageIndex == 3 ? " · финал" : ""), new Rect(44, rowHeight - 30, listWidth - 124, 22), 14, PlayerUi.Muted); waves.name = "Campaign page waves " + definition.Id;
                string state = item.IsCompleted ? Stars(item.BestStars) : item.IsUnlocked ? "Открыта" : "Закрыта";
                var status = CampaignText(button.transform, state, new Rect(listWidth - 76, rowHeight - 30, 68, 22), 14, item.IsUnlocked ? PlayerUi.Ink : PlayerUi.Muted); status.name = "Campaign page state " + definition.Id;
                ui.MarkSelected(button, definition.Id == mission.Id);
                contextHints.Tip(button.gameObject, definition.Title + " · " + WaveCountLabel(definition.WaveCount) + "\n" + (item.IsUnlocked ? state : CampaignLockReason(definition)));
            }
            float notesY = rowHeight * 3 + 28, notesHeight = Mathf.Min(174, bodyHeight - notesY);
            if (!wide && notesHeight > 70)
            {
                var notes = ui.Panel(list, "Campaign mission notes", new Rect(0, notesY, listWidth, notesHeight), PlayerUi.Card);
                CampaignText(notes, mission.Subtitle, new Rect(10, 6, listWidth - 20, 40), 14, PlayerUi.Muted);
                var tactic = CampaignText(notes, mission.Tactics, new Rect(10, 50, listWidth - 20, notesHeight - 58), 14); tactic.resizeTextForBestFit = false; tactic.alignment = TextAnchor.UpperLeft;
                contextHints.Tip(tactic.gameObject, mission.Tactics);
            }
            var details = ui.Panel(page, "Campaign mission preview", new Rect(margin + listWidth + gap, bodyY, detailsWidth, bodyHeight), PlayerUi.Card);
            var heading = CampaignText(details, mission.Title, new Rect(wide ? 20 : 12, wide ? 12 : 5, detailsWidth - (wide ? 40 : 24), wide ? 48 : 30), wide ? 32 : 22); heading.name = "Campaign mission title";
            heading.fontStyle = FontStyle.Bold;
            var preview = game.Run.CreateMissionPreview(mission.Id);
            var previewModel = new RunReadModel(preview);
            float rewardY = bodyHeight - 120, metricsY = rewardY - 34, goalY = metricsY - 30, mapY = 42;
            if (wide) { mapY = 94; metricsY = bodyHeight - 76; goalY = metricsY + 34; }
            if (wide) CampaignText(details, mission.Subtitle, new Rect(20, 61, detailsWidth - 40, 24), 16, PlayerUi.Muted);
            float mapBottom = wide ? metricsY : goalY;
            if (previewModel.Balance.Waves.Count > 0 && previewModel.Balance.Waves[0].IsHorde)
            {
                mapBottom -= 40;
                MissionScoutButton(details, "Campaign wave scout", new Rect(12, mapBottom, detailsWidth - 24, 34), previewModel.Balance);
            }
            var map = PlayerUi.Graphic<NeutralMissionMapGraphic>(details, "Campaign map preview", new Rect(12, mapY, detailsWidth - 24, Mathf.Max(80, mapBottom - mapY - 8)));
            map.Bind(previewModel);
            var goal = CampaignText(details, "Цель: сохранить замок после " + mission.WaveCount + " волн.", new Rect(12, goalY, detailsWidth - 24, 24), 14); goal.name = "Campaign objective";
            float cell = (detailsWidth - 24) / 3;
            CampaignMetric(details, "Campaign waves", PlayerIcon.Flag, "Волны: " + preview.Balance.Waves.Count, new Rect(12, metricsY, cell, 28));
            CampaignMetric(details, "Campaign castle health", PlayerIcon.Shield, "Замок: " + preview.Balance.BaseHealth.ToString("0.##"), new Rect(12 + cell, metricsY, cell, 28));
            CampaignMetric(details, "Campaign starting ink", PlayerIcon.Pencil, "Старт: " + preview.Balance.StartingInk, new Rect(12 + cell * 2, metricsY, cell, 28));
            if (wide)
            {
                var dossier = ui.Panel(page, "Campaign mission dossier", new Rect(W - margin - dossierWidth, bodyY, dossierWidth, bodyHeight), PlayerUi.Paper);
                int leftPad = 16;
                float usable = dossierWidth - leftPad * 2;
                MissionSectionTitle(dossier, "Тактика", new Rect(leftPad, 12, usable, 32));
                var tactic = CampaignText(dossier, mission.Tactics, new Rect(leftPad, 54, usable, 76), 18);
                tactic.alignment = TextAnchor.UpperLeft;
                contextHints.Tip(tactic.gameObject, mission.Tactics);
                int threatsY = 140;
                float threatsHeight = MissionOpposition(dossier, previewModel.Balance, leftPad, threatsY, usable);
                float rewardsTop = threatsY + threatsHeight + 12;
                MissionSectionTitle(dossier, "Награды", new Rect(leftPad, rewardsTop, usable, 28));
                MissionRewardSummary(dossier, "Campaign", mission, selected.IsCompleted, new Rect(leftPad, rewardsTop + 34, usable, 120));
            }
            else MissionRewardSummary(details, "Campaign", mission, selected.IsCompleted, new Rect(12, rewardY, detailsWidth - 24, 112));
            float footerY = H - 60;
            CompactMenuAction(page, "Campaign album", new Rect(margin, footerY, 146, 44), PlayerIcon.Book, "Альбом", game.OpenAlbum);
            float actionWidth = wide ? dossierWidth : Mathf.Min(340, detailsWidth);
            var open = CompactMenuAction(page, "Open mission", new Rect(W - margin - actionWidth, footerY, actionWidth, 44), selected.IsUnlocked ? PlayerIcon.Next : PlayerIcon.Lock,
                selected.IsUnlocked ? selected.IsCompleted ? "Повторить страницу" : "Подготовить" : "Недоступно", () => game.OpenMission(mission.Id), true);
            open.interactable = selected.IsUnlocked;
            if (!selected.IsUnlocked) contextHints.Tip(open.gameObject, CampaignLockReason(mission));
        }

        void CampaignMetric(Transform parent, string name, PlayerIcon icon, string value, Rect bounds)
        {
            var glyph = PlayerUi.Graphic<PlayerIconGraphic>(parent, name + " icon", new Rect(bounds.x, bounds.y + 5, 18, 18)); glyph.Icon = icon; glyph.Sketch = true; glyph.color = PlayerUi.Ink;
            var label = ui.Label(parent, value, new Rect(bounds.x + 24, bounds.y, bounds.width - 26, bounds.height), 14); label.name = name;
        }

        static string CampaignLockReason(CampaignMissionDefinition mission)
            => mission.Index == 0 ? "Страница открыта." : "Сначала победи на странице «" + CampaignCatalog.Default.Missions[mission.Index - 1].Title + "».";

        Text CampaignText(Transform parent, string copy, Rect bounds, int size, Color? color = null)
        {
            var label = NotebookLabel(parent, copy, bounds, size, color);
            label.resizeTextMinSize = Mathf.Min(size, 16);
            return label;
        }

        static string Stars(int count) => new string('★', Mathf.Clamp(count, 0, 3)) + new string('☆', 3 - Mathf.Clamp(count, 0, 3));
    }
}
