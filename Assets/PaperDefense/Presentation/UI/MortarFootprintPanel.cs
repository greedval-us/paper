using System;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
namespace PaperDefense.Presentation
{
    public sealed class MortarFootprintPanel
    {
        readonly UserTextResolver textResolver;
        string Resolve(string value) => GameText.Resolve(textResolver, value);
        readonly GameObject sheet;
        readonly Func<TowerBlueprint, string> validationReason;
        readonly Action<TowerBlueprint> changeDraft;
        readonly Func<TowerBlueprint, int, MortarFootprint, MortarFootprint, FootprintFormation, FootprintProbeComparison> compare;
        readonly FootprintProbeSketch first, second;
        readonly UnityEngine.UI.Text summary;
        readonly UnityEngine.UI.Text instructions, aspectLabel;
        float maximumAspect = MortarFootprint.MaximumAspectRatio;
        TowerBlueprint blueprint;
        FootprintFormation formation;
        float started;
        public bool IsOpen => sheet.activeSelf;
        public bool IsDragging { get; private set; }
        public int Mount { get; private set; }
        public MortarFootprint Baseline { get; private set; }
        public FootprintProbeComparison Recording { get; private set; }
        public FootprintAxisGraphic Axis { get; }
        public float PlaybackSeconds => Recording == null
            ? 0
            : Mathf.Repeat(Time.unscaledTime - started, Duration + .8f);

        float Duration => Recording == null
            ? 0
            : Mathf.Max(Recording.First.Frames[Recording.First.Frames.Count - 1].Seconds,
                Recording.Second.Frames[Recording.Second.Frames.Count - 1].Seconds);

