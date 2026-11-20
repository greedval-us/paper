using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class CompositeWorkshopPanel
    {
        readonly Dictionary<string, Vector2> sequenceScrollPositions = new Dictionary<string, Vector2>();
        string renderedSequenceScope, renderedSequenceInspection;
        internal void HideHints() => ui.HideTooltip();

        string SequenceScrollScope => DraftSession.Draft.Id + ":" + Route + ":" + Step + ":" + DraftSession.ActiveCategory;
        static string SequenceScrollKey(string scope, string inspection, string name)
            => scope + ":" + name + (name == "Sequence specification scroll" || name == "Structure consequences" ? ":" + inspection : "");

        void CaptureSequenceScroll()
        {
            if (string.IsNullOrEmpty(renderedSequenceScope)) return;
            Canvas.ForceUpdateCanvases();
            foreach (var scroll in Root.GetComponentsInChildren<ScrollRect>())
            {
                var position = scroll.normalizedPosition;
                if (scroll.content.rect.height <= scroll.viewport.rect.height + 1) position.y = 1;
                if (scroll.content.rect.width <= scroll.viewport.rect.width + 1) position.x = 0;
                sequenceScrollPositions[SequenceScrollKey(renderedSequenceScope, renderedSequenceInspection, scroll.name)] = position;
            }
        }

        void RestoreSequenceScroll()
        {
            Canvas.ForceUpdateCanvases();
            renderedSequenceScope = SequenceScrollScope; renderedSequenceInspection = sequenceInspected;
            foreach (var scroll in Root.GetComponentsInChildren<ScrollRect>())
                if (sequenceScrollPositions.TryGetValue(SequenceScrollKey(renderedSequenceScope, renderedSequenceInspection, scroll.name), out var position))
                { scroll.StopMovement(); scroll.normalizedPosition = position; }
        }

        void RestoreSequenceFocus(string previousName)
        {
            if (EventSystem.current == null) return;
            var controls = Root.GetComponentsInChildren<Selectable>();
            string primary = Route == PaperDefense.Core.AlbumEntryKind.Structure
                ? Step == 2 ? "Structure save" : "Structure next"
                : Step == 3 ? "Sequence save" : "Sequence next";
            var target = controls.FirstOrDefault(item => item.name == previousName && item.IsInteractable())
                ?? controls.FirstOrDefault(item => item.name == primary && item.IsInteractable())
                ?? controls.FirstOrDefault(item => item.IsInteractable());
            if (target != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        CatalogScrollList SequenceBodyScroll(Transform parent, string name, Rect bounds)
        {
            var list = new CatalogScrollList(parent, name, bounds, 0, 1, 32, 0);
            list.Scroll.scrollSensitivity = 32;
            list.Scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return list;
        }

        static void SequenceScrollExtent(CatalogScrollList list, float height)
            => list.SetContentHeight(height);

        Text SequenceCopy(Transform parent, string name, string copy, Rect bounds, int size = 18, Color? color = null, bool heading = false)
        {
            var label = ui.Label(parent, name, copy, bounds, size, heading, color);
            label.alignment = TextAnchor.UpperLeft;
            float height = Mathf.Max(size * 1.5f, Mathf.Ceil(label.preferredHeight) + 4);
            WorkshopUi.Place(label.rectTransform, new Rect(bounds.x, bounds.y, bounds.width, height));
            return label;
        }

        void SequenceHelp()
            => UiHelp.Show(Root, "Как собрать башню", "1. Основа: выбери корпус, защиту и число мест для оружия.\n\n2. Оружие: выбери семейство и детали. Покупка открывает деталь навсегда; установка меняет текущий черновик.\n\nСохранённые основы и оружие доступны в библиотеке слева. «Сохранить копию» создаёт отдельный компонент.\n\n3. Сборка: выбери основу, место и совместимое оружие.\n\n4. Проверка: задай имя, проверь башню на полигоне и сохрани её в альбом. Назначь сохранённую башню в боевой набор перед запуском.");

        void StructureHelp()
            => UiHelp.Show(Root, "Как собрать сооружение", "Выбери семейство, чтобы рассмотреть его. Закрытые семейства можно купить за золото, заработанное в боях, когда достигнешь указанного уровня. Одной покупки хватает для всех будущих сборок этого семейства.\n\n" + StructurePurpose(inspectedStructureOffer?.Snapshot.Recipe.Structure.Family ?? DraftSession.PreviewRecipe.Structure.Family) + "\n\nВыбери детали, сохрани сооружение в альбом и назначь его в боевой набор.");
    }
}
