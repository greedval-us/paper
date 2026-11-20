using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Field materials inside the existing battle controls, with gameplay colors kept distinct.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(500)]
    public sealed class BattleHudTheme : MonoBehaviour
    {
        static readonly Color Red = new Color32(237, 145, 122, 255);
        static readonly Color Blue = new Color32(144, 192, 239, 255);
        static readonly Color Green = new Color32(159, 204, 162, 255);

        enum Treatment { Foreground, SemanticImage, DarkImage, ScrollTrack, ScrollHandle, Panel, Border }

        sealed class Entry
        {
            public Graphic Graphic;
            public Selectable Selectable;
            public Treatment Treatment;
            public Color Source, LastApplied;
            public bool HasApplied;
        }

        readonly List<Graphic> graphics = new List<Graphic>();
        readonly List<Button> buttons = new List<Button>();
        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<Graphic, Entry> known = new Dictionary<Graphic, Entry>();

        public static bool Owns(Transform target)
            => target != null && target.GetComponentInParent<BattleHudTheme>(true) != null;

        public static void ApplyTo(RectTransform root)
        {
            if (root == null) return;
            var theme = root.GetComponent<BattleHudTheme>() ?? root.gameObject.AddComponent<BattleHudTheme>();
            theme.Refresh();
        }

        public void Refresh()
        {
            GetComponentsInChildren(true, graphics);
            entries.Clear();
            foreach (var graphic in graphics)
            {
                if (graphic == null || IsSchematic(graphic)) continue;
                if (graphic is PlayerCardGraphic card)
                {
                    bool primary = card.name == "Launch wave";
                    if (!card.FieldStyle || card.FieldPrimary != primary)
                    {
                        card.FieldStyle = true;
                        card.FieldPrimary = primary;
                        card.SetVerticesDirty();
                    }
                    continue;
                }

                Treatment treatment;
                if (graphic is Text || graphic is PlayerIconGraphic)
                    treatment = Treatment.Foreground;
                else if (graphic is Image image && image.sprite == null)
                {
                    if (!TryImageTreatment(image, out treatment)) continue;
                }
                else continue;

                if (!known.TryGetValue(graphic, out var entry))
                {
                    entry = new Entry { Graphic = graphic, Source = graphic.color };
                    known.Add(graphic, entry);
                }
                entry.Selectable = graphic.GetComponentInParent<Selectable>(true);
                entry.Treatment = treatment;
                entries.Add(entry);
            }

            GetComponentsInChildren(true, buttons);
            foreach (var button in buttons) UiInteractionFeedback.Ensure(button);
            SyncColors();
        }

        void LateUpdate() => SyncColors();

        public void SyncColors()
        {
            foreach (var entry in entries)
            {
                var graphic = entry.Graphic;
                if (graphic == null) continue;
                // Gameplay owns the raw color. Keep it through disabled/hover transitions,
                // but notice subsequent affordability, well-threat and health updates.
                if (!entry.HasApplied || graphic.color != entry.LastApplied) entry.Source = graphic.color;
                Color source = entry.Source;
                Color value;
                switch (entry.Treatment)
                {
                    case Treatment.Foreground:
                        var control = entry.Selectable;
                        var card = control != null ? control.targetGraphic as PlayerCardGraphic : null;
                        value = Foreground(source, card != null && card.FieldPrimary,
                            control != null && !control.IsInteractable());
                        break;
                    case Treatment.SemanticImage: value = Foreground(source); break;
                    case Treatment.DarkImage:
                    case Treatment.ScrollTrack: value = WithAlpha(FieldMenuSkin.Background, source.a); break;
                    case Treatment.ScrollHandle: value = WithAlpha(FieldMenuSkin.Accent, source.a); break;
                    case Treatment.Panel: value = WithAlpha(FieldMenuSkin.Panel, source.a); break;
                    default: value = WithAlpha(FieldMenuSkin.Border, source.a); break;
                }
                if (graphic.color != value) graphic.color = value;
                entry.LastApplied = value;
                entry.HasApplied = true;
            }
        }

        static bool IsSchematic(Graphic graphic) => graphic is WeaponAssemblyGraphic || graphic is BlueprintSketch ||
            graphic is NotebookArtGraphic || graphic is WorkshopAssemblyDiagram || graphic is TrialLaneSketch ||
            graphic is NightNotebookPaperGraphic;

        static bool TryImageTreatment(Image image, out Treatment treatment)
        {
            string name = image.name;
            if (StandardAssemblyIllustration.IsPaper(image))
            { treatment = default; return false; }
            if (name == "Album battle HP fill" || name == "Tower modernization progress") treatment = Treatment.SemanticImage;
            else if (name == "Album battle HP track") treatment = Treatment.DarkImage;
            else if (name == "Scroll track" || name == "Horizontal scroll track") treatment = Treatment.ScrollTrack;
            else if (name == "Scroll handle" || name == "Horizontal scroll handle") treatment = Treatment.ScrollHandle;
            else
            {
                var rect = image.rectTransform.rect;
                if (rect.width > 160 && rect.height > 72) treatment = Treatment.Panel;
                else if (rect.width <= 4 || rect.height <= 4) treatment = Treatment.Border;
                else { treatment = default; return false; }
            }
            return true;
        }

        static Color Foreground(Color source, bool primary = false, bool disabled = false)
        {
            if (source.a <= 0) return source;
            Color value;
            if (disabled) value = FieldMenuSkin.Disabled;
            else if (primary) value = FieldMenuSkin.AccentInk;
            else if (Same(source, PlayerUi.Ink) || Same(source, NotebookPalette.Navy) || Same(source, FieldMenuSkin.Ink))
                value = FieldMenuSkin.Ink;
            else if (Same(source, PlayerUi.Muted) || Same(source, NotebookPalette.Pencil) || Same(source, FieldMenuSkin.Muted))
                value = FieldMenuSkin.Muted;
            else if (Same(source, Red)) value = Red;
            else if (Same(source, Blue) || Same(source, PlayerUi.Blue) || Same(source, NotebookPalette.Marker)) value = Blue;
            else if (Same(source, Green)) value = Green;
            else if (Same(source, FieldMenuSkin.Accent)) value = FieldMenuSkin.Accent;
            else if (Same(source, FieldMenuSkin.AccentInk)) value = FieldMenuSkin.AccentInk;
            else if (source.r > .48f && source.r > source.g * 1.3f && source.r > source.b * 1.35f)
                value = source.g > source.b * 1.5f ? FieldMenuSkin.Accent : Red;
            else if (source.g > source.r * 1.12f && source.g > source.b * 1.06f) value = Green;
            else if (source.b > source.r * 1.15f && source.b > source.g * 1.12f) value = Blue;
            else value = FieldMenuSkin.Ink;
            return WithAlpha(value, source.a);
        }

        static Color WithAlpha(Color value, float alpha) { value.a = alpha; return value; }
        static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < .015f &&
            Mathf.Abs(a.g - b.g) < .015f && Mathf.Abs(a.b - b.b) < .015f;
    }
}