        public MortarFootprintPanel(Transform parent, Font font, Action<TowerBlueprint> changeDraft,
            Func<TowerBlueprint, int, MortarFootprint, MortarFootprint, FootprintFormation, FootprintProbeComparison> compare,
            Func<TowerBlueprint, string> validationReason, UserTextResolver textResolver = null)
        {
            this.textResolver = textResolver;
            this.changeDraft = changeDraft;
            this.compare = compare;
            this.validationReason = validationReason;
            sheet = new GameObject("Mortar footprint sheet", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            sheet.transform.SetParent(parent, false);
            Place(sheet.GetComponent<RectTransform>(), new Rect(90, 115, 1420, 770));
            sheet.GetComponent<UnityEngine.UI.Image>().color = NotebookPalette.Paper;
            Text(font, UserText.Message("copy.90b777be2577be25"), new Rect(30, 20, 1350, 50), 28);
            instructions = Text(font, "", new Rect(30, 85, 390, 65), 19);
            var axis = new GameObject("Footprint axis", typeof(RectTransform), typeof(CanvasRenderer), typeof(FootprintAxisGraphic));
            axis.transform.SetParent(sheet.transform, false);
            Place(axis.GetComponent<RectTransform>(), new Rect(30, 160, 390, 400));
            Axis = axis.GetComponent<FootprintAxisGraphic>();
            Axis.Configure(BeginDrag, Commit);
            aspectLabel = Text(font, "", new Rect(30, 575, 390, 45), 22);
            aspectLabel.name = "Footprint aspect ratio";
            Axis.AspectChanged = q => aspectLabel.text = Resolve(UserText.Message("copy.031804e2db29b843", q, maximumAspect));
            Text(font, UserText.Message("copy.74f7ef7fd7c9851c"), new Rect(460, 95, 440, 35), 21);
            Text(font, UserText.Message("copy.2a5e89a7fb3c531b"), new Rect(940, 95, 440, 35), 21);
            first = Sketch("Footprint replay A", new Rect(460, 150, 440, 360));
            second = Sketch("Footprint replay B", new Rect(940, 150, 440, 360));
            summary = Text(font, "", new Rect(460, 535, 920, 100), 20);
            summary.name = "Footprint probe feedback";
            Button(font, "Footprint group", UserText.Message("copy.907efbd4ce48253b"), new Rect(30, 650, 170, 50), () => {
                formation = FootprintFormation.Group;
                Record();
            });
            Button(font, "Footprint column", UserText.Message("copy.322ef2503cd18ed7"), new Rect(215, 650, 170, 50), () => {
                formation = FootprintFormation.Column;
                Record();
            });
            Button(font, "Footprint repeat", UserText.Message("copy.5189135a6110bd31"), new Rect(400, 650, 190, 50), Record);
            Button(font, "Footprint pin A", UserText.Message("copy.d1ba6a8cc7a3945f"), new Rect(605, 650, 290, 50), PinBaseline);
            Button(font, "Footprint circle", UserText.Message("copy.bbe6ba35fc18081c"), new Rect(910, 650, 170, 50), SetCircle);
            Button(font, "Close footprint", UserText.Message("copy.6c99e7d8fbd8fa26"), new Rect(1095, 650, 285, 50), Close);
            sheet.SetActive(false);
        }
        public void Open(TowerBlueprint value, int mount)
        {
            value.GetMortarFootprint(mount);
            blueprint = value;
            Mount = mount;
            string weaponId = value.GetWeaponId(mount);
            maximumAspect = value.Content?.GetWeapon(weaponId).MaximumFootprintAspectRatio ?? MortarFootprint.MaximumAspectRatio;
            instructions.text = Resolve(UserText.Message("copy.c21bab5f45cb2479", maximumAspect));
            Axis.SetMaximum(maximumAspect);
            Baseline = value.GetMortarFootprint(mount);
            IsDragging = false;
            Axis.SetAspect(Baseline.AspectRatio);
            sheet.SetActive(true);
            sheet.transform.SetAsLastSibling();
            Record();
        }
        public void Close()
        {
            IsDragging = false;
            Axis.Cancel();
            sheet.SetActive(false);
            Clear();
        }
        public void PinBaseline()
        {
            if (!IsOpen || IsDragging)
            {
                return;
            }
            Baseline = blueprint.GetMortarFootprint(Mount);
            Record();
        }
        public void SetCircle()
        {
            if (!IsOpen)
            {
                return;
            }
            Axis.Cancel();
            IsDragging = false;
            Commit(1);
        }
        void BeginDrag()
        {
            IsDragging = true;
            Clear();
        }
        void Commit(float q)
        {
            if (!IsOpen)
            {
                return;
            }
            IsDragging = false;
            blueprint = blueprint.WithMortarFootprint(Mount, new MortarFootprint(q, maximumAspect));
            changeDraft(blueprint);
            Axis.SetAspect(q);
            Record();
        }
        void Clear()
        {
            Recording = null;
            first.SetRecording(null, () => 0);
            second.SetRecording(null, () => 0);
            summary.text = Resolve(UserText.Message("copy.5d1f1f28bccfb4d6"));
        }
        void Record()
        {
            if (!IsOpen || IsDragging)
            {
                return;
            }
            string reason = validationReason(blueprint);
            if (!string.IsNullOrEmpty(reason))
            {
                Clear();
                summary.text = Resolve(UserText.Message("copy.e7e92489666048de", reason));
                return;
            }
            Recording = compare(blueprint, Mount, Baseline, blueprint.GetMortarFootprint(Mount), formation);
            started = Time.unscaledTime;
            first.SetRecording(Recording.First, () => PlaybackSeconds);
            second.SetRecording(Recording.Second, () => PlaybackSeconds);
            summary.text = Resolve(Values(UserText.Message("common.replay_a"), Recording.First) + "\n" + Values(UserText.Message("common.replay_b"), Recording.Second));
        }
        static string Values(string name, FootprintProbeReport report)
        {
            string result = UserText.Message("copy.9f7043ce99bb7fc9", name, report.HitCount, report.MissedCount, report.Cost);
            return report.Completed ? result : UserText.Message("common.with_detail", result, report.Message);
        }

        FootprintProbeSketch Sketch(string name, Rect box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(FootprintProbeSketch));
            go.transform.SetParent(sheet.transform, false);
            Place(go.GetComponent<RectTransform>(), box);
            var result = go.GetComponent<FootprintProbeSketch>();
            result.raycastTarget = false;
            return result;
        }
        UnityEngine.UI.Text Text(Font font, string value, Rect box, int size)
        {
            var go = new GameObject("Footprint text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(sheet.transform, false);
            Place(go.GetComponent<RectTransform>(), box);
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = size;
            text.text = Resolve(value);
            text.color = NotebookPalette.Navy;
            text.raycastTarget = false;
            return text;
        }
        void Button(Font font, string name, string value, Rect box, Action action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(sheet.transform, false);
            Place(go.GetComponent<RectTransform>(), box);
            go.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => action());
            var label = Text(font, value, new Rect(5, 5, box.width - 10, box.height - 10), 18);
            label.transform.SetParent(go.transform, false);
            label.alignment = TextAnchor.MiddleCenter;
        }
        internal static void Place(RectTransform rect, Rect box)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(box.x, -box.y);
            rect.sizeDelta = box.size;
        }
    }
    public sealed class FootprintAxisGraphic : UnityEngine.UI.Graphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<float> AspectChanged { get; set; }
        Action begin;
        Action<float> commit;
        bool dragging;
        int pointerId;
        const float Radius = 82;
        float maximumAspect = MortarFootprint.MaximumAspectRatio;
        float DisplayRadius => Mathf.Min(Radius, rectTransform.rect.height * .43f / Mathf.Sqrt(maximumAspect));
        public float AspectRatio { get; private set; } = 1;
        public Vector2 Endpoint => rectTransform.rect.center + Vector2.up * DisplayRadius * Mathf.Sqrt(AspectRatio);
        internal void SetMaximum(float maximum) { maximumAspect = maximum; SetVerticesDirty(); }
        public Vector2 LocalPointToScreen(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, rectTransform.TransformPoint(local));
        internal void Configure(Action start, Action<float> end)
        {
            begin = start;
            commit = end;
            color = NotebookPalette.Blue;
        }
        internal void SetAspect(float q)
        {
            AspectRatio = q;
            AspectChanged?.Invoke(q);
            SetVerticesDirty();
        }
        internal void Cancel()
        {
            dragging = false;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out var p) || (p - Endpoint).sqrMagnitude>28 * 28)
            {
                return;
            }
            dragging = true;
            pointerId = data.pointerId;
            begin?.Invoke();
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != pointerId)
            {
                return;
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out var p))
            {
                float forwardDistance = Mathf.Max(0, p.y - rectTransform.rect.center.y);
                float q = Mathf.Pow(forwardDistance / DisplayRadius, 2);
                SetAspect(Mathf.Clamp(q, 1, maximumAspect));
            }
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (!dragging || data.pointerId != pointerId)
            {
                return;
            }
            dragging = false;
            commit?.Invoke(AspectRatio);
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            var c = rectTransform.rect.center;
            float along = DisplayRadius * Mathf.Sqrt(AspectRatio);
            float across = DisplayRadius / Mathf.Sqrt(AspectRatio);
            for (int i = 0; i<64; i++)
            {
                float a = i * Mathf.PI / 32, b = (i + 1) * Mathf.PI / 32;
                BlueprintSketch.Line(vh, c + new Vector2(Mathf.Cos(a) * across, Mathf.Sin(a) * along), c + new Vector2(Mathf.Cos(b) * across, Mathf.Sin(b) * along), 3, NotebookPalette.Blue);
            }
            BlueprintSketch.Line(vh, c, c + Vector2.up * 175, 2, NotebookPalette.Navy);
            var tip = c + Vector2.up * 175;
            BlueprintSketch.Line(vh, tip, tip + new Vector2(-9, -14), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, tip, tip + new Vector2(9, -14), 2, NotebookPalette.Navy);
            BlueprintSketch.Ring(vh, c, 5, 3, NotebookPalette.Navy);
            BlueprintSketch.Ring(vh, Endpoint, 9, 5, NotebookPalette.Red);
            BlueprintSketch.Ring(vh, c + Vector2.up * DisplayRadius, 3, 2, NotebookPalette.Navy);
            BlueprintSketch.Ring(vh, c + Vector2.up * DisplayRadius * Mathf.Sqrt(maximumAspect), 3, 2, NotebookPalette.Navy);
        }
    }
}
