using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Reusable stencil cards. Buying grants access; only Insert changes the drawing.</summary>
    internal sealed class DrawingStencilBrowser
    {
        sealed class Card
        {
            public DrawingUnlockDefinition Definition;
            public RectTransform Root;
            public UnityEngine.UI.Button Buy, Insert;
            public UnityEngine.UI.Text BuyLabel;
        }
        readonly DrawingWorkbenchUi ui;
        readonly DrawingEditorContext context;
        readonly Action<DrawingUnlockDefinition> insert;
        readonly Action<string> feedback;
        readonly RectTransform content;
        readonly UnityEngine.UI.Text gold, note, empty;
        readonly UnityEngine.UI.ScrollRect scroll;
        readonly List<Card> cards = new List<Card>();
        IReadOnlyList<DrawingUnlockDefinition> definitions;
        readonly HashSet<string> ownedIds = new HashSet<string>(StringComparer.Ordinal);
        WorkshopReadModel ownershipSource;
        bool ownershipDirty = true;
        DrawingStencilRole? currentRole;
        readonly GameObject pane;
        public void Show(bool visible)=>pane.SetActive(visible);

        public DrawingStencilBrowser(Transform parent, DrawingWorkbenchUi ui, DrawingEditorContext context, Action<DrawingUnlockDefinition> insert, Action<string> feedback, bool heading=true)
        {
            this.ui = ui; this.context = context; this.insert = insert; this.feedback = feedback;
            pane=DrawingWorkbenchUi.Box(parent,"Drawing stencil pane",new Rect(0,0,1560,940),Color.clear).gameObject;parent=pane.transform;
            if(heading)ui.Label(parent, "Drawing stencil heading", UserText.Message("drawing.workbench.stencils"), new Rect(1096, 553, 440, 34), 22, NotebookPalette.Blue);
            gold = ui.Label(parent, "Drawing stencil gold", "", new Rect(1096, 587, 440, 31), 21);
            note = ui.Label(parent, "Drawing gold source", UserText.Message("drawing.workbench.gold_source"), new Rect(1096, 620, 440, 52), 17);
            var root = DrawingWorkbenchUi.Box(parent, "Drawing stencils", new Rect(1096, 679, 440, 169), new Color(.92f, .94f, .93f), true);
            scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = DrawingWorkbenchUi.Box(root, "Stencil viewport", new Rect(0, 0, 430, 169), Color.white, true);
            var mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>(); mask.showMaskGraphic = false;
            content = new GameObject("Stencil content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>(); fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.inertia = false; scroll.scrollSensitivity = 42;
            var track = DrawingWorkbenchUi.Box(root, "Stencil scrollbar", new Rect(432, 0, 8, 169), new Color(.2f, .4f, .6f, .12f), true);
            var handle = DrawingWorkbenchUi.Box(track, "Handle", new Rect(0, 0, 8, 60), NotebookPalette.Blue, true);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.sizeDelta = Vector2.zero;
            var scrollbar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = scrollbar;
            empty = ui.Label(root, "Drawing decoration stencil hint", UserText.Message("drawing.workbench.no_stencils"), new Rect(15, 30, 400, 120), 20);
            ui.Label(parent, "Drawing stencil replacement note", UserText.Message("drawing.workbench.insert_note"), new Rect(1096, 855, 440, 62), 16);
        }

        public void Bind(IReadOnlyList<DrawingUnlockDefinition> catalog)
        {
            if (ReferenceEquals(definitions, catalog)) return;
            definitions = catalog; currentRole = null; ownershipDirty = true;
            foreach (var card in cards) UnityEngine.Object.DestroyImmediate(card.Root.gameObject);
            cards.Clear();
            foreach (var definition in catalog)
            {
                var box = DrawingWorkbenchUi.Box(content, "Stencil " + definition.Id, new Rect(0, 0, 430, 168), new Color(1, .995f, .96f));
                var layout = box.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight = 168; layout.minHeight = 168;
                var thumb = new GameObject("Stencil preview " + definition.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(DrawingStencilGraphic));
                thumb.transform.SetParent(box, false); DrawingWorkbenchUi.Place(thumb.GetComponent<RectTransform>(), new Rect(8, 8, 76, 94));
                var graphic = thumb.GetComponent<DrawingStencilGraphic>(); graphic.Strokes = definition.Strokes; graphic.raycastTarget = false;
                ui.Label(box, "Stencil name", definition.Name, new Rect(96, 6, 321, 33), 21, NotebookPalette.Blue);
                ui.Label(box, "Stencil description", definition.Description, new Rect(96, 40, 321, 76), 17);
                var card = new Card { Definition = definition, Root = box };
                card.Buy = ui.Button(box, "Buy stencil " + definition.Id, new Rect(8, 123, 249, 36), "", () => Purchase(card));
                card.BuyLabel = card.Buy.GetComponentInChildren<UnityEngine.UI.Text>(); card.BuyLabel.fontSize = 18;
                card.Insert = ui.Button(box, "Insert stencil " + definition.Id, new Rect(269, 123, 151, 36), UserText.Message("drawing.workbench.insert"), () => Insert(card));
                cards.Add(card);
            }
        }
        public void Refresh(string binding)
        {
            DrawingStencilRole? role = binding == DrawingBindings.Base ? DrawingStencilRole.Body : DrawingBindings.TryMount(binding, out _) ? DrawingStencilRole.Barrel : (DrawingStencilRole?)null;
            if (role != currentRole) { currentRole = role; content.anchoredPosition = Vector2.zero; }
            var workshop = context?.Workshop?.Invoke();
            RefreshOwnership(workshop);
            gold.text = ui.Resolve(workshop == null ? UserText.Message("drawing.workbench.shop_offline") : UserText.Message("drawing.workbench.gold", workshop.Gold));
            empty.gameObject.SetActive(!role.HasValue);
            foreach (var card in cards)
            {
                card.Root.gameObject.SetActive(role.HasValue && card.Definition.Role == role.Value);
                bool owned = ownedIds.Contains(card.Definition.Id);
                card.BuyLabel.text = ui.Resolve(owned ? UserText.Message("drawing.workbench.owned") : UserText.Message("drawing.workbench.buy", card.Definition.GoldCost));
                card.Buy.interactable = !owned && workshop != null && context?.Purchase != null && workshop.Gold >= card.Definition.GoldCost;
                card.Insert.interactable = owned;
            }
        }
        void RefreshOwnership(WorkshopReadModel workshop)
        {
            if (!ownershipDirty && ReferenceEquals(ownershipSource, workshop)) return;
            ownershipSource = workshop; ownershipDirty = false; ownedIds.Clear();
            if (definitions == null) return;
            foreach (var definition in definitions)
                if (definition.InitiallyUnlocked || (workshop != null && workshop.IsUnlocked(definition.Id))) ownedIds.Add(definition.Id);
        }
        bool Owned(DrawingUnlockDefinition definition)
        { RefreshOwnership(context?.Workshop?.Invoke()); return ownedIds.Contains(definition.Id); }
        void Purchase(Card card)
        {
            if (Owned(card.Definition)) return;
            if (context?.Purchase == null || context.Workshop?.Invoke() == null) { feedback(UserText.Message("drawing.workbench.shop_offline")); return; }
            if (context.Purchase(card.Definition.Id, out string reason)) feedback(UserText.Message("drawing.workbench.purchased")); else feedback(reason);
            Refresh(currentRole == DrawingStencilRole.Body ? DrawingBindings.Base : currentRole == DrawingStencilRole.Barrel ? DrawingBindings.Mount0 : DrawingBindings.Decoration);
        }
        void Insert(Card card)
        {
            if (!Owned(card.Definition)) { feedback(UserText.Message("drawing.workbench.locked")); return; }
            insert(card.Definition);
        }
    }

    public sealed class DrawingStencilGraphic : UnityEngine.UI.MaskableGraphic
    {
        public IReadOnlyList<DrawingStroke> Strokes { get; set; }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); if (Strokes == null) return;
            var min = new Vector2(1, 1); var max = new Vector2(-1, -1);
            foreach (var stroke in Strokes) foreach (var point in stroke.Points)
            { min = Vector2.Min(min, new Vector2(point.X, point.Y)); max = Vector2.Max(max, new Vector2(point.X, point.Y)); }
            float scale = Mathf.Min((rectTransform.rect.width - 14) / Mathf.Max(.05f, max.x - min.x), (rectTransform.rect.height - 14) / Mathf.Max(.05f, max.y - min.y));
            var center = rectTransform.rect.center - (min + max) * .5f * scale;
            foreach (var stroke in Strokes) DrawingCanvasGraphic.Draw(vh, stroke.Points, center, scale, NotebookPalette.Navy, .012f, DrawingCanvasGraphic.MinimumStrokeWidth(canvas));
        }
    }
}
