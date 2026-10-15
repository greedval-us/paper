using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class WorkshopUiTests
    {
        GameObject root, ownedEventSystem;
        RectTransform rootRect;
        EventSystem events;
        GameObject previousSelection;
        WorkshopUi ui;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Isolated workshop UI", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1600, 1000);
            events = EventSystem.current;
            if (events == null)
            {
                ownedEventSystem = new GameObject("Isolated workshop events", typeof(EventSystem));
                events = ownedEventSystem.GetComponent<EventSystem>();
            }
            previousSelection = events.currentSelectedGameObject;
            events.SetSelectedGameObject(null);
            ui = new WorkshopUi(new UserTextResolver(new Dictionary<string, string>
            {
                { "test.caption", "Следующий шаг" },
                { "test.hint", "Выбрать инструмент и продолжить рисунок." },
                { "test.long_hint", "Подсказка инструмента с длинным локализованным описанием. Выберите нужную часть чертежа, затем продолжайте рисунок. Текущая работа сохраняется при переходе к другой части." }
            }));
            ui.EnsureTooltip(rootRect);
        }

        [TearDown]
        public void TearDown()
        {
            if (events != null) events.SetSelectedGameObject(previousSelection);
            if (root != null) Object.DestroyImmediate(root);
            if (ownedEventSystem != null) Object.DestroyImmediate(ownedEventSystem);
        }

        RectTransform Tooltip => (RectTransform)root.transform.Find("WorkshopTooltip");
        PointerEventData Pointer => new PointerEventData(events);

        UnityEngine.UI.Button Control(Rect bounds)
            => ui.IconButton(rootRect, "Tool", bounds, WorkshopIcon.Pencil, UserText.Message("test.hint"), () => { });

        void Enter(GameObject control)
            => ExecuteEvents.Execute(control, Pointer, ExecuteEvents.pointerEnterHandler);

        [Test]
        public void PointerAndKeyboardFocusShowResolvedHintsAndExitOrDeselectHideThem()
        {
            var button = Control(new Rect(80, 90, 48, 48));
            Assert.That(Tooltip.gameObject.activeSelf, Is.False);
            Enter(button.gameObject);
            Assert.That(Tooltip.gameObject.activeSelf, Is.True);
            Assert.That(Tooltip.GetComponentInChildren<UnityEngine.UI.Text>().text,
                Is.EqualTo(ui.Resolve(UserText.Message("test.hint"))));
            ExecuteEvents.Execute(button.gameObject, Pointer, ExecuteEvents.pointerExitHandler);
            Assert.That(Tooltip.gameObject.activeSelf, Is.False);

            events.SetSelectedGameObject(button.gameObject);
            Assert.That(Tooltip.gameObject.activeSelf, Is.True, "Keyboard selection exposes the same hint as hover.");
            events.SetSelectedGameObject(null);
            Assert.That(Tooltip.gameObject.activeSelf, Is.False);
        }

        [TestCase(4, 4)]
        [TestCase(1548, 4)]
        [TestCase(4, 948)]
        [TestCase(1548, 948)]
        [TestCase(780, 480)]
        public void HintStaysOnCanvasWithoutCoveringItsControlOrReceivingRaycasts(float x, float y)
        {
            var button = Control(new Rect(x, y, 48, 48));
            ui.Tip(button.gameObject, UserText.Message("test.long_hint"));
            Enter(button.gameObject);
            Assert.That(Tooltip.gameObject.activeSelf, Is.True);
            Rect hintBounds = TopLeftBounds(Tooltip);
            Rect controlBounds = TopLeftBounds(button.GetComponent<RectTransform>());
            Assert.That(hintBounds.xMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(hintBounds.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(hintBounds.xMax, Is.LessThanOrEqualTo(1600.01f));
            Assert.That(hintBounds.yMax, Is.LessThanOrEqualTo(1000.01f));
            Assert.That(hintBounds.Overlaps(controlBounds), Is.False, "The hint must leave its source clickable and visible.");
            Assert.That(hintBounds.width, Is.GreaterThan(100));
            Assert.That(hintBounds.height, Is.GreaterThan(30));
            Assert.That(Tooltip.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            foreach (var graphic in Tooltip.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }

        [Test]
        public void DisablingControlOrTooltipTriggerAndModalNavigationClearTheHint()
        {
            var button = Control(new Rect(120, 200, 48, 48));
            Enter(button.gameObject);
            button.gameObject.SetActive(false);
            Assert.That(Tooltip.gameObject.activeSelf, Is.False);
            button.gameObject.SetActive(true);
            Enter(button.gameObject);
            Assert.That(Tooltip.gameObject.activeSelf, Is.True);
            button.GetComponent<WorkshopTooltip>().enabled = false;
            Assert.That(Tooltip.gameObject.activeSelf, Is.False);
            button.GetComponent<WorkshopTooltip>().enabled = true;

            events.SetSelectedGameObject(button.gameObject);
            Assert.That(Tooltip.gameObject.activeSelf, Is.True);
            ui.HideTooltip();
            Assert.That(Tooltip.gameObject.activeSelf, Is.False, "Opening a modal cancels the current keyboard hint.");
            Assert.That(events.currentSelectedGameObject, Is.SameAs(button.gameObject), "Hiding a hint does not steal focus.");
            Enter(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(events), ExecuteEvents.cancelHandler);
            Assert.That(Tooltip.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void UltrawideControlsKeepTheirHintsInsideTheActualCanvas()
        {
            rootRect.sizeDelta = new Vector2(2370, 1000);
            var button = Control(new Rect(2240, 910, 100, 48));
            Enter(button.gameObject);
            Assert.That(Tooltip.gameObject.activeSelf, Is.True);
            Rect bounds = TopLeftBounds(Tooltip);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(2370));
            Assert.That(bounds.Overlaps(TopLeftBounds(button.GetComponent<RectTransform>())), Is.False);
        }

        [Test]
        public void SourceExitDoesNotDismissAnotherControlsHintAndEnsureReusesTheOverlay()
        {
            var first = Control(new Rect(100, 100, 48, 48));
            var second = Control(new Rect(180, 100, 48, 48));
            Enter(first.gameObject);
            Enter(second.gameObject);
            ExecuteEvents.Execute(first.gameObject, Pointer, ExecuteEvents.pointerExitHandler);
            Assert.That(Tooltip.gameObject.activeSelf, Is.True, "Late exit events from a previous source cannot hide the new hint.");
            var overlay = Tooltip;
            ui.EnsureTooltip(rootRect);
            Assert.That(Tooltip, Is.SameAs(overlay));
            Assert.That(root.GetComponentsInChildren<CanvasGroup>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void CaptionButtonsKeepTheirLabelAndIconWhileCompactButtonsContainOnlyVectorArt()
        {
            var standard = ui.Button(rootRect, "Continue", new Rect(100, 100, 280, 48),
                UserText.Message("test.caption"), () => { }, WorkshopIcon.Next, true);
            var compact = Control(new Rect(400, 100, 48, 48));
            var caption = standard.GetComponentInChildren<UnityEngine.UI.Text>();
            var icon = standard.GetComponentInChildren<WorkshopIconGraphic>();
            Assert.That(caption.text, Is.EqualTo(ui.Resolve(UserText.Message("test.caption"))));
            Assert.That(icon.Icon, Is.EqualTo(WorkshopIcon.Next));
            Assert.That(icon.rectTransform.rect.width, Is.LessThan(standard.GetComponent<RectTransform>().rect.height));
            Assert.That(icon.rectTransform.anchoredPosition.x, Is.LessThan(caption.rectTransform.anchoredPosition.x));
            Assert.That(compact.GetComponentsInChildren<UnityEngine.UI.Text>(true), Is.Empty);

            caption.text = "Обновлённое действие";
            Enter(standard.gameObject);
            Assert.That(Tooltip.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo(caption.text),
                "The default hint follows a changing navigation caption.");
            standard.interactable = false;
            Assert.That(caption.color, Is.Not.EqualTo(WorkshopUi.Paper), "Disabled text is updated along with its background.");
            Assert.That(icon.color, Is.EqualTo(caption.color));
        }

        [Test]
        public void EveryGlyphProducesFiniteBoundedMaskableGeometryWithoutFontGlyphs()
        {
            var viewport = ui.Panel(rootRect, "Viewport", new Rect(60, 60, 160, 120), WorkshopUi.Paper);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(viewport, "Vector symbol", new Rect(20, 20, 32, 32));
            Assert.That(icon, Is.InstanceOf<UnityEngine.UI.MaskableGraphic>());
            Assert.That(icon.maskable, Is.True);
            Assert.That(icon.GetComponentsInChildren<UnityEngine.UI.Text>(true), Is.Empty);
            var populate = typeof(WorkshopIconGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null);
            Assert.That(populate, Is.Not.Null);
            foreach (WorkshopIcon kind in Enum.GetValues(typeof(WorkshopIcon)))
            {
                icon.Icon = kind;
                using (var mesh = new UnityEngine.UI.VertexHelper())
                {
                    Assert.DoesNotThrow(() => populate.Invoke(icon, new object[] { mesh }), kind.ToString());
                    if (kind == WorkshopIcon.None) { Assert.That(mesh.currentVertCount, Is.Zero); continue; }
                    Assert.That(mesh.currentVertCount, Is.GreaterThan(2).And.LessThan(65000), kind.ToString());
                    Assert.That(mesh.currentIndexCount, Is.GreaterThan(2), kind.ToString());
                    Rect bounds = icon.rectTransform.rect;
                    for (int i = 0; i < mesh.currentVertCount; i++)
                    {
                        var vertex = new UnityEngine.UIVertex();
                        mesh.PopulateUIVertex(ref vertex, i);
                        Assert.That(float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.x) ||
                            float.IsNaN(vertex.position.y) || float.IsInfinity(vertex.position.y) ||
                            float.IsNaN(vertex.position.z) || float.IsInfinity(vertex.position.z), Is.False, kind.ToString());
                        Assert.That(vertex.position.x, Is.InRange(bounds.xMin - .01f, bounds.xMax + .01f), kind.ToString());
                        Assert.That(vertex.position.y, Is.InRange(bounds.yMin - .01f, bounds.yMax + .01f), kind.ToString());
                    }
                }
            }
            Assert.That(viewport.GetComponent<UnityEngine.UI.MaskableGraphic>().maskable, Is.True);
        }

        Rect TopLeftBounds(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 bottomLeft = rootRect.InverseTransformPoint(corners[0]);
            Vector3 topRight = rootRect.InverseTransformPoint(corners[2]);
            return new Rect(bottomLeft.x - rootRect.rect.xMin, rootRect.rect.yMax - topRight.y,
                topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        }
    }
}
