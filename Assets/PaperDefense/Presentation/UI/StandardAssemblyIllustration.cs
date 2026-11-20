using System;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>One illustration policy for frozen recipes, stock historical pages and actual player strokes.</summary>
    public static class StandardAssemblyIllustration
    {
        public static bool HasPaper(Graphic drawing)
            => drawing != null && drawing.GetComponent<StandardIllustrationSurface>()?.Paper != null;

        public static bool IsPaper(Graphic graphic)
            => graphic != null && graphic.GetComponent<StandardIllustrationSurface>()?.Paper == graphic;

        public static void EnsureStandardPaper(Graphic drawing, string identity)
        {
            if (drawing == null) return;
            if (HasPaper(drawing)) { SyncPaper(drawing); return; }
            Protect(drawing, true, identity);
        }

        public static void EnsureBlueprintPaper(BlueprintSketch drawing, TowerBlueprint blueprint)
        {
            if (drawing == null || blueprint == null) return;
            EnsureStandardPaper(drawing, drawing.name);
            drawing.GetComponent<StandardIllustrationSurface>().SetPaperVisible(!blueprint.HasPlayerDrawing);
        }

        public static void SyncPaper(Graphic drawing)
        {
            var paper = drawing != null ? drawing.GetComponent<StandardIllustrationSurface>()?.Paper : null;
            if (paper == null || paper == drawing) return;
            var source = drawing.rectTransform; var target = paper.rectTransform;
            if (target.parent != source.parent)
            { target.SetParent(source.parent, false); target.SetSiblingIndex(source.GetSiblingIndex()); }
            if (target.anchorMin != source.anchorMin) target.anchorMin = source.anchorMin;
            if (target.anchorMax != source.anchorMax) target.anchorMax = source.anchorMax;
            if (target.pivot != source.pivot) target.pivot = source.pivot;
            if (target.anchoredPosition3D != source.anchoredPosition3D) target.anchoredPosition3D = source.anchoredPosition3D;
            if (target.sizeDelta != source.sizeDelta) target.sizeDelta = source.sizeDelta;
            if (target.localRotation != source.localRotation) target.localRotation = source.localRotation;
            if (target.localScale != source.localScale) target.localScale = source.localScale;
            var layout = paper.GetComponent<LayoutElement>() ?? paper.gameObject.AddComponent<LayoutElement>();
            if (!layout.ignoreLayout) layout.ignoreLayout = true;
        }

        public static void Draw(Transform parent, AlbumEntry entry, Rect bounds, string identity)
        {
            if (parent == null || entry == null || bounds.width <= 0 || bounds.height <= 0) return;
            if (entry.Kind == AlbumEntryKind.Legacy && entry.LegacyBlueprint.HasPlayerDrawing)
            {
                var authored = PlayerUi.Graphic<BlueprintSketch>(parent, identity, bounds);
                authored.SetBlueprint(entry.LegacyBlueprint);
                Protect(authored, false, identity);
                return;
            }

            var drawing = PlayerUi.Graphic<WeaponAssemblyGraphic>(parent, identity, bounds);
            try
            {
                if (entry.Kind == AlbumEntryKind.Legacy)
                {
                    var blueprint = entry.LegacyBlueprint;
                    var theme = NotebookVisualThemeAsset.Default;
                    bool complete = theme.FindShape(blueprint.ChassisId) != null &&
                        blueprint.WeaponMounts.All(mount => theme.FindShape(mount.WeaponId) != null) &&
                        blueprint.ModuleSelections.All(module => theme.FindShape(module.DefinitionId) != null);
                    if (!complete || !NotebookTowerComposition.TryGetBounds(blueprint, out _))
                        throw new NotSupportedException("The historical page has no drawable standard composition.");
                    drawing.SetBlueprint(entry.LegacyBlueprint);
                }
                else drawing.SetRecipe(entry.Snapshot);
                Protect(drawing, true, identity);
                return;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            WorkshopUi.RemoveView(drawing.gameObject);
            DrawFamily(parent, entry, bounds, identity);
        }

        static void DrawFamily(Transform parent, AlbumEntry entry, Rect bounds, string identity)
        {
            string id = null;
            if (entry.Kind == AlbumEntryKind.Legacy)
                id = "ui." + entry.LegacyBlueprint.FirstWeaponId;
            else if (entry.Kind == AlbumEntryKind.Tower)
            {
                string mechanism = entry.Snapshot.Recipe.Tower.Mounts.First().Weapon.Mechanism.Id;
                id = "ui.weapon." + (mechanism.StartsWith("core.", StringComparison.Ordinal) ? mechanism.Substring(5) : mechanism);
            }
            else id = "ui.structure." + entry.Snapshot.Recipe.Structure.Family.ToString().ToLowerInvariant();

            float captionHeight = Mathf.Min(22, bounds.height * .28f);
            var artBounds = new Rect(bounds.x, bounds.y, bounds.width, bounds.height - captionHeight);
            Graphic fallback;
            if (NotebookVisualThemeAsset.Default.FindShape(id) != null)
            {
                var art = PlayerUi.Graphic<NotebookArtGraphic>(parent, identity + " family drawing", artBounds);
                art.Bind(id); fallback = art;
            }
            else
            {
                var icon = PlayerUi.Graphic<WorkshopIconGraphic>(parent, identity + " unavailable drawing", artBounds);
                icon.Icon = WorkshopIcon.Help; icon.color = FieldMenuSkin.Muted; fallback = icon;
            }
            Protect(fallback, true, identity);
            var caption = new PlayerUi().Label(parent, GameText.Resolve(UserText.Message("assembly.album.schematic")),
                new Rect(bounds.x, bounds.yMax - captionHeight, bounds.width, captionHeight), 12,
                FieldMenuSkin.Muted, alignment: TextAnchor.MiddleCenter);
            caption.name = identity + " schematic caption";
        }

        static void Protect(Graphic drawing, bool night, string identity)
        {
            Graphic paper;
            if (night)
                paper = PlayerUi.Graphic<NightNotebookPaperGraphic>(drawing.transform.parent, identity + " notebook sheet", default);
            else
            {
                paper = PlayerUi.Graphic<Image>(drawing.transform.parent, identity + " authored reference sheet", default);
                paper.color = FieldMenuSkin.PreviewPaper;
            }
            paper.transform.SetSiblingIndex(drawing.transform.GetSiblingIndex());
            drawing.gameObject.AddComponent<StandardIllustrationSurface>().Paper = paper;
            paper.gameObject.AddComponent<StandardIllustrationSurface>().Paper = paper;
            SyncPaper(drawing);
        }
    }

    /// <summary>Explicit ownership keeps generic themes from adding or recoloring another preview sheet.</summary>
    [DisallowMultipleComponent]
    public sealed class StandardIllustrationSurface : MonoBehaviour
    {
        public Graphic Paper { get; set; }
        bool paperVisible = true;

        public void SetPaperVisible(bool value) { paperVisible = value; RefreshVisibility(); }
        public void RefreshVisibility()
        {
            if (Paper == null || Paper.gameObject == gameObject) return;
            var drawing = GetComponent<Graphic>();
            // Retirement detaches a view before disabling it. Never move its siblings during that clear loop.
            if (isActiveAndEnabled && drawing != null && transform.parent != null)
                StandardAssemblyIllustration.SyncPaper(drawing);
            bool visible = paperVisible && isActiveAndEnabled && drawing != null && drawing.enabled;
            if (Paper.gameObject.activeSelf != visible) Paper.gameObject.SetActive(visible);
        }
        void OnEnable() => RefreshVisibility();
        void OnDisable() => RefreshVisibility();
        void LateUpdate() => RefreshVisibility();
    }
}
