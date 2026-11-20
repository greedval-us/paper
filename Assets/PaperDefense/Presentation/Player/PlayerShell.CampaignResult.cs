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
        Text campaignResultStatus, campaignResultGold, campaignResultXp, campaignResultReceipt;
        Button campaignNext, campaignSave;

        void CampaignResult()
        {
            var mission = game.Run.ActiveMission ?? game.Run.SelectedMission;
            bool victory = game.Run.Session.Phase == RunPhase.Victory;
            MenuHeader(victory ? "Страница защищена" : "Линия прорвана", game.ReturnToHome,
                "Тетрадь " + mission.NotebookIndex + " · Страница " + mission.PageIndex,
                "За завершённые волны начисляются золото и опыт. Победа даёт дополнительную награду и открывает следующую страницу.\n\nТри звезды — замок сохранил почти всё здоровье. Две — удержал большую часть. Для открытия следующей страницы достаточно любой победы.\n\nЕсли сохранение не удалось, можно повторить его на этом экране. Награда за одну попытку не начисляется дважды.");
            var home = page.GetComponentsInChildren<Button>().First(button => button.name == "Back");
            home.name = "Result home"; home.GetComponentInChildren<Text>().text = "Меню";
            float margin = Mathf.Min(32, W * .025f), width = W - margin * 2, gap = 16;
            float leftWidth = Mathf.Min(720, width * .47f), rightWidth = width - leftWidth - gap;
            float bodyHeight = H - 180;
            bool desktop = W >= 1100 && H >= 650;
            var session = game.Run.Session;
            var sheet = ui.Panel(page, "Campaign result page", new Rect(margin, 88, leftWidth, bodyHeight), PlayerUi.Card);
            float sealSize = Mathf.Min(78, leftWidth * .18f);
            var seal = PlayerUi.Graphic<MissionSealGraphic>(sheet, "Mission outcome seal",
                new Rect(leftWidth - sealSize - 12, (88 - sealSize) * .5f, sealSize, sealSize));
            seal.Victory = victory;
            float captionWidth = leftWidth - sealSize - 40;
            var title = CampaignText(sheet, mission.Title, new Rect(18, 12, captionWidth, 42), 30);
            title.fontStyle = FontStyle.Bold;
            CampaignText(sheet, mission.NotebookTitle, new Rect(18, 54, captionWidth, 28), 18, PlayerUi.Muted);
            var preview = PlayerUi.Graphic<NeutralMissionMapGraphic>(sheet, "Result battlefield", new Rect(10, 90, leftWidth - 20, bodyHeight - (desktop ? 216 : 194)));
            preview.Bind(session);
            void ShowFirstRefitInvitation()
            {
                if (!game.CanStartFirstRefitTutorial || sheet.Find("Result first refit") != null) return;
                PlayerUi.Place(preview.rectTransform, new Rect(10, 184, leftWidth - 20, bodyHeight - (desktop ? 216 : 194) - 94));
                var invitation = CampaignText(sheet, "Открыт длинный ствол.\nПеределай и испытай свою башню.",
                    new Rect(18, 87, leftWidth - 36, 42), 16, PlayerUi.Ink);
                invitation.name = "First refit invitation";
                invitation.alignment = TextAnchor.UpperLeft;
                NotebookAction(sheet, "Result first refit", new Rect(18, 130, leftWidth - 36, 44), PlayerIcon.Pencil,
                    "Первая переделка", game.StartFirstRefitTutorial, true);
            }
            ShowFirstRefitInvitation();
            ShowDefeatReviewInvitation(sheet, preview, leftWidth, bodyHeight, desktop);
            var result = game.Run.MissionResult;
            string waves = "Волны  " + session.CompletedWaves + " / " + session.Balance.Waves.Count;
            if (desktop)
            {
                CampaignText(sheet, waves, new Rect(20, bodyHeight - 114, leftWidth - 160, 34), 22).name = "Result completed waves";
                if (victory) CampaignText(sheet, Stars(result?.Stars ?? 1), new Rect(leftWidth - 130, bodyHeight - 114, 110, 34), 25,
                    FieldMenuSkin.Accent).alignment = TextAnchor.MiddleRight;
                int waveCount = session.Balance.Waves.Count;
                float segmentWidth = (leftWidth - 40 - Mathf.Max(0, waveCount - 1) * 4) / Mathf.Max(1, waveCount);
                for (int i = 0; i < waveCount; i++)
                    ui.Panel(sheet, "Wave segment " + (i + 1), new Rect(20 + i * (segmentWidth + 4), bodyHeight - 72, segmentWidth, 6),
                        i < session.CompletedWaves ? FieldMenuSkin.Accent : FieldMenuSkin.Raised);
                float metricWidth = (leftWidth - 40) * .5f;
                CampaignMetric(sheet, "Result castle health", PlayerIcon.Heart, "Замок   " + Mathf.CeilToInt(session.BaseHealth) + " / " +
                    Mathf.CeilToInt(session.Balance.BaseHealth), new Rect(20, bodyHeight - 48, metricWidth, 28));
                CampaignMetric(sheet, "Result enemy kills", PlayerIcon.Target, "Побеждено врагов   " + session.Kills,
                    new Rect(20 + metricWidth, bodyHeight - 48, metricWidth, 28));
            }
            else
            {
                CampaignText(sheet, victory ? Stars(result?.Stars ?? 1) : "План можно улучшить",
                    new Rect(18, bodyHeight - 96, leftWidth - 36, 36), 29).alignment = TextAnchor.MiddleCenter;
                CampaignText(sheet, victory ? "Собери новую оборону для следующей страницы" : "Пересмотри позиции, поддержку и боевой набор",
                    new Rect(18, bodyHeight - 56, leftWidth - 36, 48), 17, PlayerUi.Muted).alignment = TextAnchor.MiddleCenter;
            }

            var report = ui.Panel(page, "Result mission report", new Rect(margin + leftWidth + gap, 88, rightWidth, bodyHeight), PlayerUi.Card);
            Rect detailsBounds = new Rect(14, 12, rightWidth - 28, bodyHeight - 126);
            const float offerHeight = 88;
            float detailsHeight = desktop ? 364 : 335;
            bool scrollDetails = detailsBounds.height < detailsHeight + offerHeight;
            var content = !scrollDetails ? ui.Panel(report, "Campaign result details", detailsBounds, Color.clear) :
                ui.Scroll(report, "Campaign result details", detailsBounds, detailsHeight);
            float w = rightWidth - 46;
            if (desktop)
            {
                MissionSectionTitle(content, "Награды за миссию", new Rect(4, 0, w - 8, 48), 29);
                float cardWidth = (w - 12) * .5f;
                campaignResultGold = CampaignRewardCard(content, new Rect(0, 59, cardWidth, 84), true);
                campaignResultXp = CampaignRewardCard(content, new Rect(cardWidth + 12, 59, cardWidth, 84), false);
            }
            else
            {
                CampaignText(content, waves, new Rect(0, 0, w, 38), 25).name = "Result completed waves";
                campaignResultGold = CampaignText(content, "", new Rect(0, 44, w * .5f, 40), 27);
                campaignResultXp = CampaignText(content, "", new Rect(w * .5f, 44, w * .5f, 40), 27);
            }
            campaignResultGold.name = "Result gold reward";
            campaignResultXp.name = "Result XP reward";
            resultProgress = CampaignText(content, ProgressCopy(), new Rect(4, desktop ? 152 : 90, w - 8, 30), 19);
            resultProgress.name = "Result progress";
            string firstClear = result != null && result.FirstClear ? "Первая победа: +" + result.FirstClearGold + " золота · +" + result.FirstClearXp + " XP" :
                victory ? "Повторная победа: награды за волны и завершение" : "Награда за завершённые волны сохранится";
            var firstLabel = CampaignText(content, firstClear, new Rect(4, desktop ? 188 : 130, w - 8, 54), 19);
            firstLabel.name = "Campaign first clear reward";
            string rewardParts = "";
            if (victory && result != null && result.FirstClear && game.Run.AssemblyCatalog != null)
                rewardParts = string.Join(", ", mission.UnlockPartIds.Select(id => game.Run.AssemblyCatalog.Definitions.FirstOrDefault(part => part.Id == id))
                    .Where(part => part != null).Select(part => GameText.Resolve(part.Name)));
            Text notesTitle = desktop ? MissionSectionTitle(content, string.IsNullOrEmpty(rewardParts) ? "Тактика страницы" : "Открытые детали",
                new Rect(4, 251, w - 8, 32)) : null;
            var notes = CampaignText(content, string.IsNullOrEmpty(rewardParts) ? mission.Tactics : desktop ? rewardParts : "Новые детали: " + rewardParts,
                new Rect(4, desktop ? 291 : 190, w - 8, desktop ? 72 : 116), 19, desktop ? FieldMenuSkin.Muted : PlayerUi.Ink);
            notes.name = "Campaign result notes"; notes.alignment = TextAnchor.UpperLeft;
            void ShowAffordableModule()
            {
                var offer = FindResultModule();
                if (offer.part == null || content.Find("Result inspect module") != null) return;
                if (scrollDetails) content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, detailsHeight + offerHeight);
                firstLabel.rectTransform.anchoredPosition += Vector2.down * offerHeight;
                notes.rectTransform.anchoredPosition += Vector2.down * offerHeight;
                if (notesTitle != null) notesTitle.rectTransform.anchoredPosition += Vector2.down * offerHeight;
                float y = desktop ? 188 : 127;
                var target = GameText.Resolve(offer.entry.Name);
                if (offer.slot > 0) target += " · орудие " + (offer.slot + 1);
                CampaignText(content, target + " · " + offer.gain,
                    new Rect(4, y, w - 8, 28), 17, PlayerUi.Ink).name = "Result module benefit";
                NotebookAction(content, "Result inspect module", new Rect(4, y + 30, w - 8, 47), PlayerIcon.Pencil,
                    "Сравнить · " + GameText.Resolve(offer.part.Name) + " · " + offer.part.UnlockGold + " зол.",
                    () => game.InspectResultModule(offer.entry, offer.part, offer.slot));
            }
            ShowAffordableModule();
            NotebookAction(report, "Result statistics", new Rect(14, bodyHeight - 110, rightWidth - 28, 44), PlayerIcon.Book,
                "Статистика миссии", OpenCampaignResultStatistics);
            campaignResultStatus = CampaignText(report, "", new Rect(14, bodyHeight - 62, rightWidth - 28, 25), 17);
            campaignResultStatus.name = "Result reward status";
            campaignResultReceipt = CampaignText(report, "", new Rect(14, bodyHeight - 35, rightWidth - 28, 24), 16, PlayerUi.Muted);
            campaignResultReceipt.name = "Result receipt";
            campaignSave = NotebookAction(report, "Result retry save", new Rect(14, bodyHeight - 110, rightWidth - 28, 44), PlayerIcon.Save,
                "Повторить сохранение", () => {
                    game.Run.RetrySave(out _);
                    ShowFirstRefitInvitation();
                    ShowAffordableModule();
                    RefreshCampaignResult();
                }, true);
            campaignSave.gameObject.SetActive(false);
            float buttonWidth = (width - gap * 3) / 4, footerY = H - 76;
            NotebookAction(page, "Result map", new Rect(margin, footerY, buttonWidth, 52), PlayerIcon.Book, "Тетради", game.OpenCampaign);
            NotebookAction(page, "Result album", new Rect(margin + buttonWidth + gap, footerY, buttonWidth, 52), PlayerIcon.Pencil, "Чертежи", game.ReturnToAlbumFromResult);
            NotebookAction(page, "Result retry", new Rect(margin + (buttonWidth + gap) * 2, footerY, buttonWidth, 52), PlayerIcon.Restart, "Повторить", game.Retry);
            campaignNext = NotebookAction(page, "Result next mission", new Rect(margin + (buttonWidth + gap) * 3, footerY, buttonWidth, 52), PlayerIcon.Next,
                victory ? string.IsNullOrEmpty(result?.NextMissionId) ? "Кампания пройдена" : "Дальше" : "К карте",
                victory ? (System.Action)game.NextCampaignMission : game.ReturnToMap, true);
            RefreshCampaignResult();
            RestoreDefeatReview();
        }

        // Showcase the module available at the most recent level first. Quotes use the player's equipped designs.
        static readonly string[] ResultModuleIds = {
            "head.short", "magazine.drum", "frame.outpost", "head.wide", "frame.battery", "magazine.belt", "magazine.mortar.pair"
        };

        (AlbumEntry entry, ComponentDefinition part, int slot, string gain) FindResultModule()
        {
            var run = game.Run;
            var profile = run.Workshop;
            if (!run.IsCurrentMissionRewardSaved || run.MissionResult == null || run.MissionResult.Gold == 0 ||
                run.AssemblyCatalog == null || profile == null) return default;
            var candidates = run.AssemblyCatalog.Definitions.GroupBy(p => p.Id)
                .Select(group => group.OrderByDescending(p => p.Revision).First())
                .Where(p => p.UnlockGold > 0 && !profile.IsUnlocked(p.Id) && p.RequiredLevel <= profile.Level &&
                    p.UnlockGold <= profile.Gold && (p.Category <= ComponentCategory.Turn || p.Category == ComponentCategory.Head ||
                    p.Category == ComponentCategory.Sight || p.Category == ComponentCategory.Magazine ||
                    p.Category == ComponentCategory.Trigger || p.Category == ComponentCategory.Mount))
                .OrderByDescending(p => p.RequiredLevel)
                .ThenBy(p => { int rank = System.Array.IndexOf(ResultModuleIds, p.Id); return rank < 0 ? int.MaxValue : rank; })
                .ThenBy(p => p.UnlockGold).ThenBy(p => p.Id, System.StringComparer.Ordinal).ToArray();
            if (candidates.Length == 0) return default;
            var fits = AssemblyVisualComposer.Fits;
            var draft = new WorkshopDraftSession(run, run.AssemblyCatalog, fits);
            var towers = profile.LoadoutEntries.Where(e => e?.Kind == AlbumEntryKind.Tower).Distinct().ToArray();
            foreach (var part in candidates)
            foreach (var tower in towers)
            {
                draft.Begin(tower);
                bool foundation = part.Category <= ComponentCategory.Turn;
                foreach (var mount in tower.Snapshot.Recipe.Tower.Mounts)
                {
                    int slot = mount.SlotIndex;
                    if (foundation && slot != 0) break;
                    if (foundation) draft.OpenFoundation(); else draft.OpenWeapon(slot);
                    draft.SelectCategory(part.Category);
                    if (!draft.CompatibleParts.Any(p => p.Id == part.Id)) continue;
                    var proposed = ComponentCatalogPanel.QuotePart(run, draft, part);
                    if (proposed == null || AssemblyFitRules.Validate(proposed.Snapshot.Recipe, fits).Count != 0) continue;
                    var gain = ResultModuleGain(draft.Resolved, proposed, slot);
                    if (gain != null) return (tower, part, slot, gain);
                }
            }
            return default;
        }

        string ResultModuleGain(ResolvedEntry current, ResolvedEntry proposed, int slot)
        {
            if (current == null) return null;
            var culture = TextResourcesAsset.Default.Culture;
            string Delta(string label, float from, float to) => label + " " + from.ToString("0.##", culture)
                + " → " + to.ToString("0.##", culture);
            var before = current.Weapons.First(w => w.SlotIndex == slot);
            var after = proposed.Weapons.First(w => w.SlotIndex == slot);
            if (after.Radius > before.Radius + .01f) return Delta("радиус удара", before.Radius, after.Radius);
            if (proposed.Dps > current.Dps + .1f) return Delta("урон/с", current.Dps, proposed.Dps);
            if (proposed.Cost < current.Cost) return Delta("цена в штрихах", current.Cost, proposed.Cost);
            if (after.MagazineSize > before.MagazineSize) return Delta("заряды", before.MagazineSize, after.MagazineSize);
            if (after.Range > before.Range + .01f) return Delta("дальность", before.Range, after.Range);
            if (after.TargetLimit > before.TargetLimit) return Delta("целей за выстрел", before.TargetLimit, after.TargetLimit);
            if (after.AcquisitionSeconds < before.AcquisitionSeconds - .001f) return Delta("наведение, с", before.AcquisitionSeconds, after.AcquisitionSeconds);
            if (after.Interval < before.Interval - .01f) return Delta("между выстрелами, с", before.Interval, after.Interval);
            if (proposed.EffectiveHealth > current.EffectiveHealth + .1f) return Delta("эфф. прочность", current.EffectiveHealth, proposed.EffectiveHealth);
            if (proposed.FrontDefense > current.FrontDefense + .001f) return Delta("защита спереди, %", current.FrontDefense * 100, proposed.FrontDefense * 100);
            if (proposed.Capacity > current.Capacity) return Delta("нагрузка рамы", current.Capacity, proposed.Capacity);
            return null;
        }

        Text CampaignRewardCard(Transform parent, Rect bounds, bool goldReward)
        {
            var card = ui.Panel(parent, goldReward ? "Campaign gold card" : "Campaign XP card", bounds, FieldMenuSkin.Raised);
            var art = PlayerUi.Graphic<CampaignRewardGraphic>(card, goldReward ? "Gold coins" : "Experience ribbon", new Rect(8, 9, 62, 66));
            art.Coin = goldReward;
            var amount = CampaignText(card, "", new Rect(78, 8, bounds.width - 86, 68), 28, goldReward ? FieldMenuSkin.Accent : FieldMenuSkin.Ink);
            amount.fontStyle = FontStyle.Bold;
            return amount;
        }

        void RefreshCampaignResult()
        {
            var result = game.Run.MissionResult;
            bool saved = game.Run.IsCurrentMissionRewardSaved;
            campaignResultGold.text = "+" + (result?.Gold ?? 0) + " золота";
            campaignResultXp.text = "+" + (result?.TotalXp ?? 0) + " XP";
            resultProgress.text = ProgressCopy();
            if (progress != null) progress.text = resultProgress.text;
            campaignResultStatus.text = saved ? game.Run.Workshop.SaveState == ProfileSaveState.MemoryOnly
                ? GameText.Resolve(game.Run.Workshop.SaveMessage) : "Награда и прогресс сохранены" : "Награда ожидает сохранения";
            campaignResultReceipt.text = saved ? "За попытку: " + (result?.Gold ?? 0) + " золота · " + (result?.TotalXp ?? 0) + " XP" : "Можно повторить сохранение";
            bool retry = game.Run.HasPendingRunRewards || game.Run.Workshop.HasUnsavedChanges;
            bool retryHadFocus = EventSystem.current?.currentSelectedGameObject == campaignSave.gameObject;
            campaignSave.gameObject.SetActive(retry);
            var statisticsButton = campaignSave.transform.parent.Find("Result statistics");
            if (statisticsButton != null) statisticsButton.gameObject.SetActive(!retry);
            campaignNext.interactable = game.Run.Session.Phase != RunPhase.Victory || saved;
            if (!retry && retryHadFocus)
                EventSystem.current?.SetSelectedGameObject(campaignNext.gameObject);
        }

        void OpenCampaignResultStatistics()
        {
            var stats = game.Run.Session.Statistics;
            var result = game.Run.MissionResult;
            string copy = result == null ? "" : "Волны: " + result.WaveGold + " золота · " + result.WaveXp + " XP\n" +
                "Завершение: " + result.CompletionGold + " золота · " + result.CompletionXp + " XP\n" +
                "Первая победа: " + result.FirstClearGold + " золота · " + result.FirstClearXp + " XP\n\n";
            copy += ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment, stats.RefundedInk, stats.LostInvestment, stats.PeakAliveEnemies)
                + "\n\n" + ui.T("result_stats_right", Mathf.RoundToInt(stats.TotalDamage), stats.TotalStructuresBuilt, stats.DestroyedStructures, stats.Upgrades.Count);
            var modal = UiHelp.Show(page, "Статистика миссии", copy);
            modal.GetComponentsInChildren<Button>().First(button => button.name == "Close context help").name = "Close result statistics";
        }
    }

    public sealed class CampaignRewardGraphic : MaskableGraphic
    {
        public bool Coin;
        readonly MeshPen pen = new MeshPen(null, 480);

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); pen.Clear();
            Rect bounds = rectTransform.rect;
            Color brass = FieldMenuSkin.Accent, shadow = new Color32(115, 81, 46, 255);
            if (Coin)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 center = i == 0 ? new Vector2(-7, -7) : new Vector2(5, 4);
                    pen.Disc(center - Vector2.up * 2, 20, shadow, 32);
                    pen.Disc(center, 20, brass, 32);
                    pen.Ring(center, 16.5f, 1.4f, shadow, 32);
                    if (i == 1)
                    {
                        pen.Quad(center + new Vector2(-9, 2), center + new Vector2(-4, -6),
                            center + new Vector2(5, -6), center + new Vector2(9, 2), shadow);
                        pen.Triangle(center + new Vector2(-5, 1), center + new Vector2(0, 9), center + new Vector2(5, 1), shadow);
                    }
                }
            }
            else
            {
                Color enamel = new Color32(141, 194, 166, 255);
                for (int side = -1; side <= 1; side += 2)
                    pen.Quad(new Vector2(side * 3, -4), new Vector2(side * 14, -4), new Vector2(side * 17, -26),
                        new Vector2(side * 7, -21), enamel);
                var center = new Vector2(0, 5);
                pen.Disc(center, 21, enamel, 10);
                pen.Disc(center, 18, FieldMenuSkin.Panel, 10);
                for (int i = 0; i < 10; i++)
                {
                    float angle = (90 + i * 36) * Mathf.Deg2Rad, next = angle + 36 * Mathf.Deg2Rad;
                    float radius = i % 2 == 0 ? 14 : 6, nextRadius = i % 2 == 0 ? 6 : 14;
                    pen.Triangle(center, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                        center + new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * nextRadius, enamel);
                }
            }
            pen.CopyTo(target, bounds.center, Mathf.Min(bounds.width, bounds.height) / 62);
        }
    }
}
