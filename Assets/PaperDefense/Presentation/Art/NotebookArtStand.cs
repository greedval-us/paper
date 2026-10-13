using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public enum NotebookArtStandMode { Towers, Enemies, Effects }

    /// <summary>Isolated art gallery. It never opens a profile or starts a combat simulation.</summary>
    public sealed class NotebookArtStand : MonoBehaviour
    {
        public NotebookArtStandMode mode;
        public BalancePresetAsset preset;
        public TextResourcesAsset textResources;
        readonly List<NotebookArtGraphic> animated = new List<NotebookArtGraphic>();
        readonly List<string> enemyPrefixes = new List<string>();
        RectTransform page, content;
        Font font;
        UserTextResolver text;
        Text clockLabel, selectionLabel;
        NotebookArtGraphic selection;
        BlueprintSketch assembled;
        TowerBlueprint previewBlueprint;
        readonly NotebookAnimationState previewAnimation = new NotebookAnimationState();
        readonly SimulationEventBuffer previewEvents = new SimulationEventBuffer(16);
        readonly Dictionary<int, DrawingSnapshot> authored = new Dictionary<int, DrawingSnapshot>();
        readonly double[] nextFire = new double[3];
        DrawingEditorPanel drawingEditor;
        Func<int, Point2> previewAim;
        int combination;
        long previewAction;
        bool aiming = true, recoil = true;
        float seconds, speed = 1;
        bool paused;
        int effectsPage;
        string enemyPose = "walk";
        public int CardCount { get; private set; }
        public bool Paused => paused;
        public float Seconds => seconds;
        public NotebookArtStandMode Mode => mode;
        string T(string key, params object[] args) => text.Resolve(UserText.Message(key, args));

        void Awake()
        {
            text = textResources != null ? textResources.CreateResolver() : TextResourcesAsset.Default;
            font = Resources.Load<Font>("NotebookHand") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("Art gallery canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.matchWidthOrHeight = .5f;
            page = Panel(canvasGo.transform, "Notebook page", new Rect(0, 0, 1600, 1000), new Color(.975f, .961f, .9f));
            var grid = new GameObject("Notebook grid", typeof(RectTransform), typeof(CanvasRenderer), typeof(NotebookGalleryPaper));
            grid.transform.SetParent(page, false); Place(grid.GetComponent<RectTransform>(), new Rect(0, 0, 1600, 1000));
            grid.GetComponent<NotebookGalleryPaper>().raycastTarget = false;
            Label(page, T("art.title." + mode.ToString().ToLowerInvariant()), new Rect(38, 18, 1490, 60), 38, true);
            Label(page, T("art.hint." + mode.ToString().ToLowerInvariant()), new Rect(40, 78, 1490, 50), 19);
            Button(page, "Pause", new Rect(40, 135, 230, 43), T("art.pause"), () => paused = !paused);
            Button(page, "Step", new Rect(282, 135, 150, 43), T("art.step"), () => { paused = true; Advance(1f / 30); });
            Button(page, "Speed", new Rect(444, 135, 190, 43), T("art.speed"), () => speed = speed == 1 ? .25f : speed == .25f ? 2 : 1);
            Button(page, "Restart", new Rect(646, 135, 170, 43), T("art.restart"), () => { seconds = 0; Advance(0); });
            clockLabel = Label(page, "", new Rect(850, 138, 700, 38), 19);
            var navigator = MechanicsSceneNavigator.Attach(gameObject);
            Button(page, "Return to mechanics hub", new Rect(1305, 22, 255, 43), T("hub.back"), navigator.ReturnToHub);
            content = Panel(page, "Isolated gallery", new Rect(38, 195, 1524, 756), Color.clear);
            if (EventSystem.current == null)
            { var events = new GameObject("Art UI events", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(transform, false); }
            Rebuild(); Advance(0);
        }
        void Update() { if (!paused) Advance(Time.unscaledDeltaTime * speed); }
        public void SetPaused(bool value) { paused = value; }
        public void Advance(float delta)
        {
            if (delta == 0 && seconds == 0) ResetPreviewAnimation();
            seconds += Mathf.Max(0, delta);
            foreach (var graphic in animated) graphic.SetTime(seconds);
            if (previewBlueprint != null)
            {
                foreach (var mount in previewBlueprint.WeaponMounts)
                {
                    if (seconds < nextFire[mount.SlotIndex]) continue;
                    nextFire[mount.SlotIndex] = seconds + .75 + mount.SlotIndex * .23;
                    if (!recoil) continue;
                    previewEvents.Publish((long)(seconds * 30), SimulationEventKind.WeaponActionStarted, default, default,
                        LegacyContentIds.Weapon(mount.WeaponId), sourceStructureId: 1, mountSlotIndex: mount.SlotIndex,
                        actionId: ++previewAction, simulationSeconds: seconds, weaponId: mount.WeaponId);
                    previewAnimation.AddEvent(previewEvents[previewEvents.Count - 1]);
                }
                previewAnimation.Prune(seconds);
                assembled.SetPreviewPose(previewAnimation, seconds, aiming ? previewAim : null);
            }
            if (clockLabel != null) clockLabel.text = T("art.clock", seconds, speed);
        }
        void ResetPreviewAnimation()
        { previewAnimation.Clear(); for (int i = 0; i < 3; i++) nextFire[i] = seconds + .2 * i; }
        Point2 AimForSlot(int slot)
        {
            double angle = seconds * .55 + slot * 2.1;
            return new Point2((float)Math.Cos(angle) * 2, (float)Math.Sin(angle) * 2);
        }
        void Rebuild()
        {
            animated.Clear(); enemyPrefixes.Clear(); CardCount = 0;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            if (NotebookVisualThemeAsset.Default == null)
            { Label(content, T("art.missing"), new Rect(20, 20, 1450, 80), 24); return; }
            if (mode == NotebookArtStandMode.Towers) BuildTowers();
            else if (mode == NotebookArtStandMode.Enemies) BuildEnemies();
            else BuildEffects();
        }
        void BuildTowers()
        {
            string[] bases = { "standard", "heavy", "precision", "battery" };
            string[] weapons = { "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "blotter" };
            string[] modules = { "armor", "feed", "tracking", "rail" };
            Label(content, T("art.bases"), new Rect(0, 0, 1000, 32), 22, true);
            for (int i = 0; i < bases.Length; i++) Card("chassis." + bases[i], PartName("chassis." + bases[i]), new Rect(i * 250, 38, 236, 155));
            Label(content, T("art.weapons"), new Rect(0, 210, 1000, 32), 22, true);
            for (int i = 0; i < weapons.Length; i++) Card("weapon." + weapons[i], PartName("weapon." + weapons[i]), new Rect(i % 4 * 250, 248 + i / 4 * 156, 236, 145));
            Label(content, T("art.modules"), new Rect(0, 568, 1000, 32), 22, true);
            for (int i = 0; i < modules.Length; i++) Card("module." + modules[i], PartName("module." + modules[i]), new Rect(i * 250, 606, 236, 140));
            var panel = Panel(content, "Assembly preview", new Rect(1020, 0, 500, 746), new Color(1, .99f, .94f, .95f));
            Label(panel, T("art.combinations"), new Rect(18, 12, 464, 38), 25, true);
            var go = new GameObject("Combined tower", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
            go.transform.SetParent(panel, false); Place(go.GetComponent<RectTransform>(), new Rect(40, 50, 420, 285));
            assembled = go.GetComponent<BlueprintSketch>(); assembled.raycastTarget = false;
            previewAim = AimForSlot;
            Button(panel, "Aim", new Rect(18, 305, 226, 35), T("art.aim"), () => aiming = !aiming);
            Button(panel, "Recoil", new Rect(256, 305, 226, 35), T("art.recoil"), () => { recoil = !recoil; previewAnimation.Clear(); });
            for (int i = 0; i < 5; i++) { int variant = i; Button(panel, "Combination " + i, new Rect(18, 350 + i * 41, 464, 36), T("art.combo." + i), () => ShowCombination(variant)); }
            Button(panel, "Draw tower", new Rect(18, 561, 226, 39), T("art.draw"), () => drawingEditor.Open(previewBlueprint));
            Button(panel, "Reference tower", new Rect(256, 561, 226, 39), T("art.reference"), () => { authored.Remove(combination); ShowCombination(combination); });
            Label(panel, T("art.inspect"), new Rect(18, 609, 464, 30), 17);
            selection = Graphic(panel, "Selected part", new Rect(150, 639, 200, 70), "weapon.gun");
            selectionLabel = Label(panel, PartName("weapon.gun"), new Rect(18, 709, 464, 33), 18, true);
            drawingEditor = new DrawingEditorPanel(page, font, drawing => {
                authored[combination] = drawing; previewBlueprint = previewBlueprint.WithDrawing(drawing); assembled.SetBlueprint(previewBlueprint);
            }, text);
            ShowCombination(3);
        }
        public void ShowCombination(int variant)
        {
            if (preset == null || assembled == null) return;
            combination = variant;
            var balance = preset.CreateBalance();
            string chassis = variant == 0 ? "standard" : variant == 1 ? "heavy" : variant == 2 ? "precision" : "battery";
            string[] selected = variant == 0 ? new[] { "gun", "piercer" } : variant == 1 ? new[] { "mortar", "gun" }
                : variant == 2 ? new[] { "mortar" } : variant == 4 ? new[] { "gun", "gun", "gun" } : new[] { "gun", "piercer", "mortar" };
            var mounts = new List<WeaponMountSelection>(); for (int i = 0; i < selected.Length; i++) mounts.Add(new WeaponMountSelection(i, "weapon." + selected[i]));
            var modules = new List<ModuleSelection>();
            if (variant > 0 && variant < 4) modules.Add(new ModuleSelection("armor", "module.armor"));
            if (variant == 2) { modules.Add(new ModuleSelection("feed", "module.feed")); modules.Add(new ModuleSelection("tracking", "module.tracking")); modules.Add(new ModuleSelection("rail", "module.rail")); }
            if (variant == 3) modules.Add(new ModuleSelection("tracking", "module.tracking"));
            var blueprint = new TowerBlueprint("art.combo." + variant, T("art.combo." + variant), "chassis." + chassis, mounts, modules, content: balance.Content);
            previewBlueprint = authored.TryGetValue(variant, out var drawing) ? blueprint.WithDrawing(drawing) : blueprint;
            assembled.SetBlueprint(previewBlueprint); ResetPreviewAnimation(); Advance(0);
        }
        void BuildEnemies()
        {
            string[] poses = { "idle", "walk", "attack", "hit", "death" };
            for (int i = 0; i < poses.Length; i++) { string pose = poses[i]; Button(content, "Pose " + pose, new Rect(i * 302, 0, 288, 43), T("art.pose." + pose), () => SetEnemyPose(pose)); }
            string[] kinds = { "doodle", "runner", "gnawer", "spitter" }, tiers = { "sketch", "outlined", "hatched" };
            for (int row = 0; row < tiers.Length; row++)
                for (int col = 0; col < kinds.Length; col++)
                {
                    string prefix = "enemy." + kinds[col] + "." + tiers[row];
                    enemyPrefixes.Add(prefix);
                    Card(prefix + ".idle.0", T("art.enemy_variant", T("art.enemy." + kinds[col]), T("art.tier." + tiers[row])),
                        new Rect(col * 382, 62 + row * 230, 368, 214), prefix + "." + enemyPose);
                }
        }
        public void SetEnemyPose(string pose)
        {
            enemyPose = pose; seconds = 0;
            for (int i = 0; i < enemyPrefixes.Count; i++) animated[i].Bind(null, enemyPrefixes[i] + "." + pose);
            Advance(0);
        }
        void BuildEffects()
        {
            Button(content, "Effects page", new Rect(0, 0, 1490, 43), T("art.effects_page", effectsPage + 1), () => { effectsPage = (effectsPage + 1) % 3; Rebuild(); });
            var theme = NotebookVisualThemeAsset.Default; var ids = new List<string>();
            if (effectsPage == 0) { foreach (var clip in theme.Data.Clips) if (clip.Id.StartsWith("fx.", StringComparison.Ordinal)) ids.Add(clip.Id); }
            else if (effectsPage == 1) { ids.Add("object.castle"); ids.Add("object.castle.damaged"); ids.Add("object.spikes"); ids.Add("object.blot"); foreach (var shape in theme.Data.Shapes) if (shape.Id.StartsWith("object.well.", StringComparison.Ordinal)) ids.Add(shape.Id); }
            else for (int i = 0; i < 16; i++) ids.Add("object.wall." + i);
            int columns = effectsPage == 0 ? 6 : 4; float width = 1524f / columns, height = effectsPage == 0 ? 166 : effectsPage == 2 ? 166 : 218;
            for (int i = 0; i < ids.Count; i++)
                Card(ids[i], EffectName(ids[i]), new Rect(i % columns * width, 60 + i / columns * height, width - 12, height - 10), theme.FindClip(ids[i]) != null ? ids[i] : null);
        }
        string EffectName(string id)
        {
            if (id.StartsWith("object.wall.", StringComparison.Ordinal)) return T("art.wall_mask", int.Parse(id.Substring(12)));
            return T("art.label." + id);
        }
        string PartName(string id)
        {
            var catalog = preset?.CreateBalance().Content;
            if (catalog == null) return T("art.part");
            return GameText.Resolve(text, id.StartsWith("chassis.", StringComparison.Ordinal) ? catalog.GetChassis(id).Name
                : id.StartsWith("weapon.", StringComparison.Ordinal) ? catalog.GetWeapon(id).Name : catalog.GetModule(id).Name);
        }
        void Card(string id, string title, Rect box, string clip = null)
        {
            CardCount++;
            var panel = Panel(content, "Art card " + id, box, new Color(1, .995f, .96f, .96f));
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>();
            button.onClick.AddListener(() => { if (selection != null) { selection.Bind(id, clip); selectionLabel.text = title; } });
            var art = Graphic(panel, "Resource art", new Rect(12, 6, box.width - 24, box.height - 45), id, clip);
            art.RepeatPreview = clip != null;
            if (clip != null) animated.Add(art);
            Label(panel, title, new Rect(8, box.height - 39, box.width - 16, 33), 17, true, TextAnchor.MiddleCenter);
        }
        NotebookArtGraphic Graphic(Transform parent, string name, Rect box, string shape, string clip = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(NotebookArtGraphic));
            go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), box);
            var graphic = go.GetComponent<NotebookArtGraphic>(); graphic.raycastTarget = false; graphic.Bind(shape, clip); return graphic;
        }
        RectTransform Panel(Transform parent, string name, Rect box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); Place(rect, box); var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            if (color.a > 0) { var border = go.AddComponent<Outline>(); border.effectColor = new Color(.07f, .12f, .33f, .3f); border.effectDistance = new Vector2(1, -1); }
            return rect;
        }
        Text Label(Transform parent, string value, Rect box, int size, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var go = new GameObject("Resource label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), box);
            var label = go.GetComponent<Text>(); label.font = font; label.text = value; label.fontSize = size; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = NotebookPalette.Navy; label.alignment = align; label.raycastTarget = false; return label;
        }
        void Button(Transform parent, string name, Rect box, string value, Action action)
        {
            var panel = Panel(parent, name, box, Color.white); panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>(); button.onClick.AddListener(() => action());
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            Label(panel, value, new Rect(4, 1, box.width - 8, box.height - 2), 18, true, TextAnchor.MiddleCenter);
        }
        static void Place(RectTransform rect, Rect box)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(box.x, -box.y); rect.sizeDelta = box.size; }
    }

    internal sealed class NotebookGalleryPaper : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; Color grid = new Color(.25f, .46f, .62f, .12f);
            for (float x = r.xMin; x < r.xMax; x += 32) BlueprintSketch.Line(vh, new Vector2(x, r.yMin), new Vector2(x, r.yMax), 1, grid);
            for (float y = r.yMin; y < r.yMax; y += 32) BlueprintSketch.Line(vh, new Vector2(r.xMin, y), new Vector2(r.xMax, y), 1, grid);
            BlueprintSketch.Line(vh, new Vector2(r.xMin + 28, r.yMin), new Vector2(r.xMin + 28, r.yMax), 2, new Color(.8f, .13f, .25f, .25f));
        }
    }
}
