using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Menu-only art. Existing layout, input objects and semantic colors stay authoritative.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(500)]
    public sealed class FieldMenuTheme : MonoBehaviour
    {
        const string BackdropName = "Field headquarters backdrop";
        const string PreviewName = "Field schematic backing";
        static Texture2D map;
        sealed class ColorState
        {
            public Color Source, Applied;
            public bool HasApplied, Disabled, Primary;
        }
        readonly Dictionary<Graphic, ColorState> applied = new Dictionary<Graphic, ColorState>();
        Graphic[] graphics = new Graphic[0];
        RawImage backdrop;
        bool showBackdrop, refreshing;
        int children;
        Vector2 lastSize;

        public static bool IsThemed(Transform target)
            => target != null && target.GetComponentInParent<FieldMenuTheme>(true) != null;

        public static void ApplyTo(RectTransform root, bool backdrop = true)
        {
            if (root == null) return;
            var scope = root.GetComponent<FieldMenuTheme>() ?? root.gameObject.AddComponent<FieldMenuTheme>();
            scope.showBackdrop = backdrop; scope.Refresh();
        }

        public void Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                if (showBackdrop) EnsureBackdrop();
                graphics = GetComponentsInChildren<Graphic>(true);
                // Keep semantic colors through refreshes, but retire replaced workshop views.
                var current = new HashSet<Graphic>(graphics);
                var retired = new List<Graphic>();
                foreach (var previous in applied.Keys)
                    if (previous == null || !current.Contains(previous)) retired.Add(previous);
                foreach (var previous in retired) applied.Remove(previous);
                foreach (var graphic in graphics)
                    if (graphic is NotebookArtGraphic && (graphic.name.StartsWith("terrain.") || graphic.name.StartsWith("Result fallen") || graphic.name.StartsWith("Result icon.") || graphic.name.StartsWith("Composite trial perimeter")))
                        graphic.enabled = false;
                EnsureResultPreview();
                foreach (var graphic in graphics)
                {
                    if (graphic == null || graphic.name == BackdropName || graphic.name.StartsWith(PreviewName)) continue;
                    if (graphic is PlayerCardGraphic card)
                    {
                        card.FieldStyle = true;
                        card.FieldPrimary |= card.Control && IsPrimary(card.color);
                        card.SetVerticesDirty();
                    }
                    else if (graphic is WorkshopSurfaceGraphic surface)
                    {
                        surface.FieldStyle = true;
                        surface.FieldPrimary |= surface.IsControl && IsPrimary(surface.color);
                        surface.SetVerticesDirty();
                    }
                    else if (graphic is PlayerPaperGraphic || graphic is NotebookResultEmblemGraphic ||
                        graphic is FoundationDetailGraphic && graphic.name == "Album selected card frame")
                        graphic.enabled = false;
                    else if (IsSchematic(graphic))
                    {
                        EnsurePreview(graphic);
                        if (graphic is TrialLaneSketch lane) lane.DarkSurface = true;
                    }
                }
                foreach (var button in GetComponentsInChildren<PlayerNotebookButton>(true)) button.RefreshFieldStyle();
                foreach (var button in GetComponentsInChildren<WorkshopButton>(true)) button.RefreshFieldStyle();
                foreach (var button in GetComponentsInChildren<Button>(true)) UiInteractionFeedback.Ensure(button);
                foreach (var field in GetComponentsInChildren<InputField>(true))
                { field.customCaretColor = true; field.caretColor = FieldMenuSkin.Accent; field.selectionColor = new Color(.86f, .65f, .28f, .32f); }
                foreach (var graphic in graphics) Recolor(graphic);
                children = transform.childCount;
                FitBackdrop();
                if (TryGetComponent<UiMenuMotion>(out var motion)) motion.RefreshBackdrop();
            }
            finally { refreshing = false; }
        }

        void LateUpdate()
        {
            if (children != transform.childCount) { Refresh(); return; }
            foreach (var graphic in graphics) Recolor(graphic);
            FitBackdrop();
        }

        void Recolor(Graphic graphic)
        {
            if (graphic == null || graphic.name == BackdropName || graphic.name.StartsWith(PreviewName) ||
                IsSchematic(graphic) || StandardAssemblyIllustration.IsPaper(graphic)) return;
            bool primary = false;
            var selectable = graphic.GetComponentInParent<Selectable>(true);
            if (selectable != null)
            {
                var player = selectable.targetGraphic as PlayerCardGraphic;
                var workshop = selectable.targetGraphic as WorkshopSurfaceGraphic;
                primary = player != null && player.FieldPrimary || workshop != null && workshop.FieldPrimary;
            }
            bool disabled = selectable != null && !selectable.IsInteractable();
            if (!applied.TryGetValue(graphic, out var state))
            {
                state = new ColorState { Source = graphic.color };
                applied.Add(graphic, state);
            }
            // A caller can change the semantic ink even while this control is disabled.
            else if (graphic.color != state.Applied) state.Source = graphic.color;
            else if (!refreshing && state.HasApplied && state.Disabled == disabled && state.Primary == primary) return;
            Color value;
            if (graphic is Text || graphic is PlayerIconGraphic || graphic is WorkshopIconGraphic)
                value = FieldMenuSkin.TextColor(state.Source, primary, disabled);
            else if (graphic is Image image && image.sprite == null)
            {
                var rect = image.rectTransform.rect;
                value = image.name == "Scroll track" || image.name == "Horizontal scroll track" ? FieldMenuSkin.Background :
                    image.name == "Scroll handle" || image.name == "Horizontal scroll handle" ? FieldMenuSkin.Border :
                    rect.width > 160 && rect.height > 72 ? FieldMenuSkin.Panel :
                    rect.width <= 4 || rect.height <= 4 ? FieldMenuSkin.Border : FieldMenuSkin.TextColor(state.Source);
                value.a = state.Source.a;
            }
            else return;
            graphic.color = value;
            state.Applied = value; state.Disabled = disabled; state.Primary = primary; state.HasApplied = true;
        }

        void EnsureBackdrop()
        {
            if (map == null) map = Resources.Load<Texture2D>("FieldHeadquarters/MapBackdrop");
            Transform parent = transform;
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Home illustration" || image.name == "Neutral page background" ||
                    image.name == "Result landscape border" || image.name == "Composite page landscape")
                { parent = image.transform; break; }
            }
            if (parent == transform)
            {
                var paper = transform.Find("Paper");
                if (paper != null && paper.gameObject.activeSelf) parent = paper;
            }
            var old = parent.Find(BackdropName);
            if (old != null && old.gameObject.activeSelf) backdrop = old.GetComponent<RawImage>();
            if (backdrop == null || !backdrop.gameObject.activeSelf)
            {
                var go = new GameObject(BackdropName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(LayoutElement));
                go.transform.SetParent(parent, false); go.transform.SetAsFirstSibling();
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                backdrop = go.GetComponent<RawImage>(); backdrop.raycastTarget = false;
                lastSize = Vector2.zero;
                var rect = backdrop.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            backdrop.texture = map;
            // Keep the map's identity while leaving the bright contour lines behind the controls.
            bool cover = parent.name == "Home illustration";
            backdrop.color = map == null ? FieldMenuSkin.Background :
                cover ? new Color32(108, 130, 119, 255) : new Color32(73, 93, 89, 255);
        }

        void FitBackdrop()
        {
            if (backdrop == null || map == null) return;
            var size = backdrop.rectTransform.rect.size;
            if (size == lastSize || size.x <= 0 || size.y <= 0) return;
            lastSize = size;
            float view = size.x / size.y, source = map.width / (float)map.height;
            backdrop.uvRect = view < source ? new Rect((1 - view / source) * .5f, 0, view / source, 1)
                : new Rect(0, (1 - source / view) * .5f, 1, source / view);
        }

        static bool IsPrimary(Color color) => Mathf.Abs(color.r - PlayerUi.Blue.r) < .025f &&
            Mathf.Abs(color.g - PlayerUi.Blue.g) < .025f && Mathf.Abs(color.b - PlayerUi.Blue.b) < .025f;

        static bool IsSchematic(Graphic graphic) => graphic is WeaponAssemblyGraphic || graphic is BlueprintSketch ||
            graphic is WorkshopAssemblyDiagram || graphic is NeutralMissionMapGraphic || graphic is NotebookArtGraphic || graphic is TrialLaneSketch;

        static bool UsesNightNotebookPaper(Graphic graphic) =>
            graphic is WeaponAssemblyGraphic assembly && (assembly.Blueprint == null || !assembly.Blueprint.HasPlayerDrawing) ||
            graphic is WorkshopAssemblyDiagram || graphic is TrialLaneSketch || graphic is NeutralMissionMapGraphic ||
            graphic is NotebookArtGraphic art && art.ShapeId != null;

        static void EnsurePreview(Graphic graphic)
        {
            if (StandardAssemblyIllustration.HasPaper(graphic))
            { StandardAssemblyIllustration.SyncPaper(graphic); return; }
            var source = graphic.rectTransform;
            if (!graphic.enabled || source.parent == null || source.rect.width < 24 || source.rect.height < 24) return;
            bool nightPaper = UsesNightNotebookPaper(graphic);
            Transform before = source;
            if (source.parent.name == "Result composition")
            {
                // Standard result drawings share the squared sheet. Authored dark reference
                // strokes retain their protected light islands below the actual artwork.
                var result = source.parent.Find(PreviewName + " result");
                if (result != null)
                {
                    if (nightPaper || result.GetComponent<NightNotebookPaperGraphic>() == null) return;
                    before = ResultFirstDrawing(source.parent);
                    if (before == null) return;
                }
            }
            // Rebuilt pages must keep stable hierarchy identities across full player cycles.
            // Names may repeat under one parent; count only actual sibling drawings, not backings.
            int occurrence = 0;
            for (int i = 0; i < source.GetSiblingIndex(); i++)
            {
                var sibling = source.parent.GetChild(i);
                if (sibling.name == source.name && sibling.GetComponent<Graphic>() != null) occurrence++;
            }
            string name = PreviewName + " " + source.name + (occurrence == 0 ? "" : " " + occurrence);
            var rect = EnsureBacking(source.parent, name, nightPaper);
            PlaceBefore(rect, before);
            rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax; rect.pivot = source.pivot;
            rect.anchoredPosition3D = source.anchoredPosition3D; rect.sizeDelta = source.sizeDelta;
            rect.localRotation = source.localRotation; rect.localScale = source.localScale;
        }

        static RectTransform EnsureBacking(Transform parent, string name, bool nightPaper)
        {
            var old = parent.Find(name);
            if (old != null && (nightPaper ? old.GetComponent<NightNotebookPaperGraphic>() == null : old.GetComponent<Image>() == null))
            {
                // Graphic disallows a second Graphic component on the same object. Retire only
                // our own decoration when its source changes between modern and legacy art.
                old.name = name + " retired";
                old.gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
                old = null;
            }
            if (old == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                    nightPaper ? typeof(NightNotebookPaperGraphic) : typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                old = go.transform;
            }
            old.gameObject.SetActive(true);
            var layout = old.GetComponent<LayoutElement>() ?? old.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var graphic = old.GetComponent<Graphic>();
            graphic.enabled = true; graphic.raycastTarget = false;
            graphic.color = nightPaper ? Color.white : FieldMenuSkin.PreviewPaper;
            return (RectTransform)old;
        }

        static void PlaceBefore(Transform backing, Transform drawing)
        {
            int index = drawing.GetSiblingIndex();
            if (backing.GetSiblingIndex() < index) index--;
            backing.SetSiblingIndex(index);
        }

        static bool IsResultDrawing(Graphic graphic) => graphic != null && graphic.enabled &&
            (graphic is WeaponAssemblyGraphic || graphic is BlueprintSketch ||
                graphic is NotebookArtGraphic art && (art.ShapeId == "object.castle" ||
                    art.ShapeId == "object.castle.damaged" || UsesNightNotebookPaper(art)));

        static Transform ResultFirstDrawing(Transform composition)
        {
            for (int i = 0; i < composition.childCount; i++)
            {
                var child = composition.GetChild(i);
                if (IsResultDrawing(child.GetComponent<Graphic>())) return child;
            }
            return null;
        }

        void EnsureResultPreview()
        {
            var composition = transform.Find("Result composition") as RectTransform;
            if (composition == null) return;
            string name = PreviewName + " result";
            Rect bounds = default; bool found = false, nightPaper = false;
            Transform first = null;
            var corners = new Vector3[4];
            foreach (var graphic in graphics)
            {
                if (!IsResultDrawing(graphic) || graphic.transform.parent != composition ||
                    StandardAssemblyIllustration.HasPaper(graphic)) continue;
                graphic.rectTransform.GetWorldCorners(corners);
                Vector2 low = composition.InverseTransformPoint(corners[0]), high = low;
                for (int i = 1; i < corners.Length; i++)
                {
                    Vector2 point = composition.InverseTransformPoint(corners[i]);
                    low = Vector2.Min(low, point); high = Vector2.Max(high, point);
                }
                var box = new Rect(low.x - composition.rect.xMin, composition.rect.yMax - high.y, high.x - low.x, high.y - low.y);
                bounds = found ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, box.xMin), Mathf.Min(bounds.yMin, box.yMin), Mathf.Max(bounds.xMax, box.xMax), Mathf.Max(bounds.yMax, box.yMax)) : box;
                found = true; nightPaper |= UsesNightNotebookPaper(graphic);
                if (first == null || graphic.transform.GetSiblingIndex() < first.GetSiblingIndex()) first = graphic.transform;
            }
            if (!found) return;
            var rect = EnsureBacking(composition, name, nightPaper);
            PlaceBefore(rect, first);
            PlayerUi.Place(rect, bounds);
        }
    }
}
