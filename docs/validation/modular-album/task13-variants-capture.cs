if (!UnityEngine.Application.isPlaying || !string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
    throw new System.InvalidOperationException("Run the variant audit only in the isolated, unsaved empty PlayMode scene.");
var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<PaperDefense.Application.AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
var balance = UnityEditor.AssetDatabase.LoadAssetAtPath<PaperDefense.Application.GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset").CreateBalance();
var definitions = catalog.Definitions.OrderBy(d => d.Category).ThenBy(d => d.Id, System.StringComparer.Ordinal).ToArray();
if (definitions.Length != 54) throw new System.InvalidOperationException("The variant audit expects all 54 authoritative definitions, not a subset.");
var neutralIds = new System.Collections.Generic.HashSet<string>(new[] { "armor.none", "utility.none" }, System.StringComparer.Ordinal);
var guard = catalog.Starters.Single(e => e.Id == "album.guard").Snapshot.Recipe.Tower;
System.Func<string, PaperDefense.Core.PartRef> part = id => {
    var definition = definitions.Single(d => d.Id == id);
    return new PaperDefense.Core.PartRef(definition.Id, definition.Revision);
};
var records = new System.Collections.Generic.List<(PaperDefense.Core.ComponentDefinition definition, PaperDefense.Core.RecipeSnapshot snapshot, PaperDefense.Presentation.AssemblyVisualComposition composition, bool neutral)>();
foreach (var definition in definitions)
{
    var selected = new PaperDefense.Core.PartRef(definition.Id, definition.Revision);
    bool neutral = neutralIds.Contains(definition.Id);
    var rejections = new System.Collections.Generic.List<string>();
    System.Func<PaperDefense.Core.AssemblyRecipe, PaperDefense.Core.RecipeSnapshot> resolve = recipe => {
        var result = PaperDefense.Core.AssemblyResolver.Resolve(recipe, catalog, balance, PaperDefense.Core.MaterialTier.Pencil, PaperDefense.Presentation.AssemblyVisualComposer.Fits);
        if (!result.Valid) { rejections.Add(string.Join("|", result.Diagnostics.Select(d => d.Code + ":" + d.Path))); return null; }
        if (!result.Value.Snapshot.Recipe.Parts.Any(p => p.Id == selected.Id && p.Revision == selected.Revision))
            throw new System.InvalidOperationException("The selected definition was substituted in the audit: " + definition.Id);
        return result.Value.Snapshot;
    };
    PaperDefense.Core.RecipeSnapshot snapshot = null;
    if (definition.Category <= PaperDefense.Core.ComponentCategory.Turn)
    {
        var f = guard.Foundation;
        foreach (var frameDefinition in definitions.Where(d => d.Category == PaperDefense.Core.ComponentCategory.Frame && (definition.Category != PaperDefense.Core.ComponentCategory.Frame || d.Id == definition.Id)).OrderBy(d => d.Id == f.Frame.Id ? 0 : 1).ThenBy(d => d.Id, System.StringComparer.Ordinal))
        {
            if (snapshot != null) break;
            var framePart = new PaperDefense.Core.PartRef(frameDefinition.Id, frameDefinition.Revision);
            if (!PaperDefense.Presentation.AssemblyVisualComposer.Fits.TryGetFrame(framePart.Id, framePart.Revision, out var frameFit)) continue;
            var foundation = new PaperDefense.Core.FoundationRecipe(framePart,
                definition.Category == PaperDefense.Core.ComponentCategory.Hull ? selected : f.Hull,
                definition.Category == PaperDefense.Core.ComponentCategory.Armor ? selected : f.Armor,
                definition.Category == PaperDefense.Core.ComponentCategory.Feed ? selected : f.Feed,
                definition.Category == PaperDefense.Core.ComponentCategory.Turn ? selected : f.Turn);
            var slots = frameFit.Mounts.Select(m => m.SlotIndex).OrderBy(slot => slot).ToArray();
            foreach (var core in definitions.Where(d => d.Category == PaperDefense.Core.ComponentCategory.Mechanism).OrderBy(d => d.Id == "core.gun" ? 0 : 1).ThenBy(d => d.Id, System.StringComparer.Ordinal))
            {
                if (snapshot != null) break;
                var weapon = new PaperDefense.Core.WeaponRecipe(new PaperDefense.Core.PartRef(core.Id, core.Revision), part("head." + core.Id.Substring(5)), part("sight.iron"), part("magazine.standard"), part("trigger.standard"));
                // TowerRecipe requires mount 0. Try every real socket subset containing it;
                // never relabel another socket as 0 to evade the authored clearance rules.
                for (int mask = 1; mask < (1 << slots.Length); mask++)
                {
                    var mounts = slots.Where((slot, index) => (mask & (1 << index)) != 0).Select(slot => new PaperDefense.Core.WeaponMountRecipe(slot, weapon)).ToArray();
                    if (!mounts.Any(m => m.SlotIndex == 0)) continue;
                    snapshot = resolve(new PaperDefense.Core.AssemblyRecipe(new PaperDefense.Core.TowerRecipe(foundation, mounts)));
                    if (snapshot != null) break;
                }
            }
        }
    }
    else if (definition.Category <= PaperDefense.Core.ComponentCategory.Trigger)
    {
        foreach (var core in definitions.Where(d => d.Category == PaperDefense.Core.ComponentCategory.Mechanism).OrderBy(d => d.Id == "core.gun" ? 0 : 1).ThenBy(d => d.Id, System.StringComparer.Ordinal))
        {
            if (snapshot != null) break;
            if (definition.Category == PaperDefense.Core.ComponentCategory.Mechanism && core.Id != definition.Id) continue;
            foreach (var magazine in definitions.Where(d => d.Category == PaperDefense.Core.ComponentCategory.Magazine).OrderBy(d => d.Id == "magazine.standard" ? 0 : 1).ThenBy(d => d.Id, System.StringComparer.Ordinal))
            {
                var weapon = new PaperDefense.Core.WeaponRecipe(
                    new PaperDefense.Core.PartRef(core.Id, core.Revision),
                    definition.Category == PaperDefense.Core.ComponentCategory.Head ? selected : part("head." + core.Id.Substring(5)),
                    definition.Category == PaperDefense.Core.ComponentCategory.Sight ? selected : part("sight.iron"),
                    definition.Category == PaperDefense.Core.ComponentCategory.Magazine ? selected : new PaperDefense.Core.PartRef(magazine.Id, magazine.Revision),
                    definition.Category == PaperDefense.Core.ComponentCategory.Trigger ? selected : part("trigger.standard"));
                var candidate = new PaperDefense.Core.AssemblyRecipe(new PaperDefense.Core.TowerRecipe(guard.Foundation, new[] { new PaperDefense.Core.WeaponMountRecipe(0, weapon) }));
                snapshot = resolve(candidate);
                if (snapshot != null) break;
            }
        }
    }
    else
    {
        foreach (var starter in catalog.Starters.Where(e => e.Kind == PaperDefense.Core.AlbumEntryKind.Structure))
        {
            var s = starter.Snapshot.Recipe.Structure;
            if ((definition.StructureFamilies & s.Family) == 0) continue;
            var structure = new PaperDefense.Core.StructureRecipe(s.Family,
                definition.Category == PaperDefense.Core.ComponentCategory.Site ? selected : s.Site,
                definition.Category == PaperDefense.Core.ComponentCategory.Function ? selected : s.Function,
                definition.Category == PaperDefense.Core.ComponentCategory.Shell ? selected : s.Shell,
                definition.Category == PaperDefense.Core.ComponentCategory.Utility ? selected : s.Utility);
            snapshot = resolve(new PaperDefense.Core.AssemblyRecipe(structure));
            if (snapshot != null) break;
        }
    }
    if (snapshot == null) throw new System.InvalidOperationException("No valid numeric + physical fixture for " + definition.Id + ": " + string.Join("; ", rejections.Distinct()));
    var composition = PaperDefense.Presentation.AssemblyVisualComposer.Compose(snapshot, new PaperDefense.Presentation.AssemblyVisualPose());
    int selectedFragments = composition.Fragments.Count(f => f.PartId == definition.Id);
    if (!neutral && selectedFragments == 0) throw new System.InvalidOperationException("A selected non-neutral definition has no fragment in its actual full composition: " + definition.Id);
    if (neutral && selectedFragments != 0) throw new System.InvalidOperationException("A neutral definition unexpectedly has an authored fragment: " + definition.Id);
    records.Add((definition, snapshot, composition, neutral));
}
if (records.Count(r => !r.neutral) != 52 || records.Count(r => r.neutral) != 2)
    throw new System.InvalidOperationException("The audit must cover exactly 52 raster definitions and two explicitly neutral options.");

var paths = new System.Collections.Generic.List<string>();
var proof = new System.Collections.Generic.List<string> { "Page,Definition,Category,Neutral,FixtureParts,SelectedFragments,FullFragments,FullVertices,MinimumPixelGap,DetailFragments,DetailVertices,DetailMinimumPixelGap" };
var invariant = System.Globalization.CultureInfo.InvariantCulture;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
System.Func<PaperDefense.Presentation.WeaponAssemblyGraphic, int, (int vertices, float gap)> measure = (graphic, fragmentCount) => {
    using (var mesh = new UnityEngine.UI.VertexHelper())
    {
        typeof(PaperDefense.Presentation.WeaponAssemblyGraphic).GetMethod("OnPopulateMesh", flags, null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null).Invoke(graphic, new object[] { mesh });
        if (mesh.currentVertCount < fragmentCount * 4) throw new System.InvalidOperationException("Missing raster geometry in " + graphic.name);
        var vertex = new UnityEngine.UIVertex(); mesh.PopulateUIVertex(ref vertex, 0); UnityEngine.Vector2 low = vertex.position, high = vertex.position;
        for (int index = 1; index < mesh.currentVertCount; index++) { mesh.PopulateUIVertex(ref vertex, index); low = UnityEngine.Vector2.Min(low, vertex.position); high = UnityEngine.Vector2.Max(high, vertex.position); }
        var bounds = graphic.rectTransform.rect;
        float minimum = UnityEngine.Mathf.Min(low.x - bounds.xMin, bounds.xMax - high.x, low.y - bounds.yMin, bounds.yMax - high.y);
        if (minimum < 1) throw new System.InvalidOperationException("Variant art clips its actual viewport: " + graphic.name + " gap=" + minimum);
        return (mesh.currentVertCount, minimum);
    }
};
const int width = 1920, height = 1080;
for (int page = 0; page < 6; page++)
{
    var owner = new UnityEngine.GameObject("Task13 isolated variants page " + (page + 1));
    var target = new UnityEngine.RenderTexture(width, height, 24);
    try
    {
        target.Create();
        var cameraObject = new UnityEngine.GameObject("Variant audit viewport", typeof(UnityEngine.Camera)); cameraObject.transform.SetParent(owner.transform, false);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.targetTexture = target;
        var canvasObject = new UnityEngine.GameObject("Actual variant composition sheet", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas)); canvasObject.transform.SetParent(owner.transform, false);
        var canvas = canvasObject.GetComponent<UnityEngine.Canvas>(); canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.scaleFactor = 1;
        var paper = PaperDefense.Presentation.WorkshopUi.Graphic<UnityEngine.UI.Image>(canvas.transform, "Variant audit paper", new UnityEngine.Rect(0, 0, width, height)); paper.color = new UnityEngine.Color32(248, 244, 226, 255);
        System.Action<UnityEngine.Transform, string, UnityEngine.Rect, int> label = (parent, text, bounds, fontSize) => {
            var node = PaperDefense.Presentation.WorkshopUi.Graphic<UnityEngine.UI.Text>(parent, text, bounds);
            node.font = font; node.text = text; node.fontSize = fontSize; node.color = new UnityEngine.Color32(26, 48, 110, 255);
            node.alignment = UnityEngine.TextAnchor.MiddleLeft; node.supportRichText = false;
            node.horizontalOverflow = UnityEngine.HorizontalWrapMode.Wrap; node.verticalOverflow = UnityEngine.VerticalWrapMode.Truncate;
        };
        label(canvas.transform, "54 ОПРЕДЕЛЕНИЯ В ПОЛНЫХ СБОРКАХ · " + (page + 1) + " / 6 · реальный resolver + fit + composer", new UnityEngine.Rect(18, 5, width - 36, 36), 27);
        const float gap = 18, top = 50;
        float cardWidth = (width - gap * 4) / 3, cardHeight = (height - top - gap * 4) / 3;
        for (int cell = 0; cell < 9; cell++)
        {
            var record = records[page * 9 + cell]; var recipe = record.snapshot.Recipe;
            float x = gap + cell % 3 * (cardWidth + gap), y = top + gap + cell / 3 * (cardHeight + gap);
            var card = PaperDefense.Presentation.WorkshopUi.Graphic<UnityEngine.UI.Image>(canvas.transform, "Variant " + record.definition.Id, new UnityEngine.Rect(x, y, cardWidth, cardHeight)); card.color = new UnityEngine.Color32(255, 253, 242, 255);
            label(card.transform, record.definition.Name, new UnityEngine.Rect(10, 0, cardWidth - 20, 30), 23);
            label(card.transform, record.definition.Id + " · " + record.definition.Category, new UnityEngine.Rect(10, 30, cardWidth - 20, 23), 16);
            var full = PaperDefense.Presentation.WorkshopUi.Graphic<PaperDefense.Presentation.WeaponAssemblyGraphic>(card.transform, "Full variant " + record.definition.Id, new UnityEngine.Rect(8, 58, cardWidth * .62f - 8, cardHeight - 104));
            full.SetRecipe(record.snapshot);
            int detailFragments = 0, detailVertices = 0; float detailGap = 0;
            if (record.neutral)
                label(card.transform, "НЕЙТРАЛЬНАЯ ОПЦИЯ\nОтдельного фрагмента нет.\nПолная сборка сохранена.", new UnityEngine.Rect(cardWidth * .62f + 8, 78, cardWidth * .38f - 20, cardHeight - 135), 19);
            else
            {
                bool weaponDetail = record.definition.Category >= PaperDefense.Core.ComponentCategory.Mechanism && record.definition.Category <= PaperDefense.Core.ComponentCategory.Trigger;
                var detailComposition = weaponDetail ? PaperDefense.Presentation.AssemblyVisualComposer.ComposeWeapon(recipe.Tower.Mounts[0].Weapon) : PaperDefense.Presentation.AssemblyVisualComposer.ComposePart(new PaperDefense.Core.PartRef(record.definition.Id, record.definition.Revision));
                if (!detailComposition.Fragments.Any(f => f.PartId == record.definition.Id)) throw new System.InvalidOperationException("The detail view lost its selected authored part: " + record.definition.Id);
                label(card.transform, weaponDetail ? "Оружие целиком" : "Выбранная деталь", new UnityEngine.Rect(cardWidth * .62f + 8, 54, cardWidth * .38f - 20, 23), 16);
                var detail = PaperDefense.Presentation.WorkshopUi.Graphic<PaperDefense.Presentation.WeaponAssemblyGraphic>(card.transform, "Connected detail " + record.definition.Id, new UnityEngine.Rect(cardWidth * .62f + 4, 78, cardWidth * .38f - 14, cardHeight - 127));
                detail.SetComposition(detailComposition); detailFragments = detailComposition.Fragments.Count;
                UnityEngine.Canvas.ForceUpdateCanvases();
                var detailMeasured = measure(detail, detailFragments); detailVertices = detailMeasured.vertices; detailGap = detailMeasured.gap;
            }
            UnityEngine.Canvas.ForceUpdateCanvases();
            var measured = measure(full, record.composition.Fragments.Count);
            int selectedFragments = record.composition.Fragments.Count(f => f.PartId == record.definition.Id);
            string fixture = recipe.Structure != null ? recipe.Structure.Family.ToString() : recipe.Tower.Mounts[0].Weapon.Mechanism.Id + " · " + recipe.Tower.Mounts[0].Weapon.Magazine.Id;
            label(card.transform, fixture, new UnityEngine.Rect(10, cardHeight - 43, cardWidth - 20, 22), 15);
            label(card.transform, (record.neutral ? "Нейтральная" : selectedFragments + " целевых фрагм.") + " · " + record.composition.Fragments.Count + " слоёв · поля " + measured.gap.ToString("0.0", invariant) + " px", new UnityEngine.Rect(10, cardHeight - 23, cardWidth - 20, 21), 14);
            proof.Add(string.Join(",", new[] { (page + 1).ToString(), record.definition.Id, record.definition.Category.ToString(), record.neutral.ToString(), string.Join("|", recipe.Parts.Select(p => p.Id + "@" + p.Revision)), selectedFragments.ToString(), record.composition.Fragments.Count.ToString(), measured.vertices.ToString(), measured.gap.ToString("0.######", invariant), detailFragments.ToString(), detailVertices.ToString(), detailGap.ToString("0.######", invariant) }));
        }
        UnityEngine.Canvas.ForceUpdateCanvases();
        paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("task13-variants-" + (page + 1) + "-1920x1080", width, height, "docs/validation/modular-album/screenshots"));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(owner); target.Release(); UnityEngine.Object.DestroyImmediate(target);
    }
}
System.IO.File.WriteAllLines("docs/validation/modular-album/task13-variants-geometry.csv", proof);
return string.Join("\n", paths) + "\n54 definitions: 52 selected raster PartIds in valid full compositions and 2 explicit neutral options; no profile/store was created.";
