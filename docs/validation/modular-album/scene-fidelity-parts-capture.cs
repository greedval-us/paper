if (!UnityEngine.Application.isPlaying || !string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
    throw new System.InvalidOperationException("Run the part audit only in the isolated, unsaved empty PlayMode scene.");
var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<PaperDefense.Application.AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
var snapshots = new System.Collections.Generic.List<(string id, string title, PaperDefense.Core.RecipeSnapshot snapshot)>();
System.Func<string, PaperDefense.Core.PartRef> part = id => new PaperDefense.Core.PartRef(id, 1);
System.Func<string, PaperDefense.Core.WeaponRecipe> weapon = id => new PaperDefense.Core.WeaponRecipe(part(id), part("head." + id.Substring(5)), part("sight.iron"), part("magazine.standard"), part("trigger.standard"));
System.Func<string, PaperDefense.Core.FoundationRecipe> foundation = id => new PaperDefense.Core.FoundationRecipe(part(id), part("hull.standard"), part("armor.none"), part("feed.standard"), part("turn.standard"));
foreach (var core in catalog.Definitions.Where(d => d.Category == PaperDefense.Core.ComponentCategory.Mechanism))
{
    var recipe = new PaperDefense.Core.AssemblyRecipe(new PaperDefense.Core.TowerRecipe(foundation("frame.standard"), new[] { new PaperDefense.Core.WeaponMountRecipe(0, weapon(core.Id)) }));
    snapshots.Add((core.Id, core.Name, new PaperDefense.Core.RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions)));
}
foreach (var entry in catalog.Starters.Where(e => e.Kind == PaperDefense.Core.AlbumEntryKind.Structure)) snapshots.Add((entry.Id, entry.Name, entry.Snapshot));
var batteryRecipe = new PaperDefense.Core.AssemblyRecipe(new PaperDefense.Core.TowerRecipe(foundation("frame.battery"), System.Linq.Enumerable.Range(0, 3).Select(slot => new PaperDefense.Core.WeaponMountRecipe(slot, weapon("core.gun")))));
snapshots.Add(("frame.battery.3", "Батарея · 3 ствола", new PaperDefense.Core.RecipeSnapshot(batteryRecipe, catalog.BalanceRevision, catalog.Definitions)));
if (snapshots.Count != 12) throw new System.InvalidOperationException("The contact sheet must contain exactly seven weapons, four supports and one three-barrel battery.");
var paths = new System.Collections.Generic.List<string>();
var metrics = new System.Collections.Generic.List<string> { "Resolution,Pose,Definition,Fragments,Anchors,Vertices,MinimumPixelGap,MinimumAxisClearanceU,BoundsWidthU,BoundsHeightU" };
var invariant = System.Globalization.CultureInfo.InvariantCulture;
var font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
foreach (var size in new[] { new UnityEngine.Vector2Int(1920, 1080), new UnityEngine.Vector2Int(1024, 768) })
foreach (string poseName in new[] { "preview", "rest", "turn90", "max-recoil" })
{
    var owner = new UnityEngine.GameObject("Task13 isolated part audit " + poseName);
    var target = new UnityEngine.RenderTexture(size.x, size.y, 24);
    try
    {
        target.Create();
        var cameraObject = new UnityEngine.GameObject("Part audit viewport", typeof(UnityEngine.Camera)); cameraObject.transform.SetParent(owner.transform, false);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.targetTexture = target;
        var canvasObject = new UnityEngine.GameObject("Actual composer contact sheet", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas)); canvasObject.transform.SetParent(owner.transform, false);
        var canvas = canvasObject.GetComponent<UnityEngine.Canvas>(); canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.scaleFactor = 1;
        var paper = PaperDefense.Presentation.WorkshopUi.Graphic<UnityEngine.UI.Image>(canvas.transform, "Audit paper", new UnityEngine.Rect(0, 0, size.x, size.y)); paper.color = new UnityEngine.Color32(248, 244, 226, 255);
        System.Action<UnityEngine.Transform, string, UnityEngine.Rect, int> label = (parent, text, bounds, fontSize) =>
        {
            var node = PaperDefense.Presentation.WorkshopUi.Graphic<UnityEngine.UI.Text>(parent, text, bounds);
            node.font = font; node.text = text; node.fontSize = fontSize; node.color = new UnityEngine.Color32(26, 48, 110, 255);
            node.alignment = UnityEngine.TextAnchor.MiddleLeft; node.supportRichText = false;
            node.horizontalOverflow = UnityEngine.HorizontalWrapMode.Wrap; node.verticalOverflow = UnityEngine.VerticalWrapMode.Truncate;
        };
        label(canvas.transform, "СОСТАВНЫЕ ОБЪЕКТЫ · " + poseName + " · " + size.x + " × " + size.y, new UnityEngine.Rect(18, 5, size.x - 36, 31), size.x == 1024 ? 20 : 25);
        float gap = size.x == 1024 ? 10 : 18, top = 45, cardWidth = (size.x - gap * 4) / 3f, cardHeight = (size.y - top - gap * 5) / 4f;
        for (int index = 0; index < snapshots.Count; index++)
        {
            var selected = snapshots[index]; var recipe = selected.snapshot.Recipe;
            var poses = new System.Collections.Generic.List<PaperDefense.Presentation.AssemblyMountPose>();
            if (recipe.Tower != null) foreach (var mount in recipe.Tower.Mounts)
            {
                PaperDefense.Presentation.AssemblyVisualComposer.Fits.TryGetWeapon(mount.Weapon.Mechanism.Id, mount.Weapon.Mechanism.Revision, out var fit);
                float radians = poseName == "turn90" ? (mount.SlotIndex % 2 == 0 ? 1 : -1) * UnityEngine.Mathf.PI / 2 : poseName == "preview" ? -UnityEngine.Mathf.PI / 4 : 0;
                poses.Add(new PaperDefense.Presentation.AssemblyMountPose(mount.SlotIndex, radians, poseName == "max-recoil" ? -fit.RecoilLimit : 0));
            }
            var pose = new PaperDefense.Presentation.AssemblyVisualPose(poses);
            var composition = PaperDefense.Presentation.AssemblyVisualComposer.Compose(selected.snapshot, pose);
            var diagnostics = PaperDefense.Core.AssemblyFitRules.Validate(recipe, PaperDefense.Presentation.AssemblyVisualComposer.Fits);
            if (diagnostics.Count != 0) throw new System.InvalidOperationException("Uncertified contact-sheet recipe: " + selected.id);
            float x = gap + index % 3 * (cardWidth + gap), y = top + gap + index / 3 * (cardHeight + gap);
            var panel = PaperDefense.Presentation.WorkshopUi.Graphic<UnityEngine.UI.Image>(canvas.transform, "Card " + selected.id, new UnityEngine.Rect(x, y, cardWidth, cardHeight)); panel.color = new UnityEngine.Color32(255, 253, 242, 255);
            label(panel.transform, selected.title, new UnityEngine.Rect(9, 1, cardWidth - 18, 26), size.x == 1024 ? 15 : 21);
            var graphic = PaperDefense.Presentation.WorkshopUi.Graphic<PaperDefense.Presentation.WeaponAssemblyGraphic>(panel.transform, "Real assembly " + selected.id, new UnityEngine.Rect(8, 29, cardWidth - 16, cardHeight - 57));
            graphic.SetRecipe(selected.snapshot, pose);
            UnityEngine.Canvas.ForceUpdateCanvases();
            int vertices; float minimumGap;
            using (var mesh = new UnityEngine.UI.VertexHelper())
            {
                typeof(PaperDefense.Presentation.WeaponAssemblyGraphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null).Invoke(graphic, new object[] { mesh });
                vertices = mesh.currentVertCount;
                if (vertices < composition.Fragments.Count * 4) throw new System.InvalidOperationException("Missing raster fragment in " + selected.id);
                var vertex = new UnityEngine.UIVertex(); mesh.PopulateUIVertex(ref vertex, 0); UnityEngine.Vector2 low = vertex.position, high = vertex.position;
                for (int v = 1; v < mesh.currentVertCount; v++) { mesh.PopulateUIVertex(ref vertex, v); low = UnityEngine.Vector2.Min(low, vertex.position); high = UnityEngine.Vector2.Max(high, vertex.position); }
                var available = graphic.rectTransform.rect;
                minimumGap = UnityEngine.Mathf.Min(low.x - available.xMin, available.xMax - high.x, low.y - available.yMin, available.yMax - high.y);
                if (minimumGap < 1) throw new System.InvalidOperationException("Art clips its actual viewport: " + selected.id + " gap=" + minimumGap);
            }
            double clearance = double.PositiveInfinity;
            for (int a = 0; a < composition.Anchors.Count; a++) for (int b = a + 1; b < composition.Anchors.Count; b++)
            {
                var first = composition.Anchors[a]; var second = composition.Anchors[b];
                var w1 = recipe.Tower.Mounts.Single(m => m.SlotIndex == first.SlotIndex).Weapon;
                var w2 = recipe.Tower.Mounts.Single(m => m.SlotIndex == second.SlotIndex).Weapon;
                PaperDefense.Presentation.AssemblyVisualComposer.Fits.TryGetWeapon(w1.Mechanism.Id, w1.Mechanism.Revision, out var f1);
                PaperDefense.Presentation.AssemblyVisualComposer.Fits.TryGetWeapon(w2.Mechanism.Id, w2.Mechanism.Revision, out var f2);
                clearance = System.Math.Min(clearance, UnityEngine.Vector2.Distance(first.RotationCenter, second.RotationCenter) - PaperDefense.Core.AssemblyFitRules.SweptRadius(f1) - PaperDefense.Core.AssemblyFitRules.SweptRadius(f2));
            }
            string clear = double.IsPositiveInfinity(clearance) ? "" : clearance.ToString("0.######", invariant);
            label(panel.transform, composition.Fragments.Count + " слоёв · " + composition.Anchors.Count + " дул · зазор " + minimumGap.ToString("0.0", invariant) + " px", new UnityEngine.Rect(9, cardHeight - 25, cardWidth - 18, 23), size.x == 1024 ? 11 : 14);
            metrics.Add(string.Join(",", new[] { size.x + "x" + size.y, poseName, selected.id, composition.Fragments.Count.ToString(), composition.Anchors.Count.ToString(), vertices.ToString(), minimumGap.ToString("0.######", invariant), clear, composition.Bounds.width.ToString("0.######", invariant), composition.Bounds.height.ToString("0.######", invariant) }));
        }
        UnityEngine.Canvas.ForceUpdateCanvases();
        paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("fidelity-parts-" + poseName + "-" + size.x + "x" + size.y, size.x, size.y, "docs/validation/modular-album/fidelity-screenshots"));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(owner); target.Release(); UnityEngine.Object.DestroyImmediate(target);
    }
}
System.IO.File.WriteAllLines("docs/validation/modular-album/fidelity-parts-geometry.csv", metrics);
return string.Join("\n", paths) + "\n" + (metrics.Count - 1) + " actual compositions measured; no profile/store was created.";
