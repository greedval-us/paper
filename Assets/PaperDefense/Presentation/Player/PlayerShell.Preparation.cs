using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        void Preparation()
        {
            Rect composition = NotebookPageBackdrop("Art/Player/PreparationLandscape");
            NotebookScreenHeader("Подготовка", () => game.Navigate(PlayerPage.Map),
                ui.T("mission") + " · " + game.Run.Session.Balance.Waves.Count + " волн", true, composition);

            // Keep all four choices, materials and navigation on one notebook page. When height
            // is tight, decoration yields first, followed by illustration and description space.
            float scale = Mathf.Min(composition.width / 1600f, composition.height / 900f);
            float margin = 24 * scale;
            bool wide = composition.width >= 1200 && composition.height >= 700;
            float width = wide ? Mathf.Min(composition.width - margin * 2, composition.width * .77f) : composition.width - margin * 2;
            float left = composition.x + (composition.width - width) * .5f;
            float footerHeight = 56 * scale;
            float footerY = composition.yMax - 16 * scale - footerHeight;
            float gap = 12 * scale;
            float cardWidth = (width - gap * 3) / 4;
            float titleHeight = 64 * scale;
            bool ready = PreparationReady(out int validCount, out string readiness);
            float materialHeight = Mathf.Max((ready ? 80 : 100) * scale, (ready ? 68 : 94) / Layout.Scale);
            float bodyBottom = footerY - 10 * scale;
            float top = composition.y + (wide ? 280 : 100) * scale;
            float cardHeight = bodyBottom - top - titleHeight - 8 * scale - materialHeight;
            bool longNames = false;
            foreach (var blueprint in game.Run.Workshop.Loadout)
                longNames |= blueprint != null && GameText.Resolve(blueprint.Name).Length > 26;
            float minimumCardHeight = (longNames ? 330 : 250) * scale;
            if (cardHeight < minimumCardHeight)
            {
                top = Mathf.Max(composition.y + 72 * scale, top - (minimumCardHeight - cardHeight));
                cardHeight = bodyBottom - top - titleHeight - 8 * scale - materialHeight;
            }
            cardHeight = Mathf.Min(400 * scale, cardHeight);
            // Extra height belongs to the landscape above the title, rather than an empty band
            // between the material row and navigation (especially in a taller 4:3 window).
            top = bodyBottom - titleHeight - cardHeight - 8 * scale - materialHeight;
            float materialY = titleHeight + cardHeight + 8 * scale;
            float contentHeight = materialY + materialHeight;
            var body = ui.Panel(page, "Preparation composition", new Rect(left, top, width, contentHeight), Color.clear);

            var ribbon = ui.Panel(body, "Preparation ribbon", new Rect(width * .21f, 2 * scale, width * .58f, 56 * scale), PlayerUi.Card);
            NotebookLabel(ribbon, "Возьми четыре чертежа", new Rect(14 * scale, 0, width * .58f - 28 * scale, 56 * scale),
                Mathf.RoundToInt(36 * scale), alignment: TextAnchor.MiddleCenter);

            var grid = ui.Panel(body, "Preparation loadout", new Rect(0, titleHeight, width, cardHeight), Color.clear);
            var flow = grid.gameObject.AddComponent<GridLayoutGroup>();
            flow.cellSize = new Vector2(cardWidth, cardHeight);
            flow.spacing = new Vector2(gap, gap);
            flow.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            flow.constraintCount = 4;
            flow.startCorner = GridLayoutGroup.Corner.UpperLeft;
            flow.startAxis = GridLayoutGroup.Axis.Horizontal;
            flow.childAlignment = TextAnchor.UpperLeft;
            for (int i = 0; i < 4; i++) PreparationBlueprintCard(grid, i, cardWidth, cardHeight, scale);
            PreparationMaterials(body, new Rect(0, materialY, width, materialHeight), scale,
                validCount, ready ? "" : readiness);

            var footer = ui.Panel(page, "Preparation navigation", new Rect(composition.x + margin, footerY, composition.width - margin * 2, footerHeight), Color.clear);
            var footerFlow = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerFlow.spacing = 16 * scale;
            footerFlow.childControlWidth = footerFlow.childControlHeight = true;
            footerFlow.childForceExpandWidth = footerFlow.childForceExpandHeight = false;
            PreparationFooterAction(footer, "Edit album", PlayerIcon.Pencil, "В мастерскую",
                () => game.Navigate(PlayerPage.Workshop), 240 * scale, footerHeight);
            PreparationFooterAction(footer, "Open preparation album", PlayerIcon.Book, "Альбом",
                () => OpenBlueprintPicker(0), 215 * scale, footerHeight);
            var spacer = ui.Panel(footer, "Navigation space", new Rect(0, 0, 1, footerHeight), Color.clear);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            status = NotebookLabel(spacer, "", new Rect(8 * scale, 0, 100 * scale, footerHeight),
                Mathf.Max(12, Mathf.RoundToInt(17 * scale)), NotebookPalette.Red, TextAnchor.MiddleCenter);
            status.name = "Preparation status";
            status.rectTransform.anchorMin = Vector2.zero; status.rectTransform.anchorMax = Vector2.one;
            status.rectTransform.offsetMin = new Vector2(8 * scale, 0);
            status.rectTransform.offsetMax = new Vector2(-8 * scale, 0);
            var start = PreparationFooterAction(footer, "Start battle", PlayerIcon.Play, "Начать защиту",
                () => { if (PreparationReady(out _, out _)) game.StartBattle(); }, 294 * scale, footerHeight, true);
            start.interactable = ready;
            if (!ready) start.GetComponent<PlayerTooltip>().Message = readiness;
        }

        void PreparationBlueprintCard(Transform parent, int slot, float width, float height, float scale)
        {
            var blueprint = slot < game.Run.Workshop.Loadout.Count ? game.Run.Workshop.Loadout[slot] : null;
            var card = ui.Panel(parent, "Loadout " + slot, new Rect(0, 0, width, height), PlayerUi.Card);
            // The grid owns each card's position. Its internal composition is a screen-owned unit.
            bool tight = height < 330 * scale;
            float replaceHeight = (tight ? 36 : 42) * scale;
            string caption = blueprint == null ? ui.T("empty") : GameText.Resolve(blueprint.Name);
            float nameHeight = caption.Length > 26
                ? Mathf.Max(88 * scale, 72 / Layout.Scale)
                : Mathf.Max(38 * scale, 24 / Layout.Scale);
            float costHeight = 32 * scale;
            // The physical text floor needs two complete handwritten lines even on a 1024px
            // page. Text takes this space from the illustration, rather than being truncated.
            float descriptionHeight = Mathf.Max((tight ? 44 : 52) * scale, 48 / Layout.Scale);
            float artHeight = height - (64 * scale + nameHeight + descriptionHeight + replaceHeight);
            float nameY = 20 * scale + artHeight;
            float descriptionY = nameY + nameHeight;
            float costY = descriptionY + descriptionHeight + 2 * scale;
            NotebookBlueprintIllustration(card, blueprint, new Rect(15 * scale, 16 * scale, width - 30 * scale, artHeight));
            var number = ui.Panel(card, "Loadout number " + slot, new Rect(10 * scale, 8 * scale, 36 * scale, 36 * scale), PlayerUi.Card);
            NotebookLabel(number, (slot + 1).ToString(), new Rect(0, 0, 36 * scale, 36 * scale),
                Mathf.RoundToInt(28 * scale), alignment: TextAnchor.MiddleCenter);
            var name = NotebookLabel(card, caption,
                new Rect(10 * scale, nameY, width - 20 * scale, nameHeight), Mathf.RoundToInt(25 * scale),
                alignment: TextAnchor.MiddleCenter);
            name.name = "Loadout name " + slot;
            name.fontStyle = FontStyle.Bold;
            name.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            var description = NotebookLabel(card, PreparationBlueprintDescription(blueprint),
                new Rect(12 * scale, descriptionY, width - 24 * scale, descriptionHeight), Mathf.RoundToInt(18 * scale),
                alignment: TextAnchor.MiddleCenter);
            description.name = "Loadout description " + slot;
            description.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            description.resizeTextMaxSize = Mathf.Max(description.resizeTextMinSize, description.resizeTextMaxSize);
            var cost = NotebookLabel(card, blueprint == null ? "—" : ui.T("cost", game.Run.PreviewBlueprint(blueprint, game.Drawing.Tier).Cost),
                new Rect(32 * scale, costY, width - 48 * scale, costHeight), Mathf.RoundToInt(26 * scale),
                alignment: TextAnchor.MiddleCenter);
            cost.name = "Loadout cost " + slot;
            var pencil = PlayerUi.Graphic<PlayerIconGraphic>(card, "Cost pencil", new Rect(16 * scale, costY + 2 * scale, 27 * scale, 27 * scale));
            pencil.Icon = PlayerIcon.Pencil; pencil.Sketch = true; pencil.color = PlayerUi.Ink;
            NotebookInkIcon(pencil, "icon.pencil");
            NotebookAction(card, "Replace blueprint " + slot, new Rect(10 * scale, height - 6 * scale - replaceHeight, width - 20 * scale, replaceHeight),
                PlayerIcon.Restart, "Заменить", () => OpenBlueprintPicker(slot));
        }

        void PreparationMaterials(Transform parent, Rect bounds, float scale, int validCount, string readiness)
        {
            float summaryWidth = 248 * scale, gap = 12 * scale;
            float materialWidth = bounds.width - summaryWidth - gap;
            var materials = ui.Panel(parent, "Preparation material", new Rect(bounds.x, bounds.y, materialWidth, bounds.height), PlayerUi.Card);
            NotebookLabel(materials, "Материал:", new Rect(14 * scale, 6 * scale, 100 * scale, 42 * scale), Mathf.RoundToInt(20 * scale));
            float buttonWidth = (materialWidth - 130 * scale - 18 * scale) / 3;
            var choices = ui.Panel(materials, "Material choices", new Rect(116 * scale, 6 * scale, materialWidth - 130 * scale, 42 * scale), Color.clear);
            var flow = choices.gameObject.AddComponent<HorizontalLayoutGroup>();
            flow.spacing = 9 * scale; flow.childControlWidth = flow.childControlHeight = true;
            flow.childForceExpandWidth = flow.childForceExpandHeight = false;
            string[] names = { "Карандаш", "Ручка", "Фломастер" };
            for (int i = 0; i < 3; i++)
            {
                int tier = i;
                var button = NotebookAction(choices, "Starting material " + i, new Rect(0, 0, buttonWidth, 42 * scale), PlayerIcon.Pencil, names[i],
                    () => SelectPreparationMaterial((MaterialTier)tier));
                var size = button.gameObject.AddComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = buttonWidth;
                size.minHeight = size.preferredHeight = 42 * scale;
                button.GetComponent<PlayerTooltip>().Message = ui.T("material_" + i);
                ui.MarkSelected(button, (int)game.Drawing.Tier == i);
            }
            var description = NotebookLabel(materials, ui.T("material_" + (int)game.Drawing.Tier),
                new Rect(14 * scale, Mathf.Max(51 * scale, 36 / Layout.Scale), materialWidth - 28 * scale,
                    Mathf.Max(19 * scale, 26 / Layout.Scale)), Mathf.Max(Mathf.CeilToInt(15 / Layout.Scale), Mathf.RoundToInt(18 * scale)));
            description.name = "Preparation material description";
            description.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            description.resizeTextMaxSize = Mathf.Max(description.resizeTextMinSize, description.resizeTextMaxSize);

            var summary = ui.Panel(parent, "Preparation summary", new Rect(bounds.x + materialWidth + gap, bounds.y, summaryWidth, bounds.height), PlayerUi.Card);
            NotebookLabel(summary, "Старт: " + game.Run.Session.Balance.StartingInk + " штрихов",
                new Rect(14 * scale, 5 * scale, summaryWidth - 28 * scale, 30 * scale), Mathf.RoundToInt(22 * scale)).name = "Preparation starting ink";
            NotebookLabel(summary, "Чертежи: " + validCount + " / 4", new Rect(14 * scale, 36 * scale, summaryWidth - 28 * scale, 30 * scale),
                Mathf.RoundToInt(22 * scale), validCount == 4 ? PlayerUi.Ink : NotebookPalette.Red).name = "Preparation loadout count";
            if (readiness.Length > 0)
                NotebookLabel(summary, readiness, new Rect(10 * scale, 65 * scale, summaryWidth - 20 * scale, bounds.height - 69 * scale),
                    Mathf.Max(12, Mathf.RoundToInt(16 * scale)), NotebookPalette.Red).name = "Preparation readiness";
        }

        Button PreparationFooterAction(Transform parent, string name, PlayerIcon icon, string caption, Action action,
            float width, float height, bool primary = false)
        {
            var button = NotebookAction(parent, name, new Rect(0, 0, width, height), icon, caption, action, primary);
            var size = button.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width; size.minHeight = size.preferredHeight = height;
            return button;
        }

        void SelectPreparationMaterial(MaterialTier tier)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            string focusName = selected != null ? selected.name : null;
            bool keyboard = selected != null && selected.GetComponent<PlayerCardGraphic>()?.Focused == true;
            game.Drawing.SelectTier(tier);
            Show(PlayerPage.Preparation);
            Canvas.ForceUpdateCanvases();
            var replacement = Array.Find(page.GetComponentsInChildren<Button>(), x => x.name == focusName);
            if (events != null && replacement != null)
            {
                UnityEngine.EventSystems.BaseEventData input = keyboard
                    ? new UnityEngine.EventSystems.BaseEventData(events)
                    : new UnityEngine.EventSystems.PointerEventData(events);
                events.SetSelectedGameObject(replacement.gameObject, input);
            }
        }

        bool PreparationReady(out int validCount, out string reason)
        {
            validCount = 0; reason = "";
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var workshop = game.Run.Workshop;
            for (int i = 0; i < 4; i++)
            {
                var blueprint = i < workshop.Loadout.Count ? workshop.Loadout[i] : null;
                if (blueprint == null || !unique.Add(blueprint.Id)) continue;
                var preview = game.Run.PreviewBlueprint(blueprint, game.Drawing.Tier);
                if (!preview.Valid) { if (reason.Length == 0) reason = GameText.Resolve(preview.Reason); continue; }
                bool owned = workshop.IsUnlocked(blueprint.ChassisId);
                foreach (var mount in blueprint.WeaponMounts) owned &= workshop.IsUnlocked(mount.WeaponId);
                foreach (var module in blueprint.ModuleSelections) owned &= workshop.IsUnlocked(module.DefinitionId);
                if (!owned) { if (reason.Length == 0) reason = "Детали чертежа ещё не открыты"; continue; }
                validCount++;
            }
            bool ready = game.Run.InWorkshop && validCount == 4;
            if (!ready && reason.Length == 0) reason = "Выбери четыре разных чертежа";
            return ready;
        }

        string PreparationBlueprintDescription(TowerBlueprint blueprint)
        {
            if (blueprint == null) return "Выбери страницу из альбома";
            var content = game.Run.Session.Balance.Content;
            if (content.TryGetWeapon(blueprint.FirstWeaponId, out var weapon))
            {
                if (blueprint.WeaponMounts.Count > 1)
                {
                    bool same = true;
                    foreach (var mount in blueprint.WeaponMounts) same &= mount.WeaponId == blueprint.FirstWeaponId;
                    if (!same) return "Сочетает несколько типов оружия";
                }
                switch (weapon.Attack)
                {
                    case AttackKind.Single: return "Быстрая стрельба по одиночным целям";
                    case AttackKind.Line: return "Пробивает врагов на одной линии";
                    case AttackKind.AreaProjectile: return "Урон по площади";
                    case AttackKind.Cone: return "Атакует несколько целей сектором";
                    case AttackKind.Ricochet: return "Рикошетит между целями";
                    case AttackKind.Radial: return "Поражает врагов вокруг башни";
                    case AttackKind.Cleanup: return "Убирает вражеские кляксы";
                }
            }
            return "Чертёж из твоего альбома";
        }
    }
}
