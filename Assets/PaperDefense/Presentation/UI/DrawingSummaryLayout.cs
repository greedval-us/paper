using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Screen 06's live readout and existing one-shot probe, without changing drawing commands.</summary>
    internal sealed class DrawingSummaryLayout
    {
        sealed class Row
        {
            public Text Label, Value;
            public Graphic Icon;
            public DrawingSummaryDetailGraphic Accent;
        }

        static readonly Color[] MetricColors =
        {
            new Color32(40, 148, 105, 255), new Color32(207, 159, 27, 255), WorkshopUi.Ink,
            new Color32(189, 60, 72, 255), WorkshopUi.Blue, new Color32(126, 87, 195, 255)
        };
        readonly WorkshopUi ui;
        readonly Transform summary;
        readonly Text title, ink;
        readonly RectTransform track, fill, details;
        readonly Button detailToggle;
        readonly ScrollRect detailScroll;
        readonly Text detailText;
        readonly DrawingSummaryDetailGraphic paper, ribbon, inkHatching, probePaper, probeRibbon;
        readonly FoundationDetailGraphic inkBorder, separator, detailFrame, probeViewportFrame;
        readonly Row[] rows = new Row[6];
        readonly RectTransform probe;
        readonly Text probeHeading;
        readonly Image probeScenery;
        readonly Text rangeChange;
        readonly FoundationDetailGraphic rangeChangeFrame;
        float width = 432, height = 676, topHeight;

        internal DrawingSummaryLayout(Transform summary, WorkshopUi ui,DrawingProbePanel liveProbe=null)
        {
            this.summary = summary; this.ui = ui;
            if(liveProbe!=null){liveProbe.CompactDistance=true;liveProbe.ReadyHint="Один залп текущего ствола. Мишени без брони.";}
            var surface = summary.GetComponent<Graphic>();
            if (surface != null) surface.color = Color.clear;
            title = summary.Find("Guided drawing summary title").GetComponent<Text>();
            title.text = "Твой рисунок";
            ink = summary.Find("Guided drawing ink").GetComponent<Text>();
            track = (RectTransform)summary.Find("Guided drawing ink track");
            fill = (RectTransform)track.Find("Guided drawing ink fill");
            summary.Find("Guided drawing tower stats").GetComponent<Text>().enabled = false;
            summary.Find("Guided drawing mount stats").GetComponent<Text>().enabled = false;
            summary.Find("Guided drawing status").GetComponent<Text>().enabled = false;

            paper = Detail(summary, "Drawing summary folded paper", DrawingSummaryDetailGraphic.Kind.FoldedPaper);
            paper.transform.SetAsFirstSibling();
            ribbon = Detail(summary, "Drawing summary title ribbon", DrawingSummaryDetailGraphic.Kind.Ribbon);
            ribbon.transform.SetSiblingIndex(paper.transform.GetSiblingIndex() + 1);
            title.transform.SetSiblingIndex(ribbon.transform.GetSiblingIndex() + 1);
            var inkIcon = WorkshopUi.Graphic<WorkshopIconGraphic>(summary, "Drawing summary ink icon", Rect.zero);
            inkIcon.Icon = WorkshopIcon.Ink; inkIcon.color = WorkshopUi.Blue;
            track.GetComponent<Graphic>().color = new Color32(222, 226, 232, 255);
            inkBorder = Frame(track, "Drawing summary ink border"); inkBorder.Radius = 7;
            inkHatching = Detail(track, "Drawing summary ink hatching", DrawingSummaryDetailGraphic.Kind.InkHatching);
            string[] names = { "Прочность", "Нагрузка", "Цена", "Урон", "Дальность", "Пауза" };
            string[] keys = { "health", "load", "cost", "damage", "range", "interval" };
            WorkshopIcon[] icons = { WorkshopIcon.Shield, WorkshopIcon.Tower, WorkshopIcon.Pencil,
                WorkshopIcon.Damage, WorkshopIcon.Target, WorkshopIcon.None };
            for (int i = 0; i < rows.Length; i++)
            {
                var row = new Row();
                row.Label = Label(summary, "Drawing summary " + keys[i] + " label", names[i], 22, true);
                row.Value = Label(summary, "Drawing summary " + keys[i] + " value", "—", 24, true);
                row.Value.alignment = TextAnchor.MiddleRight;
                if (i == 5) row.Icon = Detail(summary, "Drawing summary interval icon", DrawingSummaryDetailGraphic.Kind.Clock);
                else
                {
                    var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(summary, "Drawing summary " + keys[i] + " icon", Rect.zero);
                    icon.Icon = icons[i]; row.Icon = icon;
                }
                row.Icon.color = WorkshopUi.Ink;
                row.Accent = Detail(summary, "Drawing summary " + keys[i] + " ink accent",
                    DrawingSummaryDetailGraphic.Kind.LoadSegments);
                row.Accent.color = MetricColors[i]; rows[i] = row;
                row.Accent.gameObject.SetActive(i != 2);
                row.Label.raycastTarget = true;
                ui.Tip(row.Label.gameObject, i == 1 ? "Нагрузка деталей / вместимость текущего корпуса."
                    : i == 2 ? "Точная стоимость текущего рисунка в штрихах."
                    : "Относительная шкала формы; точное значение справа.");
            }
            separator = WorkshopUi.Graphic<FoundationDetailGraphic>(summary, "Drawing summary details divider", Rect.zero);
            rangeChangeFrame=Frame(summary,"Drawing range change frame");rangeChangeFrame.Radius=5;
            rangeChangeFrame.Fill=new Color32(224,244,224,255);rangeChangeFrame.color=WorkshopUi.Success;
            rangeChange=Label(summary,"Drawing range change","",18,true);rangeChange.alignment=TextAnchor.MiddleCenter;
            rangeChange.color=WorkshopUi.Success;
            rangeChange.gameObject.SetActive(false);rangeChangeFrame.gameObject.SetActive(false);
            separator.Shape = FoundationDetailGraphic.Kind.Divider; separator.color = new Color32(157, 169, 185, 255);
            detailToggle = summary.Find("Guided drawing details toggle").GetComponent<Button>();
            detailToggle.GetComponentInChildren<Text>().text="Подробности";
            detailFrame = Frame(detailToggle.transform, "Drawing summary details frame");
            detailFrame.Fill = Color.clear; detailFrame.Radius = 4; detailFrame.transform.SetAsFirstSibling();
            detailToggle.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.SetAlpha(0);
            detailToggle.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.cullTransparentMesh = false;
            ((WorkshopButton)detailToggle).SetDecoration(detailFrame);
            details = (RectTransform)summary.Find("Guided drawing details");
            detailScroll = details.GetComponent<ScrollRect>();
            detailText = detailScroll.content.GetComponentInChildren<Text>(true);
            var detailPaper = Detail(details, "Drawing detailed values paper", DrawingSummaryDetailGraphic.Kind.FoldedPaper);
            detailPaper.transform.SetAsFirstSibling(); Stretch(detailPaper.rectTransform);

            probe = summary.Find("Drawing probe pane") as RectTransform;
            if (probe != null)
            {
                var sketch=probe.GetComponentInChildren<FootprintProbeSketch>();sketch.ActorScale=4;sketch.ShowWorldGrid=false;
                probePaper = Detail(probe, "Drawing probe paper frame", DrawingSummaryDetailGraphic.Kind.FoldedPaper);
                probePaper.transform.SetAsFirstSibling();
                probeRibbon = Detail(probe, "Drawing probe title ribbon", DrawingSummaryDetailGraphic.Kind.Ribbon);
                probeHeading = Label(probe, "Drawing probe heading", "Испытание в деле", 24, true);
                Label(probe,"Drawing probe distance caption","Дистанция",18,true);
                var viewport = (RectTransform)probe.Find("Drawing probe viewport");
                probeScenery = WorkshopUi.Graphic<Image>(viewport, "Drawing probe scenery", Rect.zero);
                probeScenery.sprite = Resources.Load<Sprite>("Art/Player/FoundationPreviewScenery");
                probeScenery.color = new Color(1, 1, 1, .36f); probeScenery.preserveAspect = true;
                probeScenery.transform.SetAsFirstSibling();
                probeViewportFrame = Frame(probe, "Drawing probe viewport frame");
                probeViewportFrame.Fill = Color.clear; probeViewportFrame.Radius = 5;
                StyleProbeButton("Drawing probe nearer", false);
                StyleProbeButton("Drawing probe farther", false);
                StyleProbeButton("Drawing probe formation", false);
                StyleProbeButton("Drawing probe fire", true);
                probe.Find("Drawing probe fire").GetComponentInChildren<Text>().text = "Попробовать";
            }
            Reflow(width, height);
        }

        internal void Reflow(float availableWidth, float availableHeight)
        {
            width = Mathf.Max(300, availableWidth); height = Mathf.Max(540, availableHeight);
            topHeight = Mathf.Clamp(height * .60f, 354, 430);
            WorkshopUi.Place(paper.rectTransform, new Rect(0, 0, width, topHeight));
            float plateWidth = Mathf.Min(width - 44, Mathf.Max(220, width * .62f)), plateX = (width - plateWidth) * .5f;
            WorkshopUi.Place(ribbon.rectTransform, new Rect(plateX, -2, plateWidth, 43));
            WorkshopUi.Place(title.rectTransform, new Rect(plateX + 12, 0, plateWidth - 24, 43));
            title.font = ui.HeadingFont; title.fontSize = 28; title.alignment = TextAnchor.MiddleCenter;
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 20; title.resizeTextMaxSize = 28;
            Place(summary, "Drawing summary ink icon", new Rect(19, 53, 30, 32));
            WorkshopUi.Place(ink.rectTransform, new Rect(60, 47, width - 78, 41));
            ink.font = ui.HeadingFont; ink.fontSize = 24; ink.resizeTextForBestFit = true;
            ink.resizeTextMinSize = 18; ink.resizeTextMaxSize = 24;
            WorkshopUi.Place(track, new Rect(20, 91, width - 40, 22));
            WorkshopUi.Place(fill, new Rect(2, 2, (width - 44) * inkHatching.Fraction, 18));
            WorkshopUi.Place(inkBorder.rectTransform, new Rect(0, 0, width - 40, 22));
            WorkshopUi.Place(inkHatching.rectTransform, new Rect(0, 0, width - 40, 22));
            float rowHeight = (topHeight - 164) / 6;
            bool compact = width < 390;
            float barX = compact ? width * .47f : width * .46f;
            float barWidth = compact ? width * .23f : width * .28f;
            float valueX = barX + barWidth + 8;
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i]; float y = 121 + i * rowHeight;
                WorkshopUi.Place(row.Icon.rectTransform, new Rect(20, y + (rowHeight - 29) * .5f, 29, 29));
                WorkshopUi.Place(row.Label.rectTransform, new Rect(60, y - 3, barX - 66, rowHeight + 6));
                row.Label.resizeTextMinSize = compact ? 15 : 17;
                row.Label.resizeTextMaxSize = compact ? 20 : 22;
                WorkshopUi.Place(row.Value.rectTransform, new Rect(valueX, y - 3, width - valueX - 23, rowHeight + 6));
                row.Value.resizeTextMinSize = compact ? 15 : 17;
                row.Value.resizeTextMaxSize = compact ? 20 : 24;
                if (i != 2)
                {
                    WorkshopUi.Place(row.Accent.rectTransform, new Rect(barX, y + rowHeight * .29f, barWidth, rowHeight * .43f));
                }
                else WorkshopUi.Place(row.Value.rectTransform, new Rect(barX, y - 3, width - barX - 23, rowHeight + 6));
            }
            if(rangeChange.gameObject.activeSelf)
            {
                float y=121+4*rowHeight;
                WorkshopUi.Place(rows[4].Accent.rectTransform,new Rect(barX,y+rowHeight*.29f,Mathf.Max(30,barWidth-49),rowHeight*.43f));
                WorkshopUi.Place(rows[4].Value.rectTransform,new Rect(valueX-49,y-3,width-valueX-23,rowHeight+6));
                WorkshopUi.Place(rangeChangeFrame.rectTransform,new Rect(width-72,y+(rowHeight-29)*.5f,54,29));
                WorkshopUi.Place(rangeChange.rectTransform,new Rect(width-70,y+(rowHeight-29)*.5f,50,29));
            }
            WorkshopUi.Place(separator.rectTransform, new Rect(19, topHeight - 42, width - 49, 2));
            WorkshopUi.Place((RectTransform)detailToggle.transform, new Rect(15, topHeight - 40, width - 50, 34));
            Stretch(detailFrame.rectTransform);
            var caption = detailToggle.GetComponentInChildren<Text>();
            caption.font = ui.HeadingFont; caption.resizeTextMinSize = 18; caption.resizeTextMaxSize = 23;
            caption.alignment = TextAnchor.MiddleLeft;
            float lowerY = topHeight + 11, lowerHeight = height - lowerY;
            WorkshopUi.Place(details, new Rect(0, lowerY, width, lowerHeight));
            LayoutDetails();
            if (probe != null) ReflowProbe(lowerY, lowerHeight);
        }

        internal void Refresh(TowerPreview? preview, FreeformTowerDesign design, int mount, FreeformRules rules = null,TowerPreview? previous=null)
        {
            if (design != null)
            {
                float fraction = Mathf.Clamp01(design.InkUsed / Mathf.Max(.01f, design.InkBudget));
                WorkshopUi.Place(fill, new Rect(2, 2, (width - 44) * fraction, 18));
                inkHatching.Fraction = fraction;
                inkHatching.color = design.IsWithinBudget ? WorkshopUi.Blue : WorkshopUi.Warning;
            }
            var weapon = preview.HasValue ? DesignChangeFeedback.FindMount(preview.Value, mount) : null;
            rows[0].Value.text = preview.HasValue ? preview.Value.Health.ToString("0.#") : "—";
            rows[1].Value.text = preview.HasValue ? preview.Value.Load + " / " + preview.Value.Capacity : "—";
            rows[2].Value.text = preview.HasValue ? preview.Value.Cost + " штр." : "—";
            rows[3].Value.text = weapon.HasValue ? weapon.Value.Damage.ToString("0.##") : "—";
            rows[4].Value.text = weapon.HasValue ? weapon.Value.Range.ToString("0.##") : "—";
            rows[5].Value.text = weapon.HasValue ? weapon.Value.Interval.ToString("0.##") + " с" : "—";
            var before=previous.HasValue?DesignChangeFeedback.FindMount(previous.Value,mount):null;
            float delta=weapon.HasValue&&before.HasValue?weapon.Value.Range-before.Value.Range:0;
            bool showChange=preview.HasValue&&preview.Value.Valid&&Mathf.Abs(delta)>.05f;
            rangeChange.text=delta.ToString("+0.0;-0.0");
            rangeChange.color=rangeChangeFrame.color=delta>=0?WorkshopUi.Success:WorkshopUi.Warning;
            rangeChange.gameObject.SetActive(showChange);rangeChangeFrame.gameObject.SetActive(showChange);
            rules = rules ?? FreeformRules.Default;
            var barrel = design?.GetBarrel(mount);
            rows[0].Accent.Fraction = preview.HasValue && preview.Value.Health > 0 && design != null
                ? design.HealthMultiplier / Mathf.Max(.01f, rules.HealthBase + rules.HealthSize) : 0;
            rows[1].Accent.Fraction = preview.HasValue && preview.Value.Capacity > 0
                ? Mathf.Clamp01((float)preview.Value.Load / preview.Value.Capacity) : 0;
            rows[1].Accent.color = preview.HasValue && preview.Value.Load > preview.Value.Capacity ? WorkshopUi.Warning : MetricColors[1];
            rows[3].Accent.Fraction = weapon.HasValue && weapon.Value.Damage > 0 && barrel != null
                ? barrel.DamageMultiplier / Mathf.Max(.01f, rules.DamageBase + rules.DamageWidth) : 0;
            rows[4].Accent.Fraction = weapon.HasValue && weapon.Value.Range > 0 && barrel != null
                ? barrel.RangeMultiplier / Mathf.Max(.01f, rules.RangeBase + rules.RangeLength) : 0;
            rows[5].Accent.Fraction = weapon.HasValue && weapon.Value.Interval > 0 && barrel != null
                ? barrel.IntervalMultiplier / Mathf.Max(.01f, rules.IntervalBase + rules.IntervalLength + rules.IntervalWidth) : 0;
            detailFrame.Selected = details.gameObject.activeSelf;
            Reflow(width,height);
        }

        void ReflowProbe(float y, float probeHeight)
        {
            WorkshopUi.Place(probe, new Rect(0, y, width, probeHeight));
            WorkshopUi.Place(probePaper.rectTransform, new Rect(0, 0, width, probeHeight));
            bool compact = width < 390;
            float formationWidth = compact ? 112 : 157, titleWidth = width - formationWidth - 45;
            WorkshopUi.Place(probeRibbon.rectTransform, new Rect(5, 0, titleWidth + 20, 34));
            WorkshopUi.Place(probeHeading.rectTransform, new Rect(17, -2, titleWidth, 38));
            probeHeading.fontSize = compact ? 18 : 24;
            probeHeading.resizeTextMinSize = compact ? 15 : 17;
            probeHeading.resizeTextMaxSize = compact ? 18 : 24;
            Place(probe, "Drawing probe formation", new Rect(width - formationWidth - 17, 7, formationWidth, 28));
            Text conditions = probe.Find("Drawing probe conditions").GetComponent<Text>();
            conditions.enabled=false;
            WorkshopUi.Place(conditions.rectTransform, new Rect(16, 38, width - 32, 35));
            conditions.fontSize = 13; conditions.resizeTextForBestFit = true;
            conditions.resizeTextMinSize = 12; conditions.resizeTextMaxSize = 13;
            conditions.color = WorkshopUi.Muted;
            float viewHeight = Mathf.Max(60, probeHeight - 156);
            var view = new Rect(14, 44, width - 28, viewHeight);
            Place(probe, "Drawing probe viewport", view);
            Place(probe, "Drawing probe viewport/Drawing probe sketch", new Rect(0, 0, view.width, view.height));
            WorkshopUi.Place(probeViewportFrame.rectTransform, view);
            CenterImage(probeScenery.rectTransform, new Rect(0, 0, view.width, view.height));
            Text result = probe.Find("Drawing probe result").GetComponent<Text>();
            WorkshopUi.Place(result.rectTransform, new Rect(16, 47 + viewHeight, width - 32, 58));
            result.fontSize = 14; result.resizeTextForBestFit = true; result.resizeTextMinSize = 12; result.resizeTextMaxSize = 14;
            float controlY = probeHeight - 45, distanceWidth = width * .56f;
            Place(probe,"Drawing probe distance caption",new Rect(15,controlY,distanceWidth-111,34));
            var distanceCaption=probe.Find("Drawing probe distance caption").GetComponent<Text>();
            distanceCaption.resizeTextMinSize=11;distanceCaption.resizeTextMaxSize=18;
            Place(probe, "Drawing probe nearer", new Rect(distanceWidth-96, controlY, 28, 34));
            Place(probe, "Drawing probe distance", new Rect(distanceWidth-65, controlY, 31, 34));
            Place(probe, "Drawing probe farther", new Rect(distanceWidth - 32, controlY, 31, 34));
            Place(probe, "Drawing probe fire", new Rect(distanceWidth + 9, controlY - 2, width - distanceWidth - 24, 38));
            var distance = probe.Find("Drawing probe distance").GetComponent<Text>();
            distance.fontSize = 14; distance.resizeTextForBestFit = true; distance.resizeTextMinSize = 12; distance.resizeTextMaxSize = 14;
            foreach (var button in probe.GetComponentsInChildren<Button>(true))
            {
                var frame = button.transform.Find("Drawing probe control frame").GetComponent<FoundationDetailGraphic>();
                Stretch(frame.rectTransform);
                var label = button.GetComponentInChildren<Text>();
                Stretch(label.rectTransform, 5, 1); label.font = ui.HeadingFont;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = compact && button.name == "Drawing probe formation" ? 10 : 13;
                label.resizeTextMaxSize = button.name == "Drawing probe fire" ? 22 : compact && button.name == "Drawing probe formation" ? 14 : 18;
            }
        }

        void LayoutDetails()
        {
            float contentWidth = width - 38;
            WorkshopUi.Place(detailText.rectTransform, new Rect(17, 12, contentWidth, Mathf.Max(350, detailText.preferredHeight + 18)));
            detailText.fontSize = 16;
            float contentHeight = Mathf.Max(details.rect.height, detailText.preferredHeight + 30);
            detailText.rectTransform.sizeDelta = new Vector2(contentWidth, contentHeight - 24);
            detailScroll.content.sizeDelta = new Vector2(0, contentHeight);
        }

        void StyleProbeButton(string name, bool primary)
        {
            Button button = probe.Find(name).GetComponent<Button>();
            var frame = Frame(button.transform, "Drawing probe control frame");
            frame.Fill = Color.clear; frame.Radius = 5; frame.transform.SetAsFirstSibling();
            button.GetComponent<Image>().color = primary ? WorkshopUi.Blue : WorkshopUi.Paper;
            var colors = button.colors;
            colors.highlightedColor = colors.selectedColor = primary ? new Color(.78f, .85f, 1) : WorkshopUi.Selected;
            colors.pressedColor = primary ? new Color(.62f, .72f, 1) : new Color(.74f, .84f, .97f);
            colors.disabledColor = new Color(.73f, .76f, .81f, .8f); button.colors = colors;
            if (primary) frame.color = WorkshopUi.Ink;
            Text label = button.GetComponentInChildren<Text>(); label.color = primary ? WorkshopUi.Paper : WorkshopUi.Ink;
        }

        Text Label(Transform parent, string name, string text, int size, bool heading)
        {
            Text label = ui.Label(parent, name, text, Rect.zero, size, heading);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 17; label.resizeTextMaxSize = size;
            return label;
        }
        static DrawingSummaryDetailGraphic Detail(Transform owner, string name, DrawingSummaryDetailGraphic.Kind kind)
        {
            var graphic = WorkshopUi.Graphic<DrawingSummaryDetailGraphic>(owner, name, Rect.zero);
            graphic.Shape = kind; graphic.color = WorkshopUi.Blue; return graphic;
        }
        static FoundationDetailGraphic Frame(Transform parent, string name)
        {
            var graphic = WorkshopUi.Graphic<FoundationDetailGraphic>(parent, name, Rect.zero);
            graphic.Shape = FoundationDetailGraphic.Kind.Frame; graphic.color = WorkshopUi.Ink; graphic.StrokeWidth = 1.05f;
            return graphic;
        }
        static void Place(Transform parent, string name, Rect bounds)
        { var rect = parent.Find(name) as RectTransform; if (rect != null) WorkshopUi.Place(rect, bounds); }
        static void Stretch(RectTransform rect, float horizontal = 0, float vertical = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical); rect.offsetMax = new Vector2(-horizontal, -vertical);
        }
        static void CenterImage(RectTransform rect, Rect bounds)
        {
            WorkshopUi.Place(rect, bounds); rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(bounds.center.x, -bounds.center.y);
        }
    }

    /// <summary>Paper folds, title ribbons and calibrated load segments for the drawing readout.</summary>
    public sealed class DrawingSummaryDetailGraphic : MaskableGraphic
    {
        public enum Kind { FoldedPaper, Ribbon, InkAccent, InkHatching, LoadSegments, Clock }
        Kind shape;
        float fraction;
        readonly MeshPen pen = new MeshPen(null, 1536);
        public Kind Shape { get => shape; set { shape = value; SetVerticesDirty(); } }
        public float Fraction { get => fraction; set { fraction = Mathf.Clamp01(value); SetVerticesDirty(); } }
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); pen.Clear(); Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            switch (shape)
            {
                case Kind.FoldedPaper: Paper(r); break;
                case Kind.Ribbon: Ribbon(r); break;
                case Kind.InkAccent:
                    pen.Line(new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y + .5f), 1.6f, MeshPen.Alpha(color, .75f));
                    pen.Line(new Vector2(r.xMin + 3, r.center.y - 1), new Vector2(r.xMax - 2, r.center.y - 1), .6f, MeshPen.Alpha(color, .35f));
                    break;
                case Kind.InkHatching:
                    float end = r.xMin + 2 + (r.width - 4) * fraction;
                    for (float x = r.xMin + 5; x < end - 7; x += 6)
                        pen.Line(new Vector2(x, r.yMin + 5), new Vector2(Mathf.Min(end - 3, x + 9), r.yMax - 5), .75f, new Color(1, 1, 1, .28f));
                    break;
                case Kind.LoadSegments: Segments(r); break;
                case Kind.Clock:
                    float radius = Mathf.Min(r.width, r.height) * .40f;
                    pen.Disc(r.center, radius, WorkshopUi.Paper, 28);
                    pen.Ring(r.center, radius, 1.6f, color, 32);
                    pen.Line(r.center, r.center + Vector2.up * radius * .65f, 1.8f, color);
                    pen.Line(r.center, r.center + Vector2.right * radius * .49f, 1.8f, color);
                    pen.Disc(r.center, 1.5f, color, 12);
                    break;
            }
            pen.CopyTo(vh, Vector2.zero, 1);
        }

        void Paper(Rect r)
        {
            const float margin = 2, fold = 25;
            Vector2[] contour =
            {
                new Vector2(r.xMin + margin + 5, r.yMax - margin), new Vector2(r.xMax - margin - 5, r.yMax - margin),
                new Vector2(r.xMax - margin, r.yMax - margin - 5), new Vector2(r.xMax - margin, r.yMin + fold),
                new Vector2(r.xMax - fold, r.yMin + margin), new Vector2(r.xMin + margin + 5, r.yMin + margin),
                new Vector2(r.xMin + margin, r.yMin + margin + 5), new Vector2(r.xMin + margin, r.yMax - margin - 5)
            };
            Polygon(contour, WorkshopUi.Paper, WorkshopUi.Ink, 1.25f);
            Color grid = new Color(.39f, .57f, .75f, .055f);
            for (float x = r.xMin + 20; x < r.xMax - fold; x += 22)
                pen.Line(new Vector2(x, r.yMin + 6), new Vector2(x, r.yMax - 6), .65f, grid);
            for (float y = r.yMin + 20; y < r.yMax - 6; y += 22)
                pen.Line(new Vector2(r.xMin + 6, y), new Vector2(r.xMax - fold, y), .65f, grid);
            Color fine = MeshPen.Alpha(WorkshopUi.Blue, .35f);
            pen.Line(new Vector2(r.xMin + 6, r.yMin + 12), new Vector2(r.xMin + 6, r.yMax - 9), .65f, fine);
            pen.Line(new Vector2(r.xMin + 11, r.yMin + 6), new Vector2(r.xMax - fold - 4, r.yMin + 6), .65f, fine);
            pen.Line(new Vector2(r.xMin + 10, r.yMax - 6), new Vector2(r.xMax - 10, r.yMax - 6), .65f, fine);
            Vector2 corner = new Vector2(r.xMax - fold, r.yMin + fold);
            Vector2 bottom = new Vector2(r.xMax - fold, r.yMin + margin);
            Vector2 right = new Vector2(r.xMax - margin, r.yMin + fold);
            pen.Triangle(corner, bottom, right, new Color32(218, 225, 226, 255));
            pen.Line(bottom, corner, 1, WorkshopUi.Ink); pen.Line(corner, right, 1, WorkshopUi.Ink);
            for (int i = 0; i < 3; i++)
                pen.Line(bottom + new Vector2(3 + i * 3, 5 + i * 4), right + new Vector2(-5 - i * 4, -3 - i * 3), .6f, fine);
        }

        void Ribbon(Rect r)
        {
            Vector2[] outline =
            {
                new Vector2(r.xMin + 8, r.yMax - 2), new Vector2(r.xMax - 8, r.yMax - 2),
                new Vector2(r.xMax - 2, r.yMax - 5), new Vector2(r.xMax - 9, r.center.y),
                new Vector2(r.xMax - 2, r.yMin + 4), new Vector2(r.xMax - 8, r.yMin + 2),
                new Vector2(r.xMin + 8, r.yMin + 2), new Vector2(r.xMin + 2, r.yMin + 4),
                new Vector2(r.xMin + 9, r.center.y), new Vector2(r.xMin + 2, r.yMax - 5)
            };
            Polygon(outline, WorkshopUi.Paper, WorkshopUi.Ink, 1.1f);
            pen.Line(new Vector2(r.xMin + 12, r.yMin + 5), new Vector2(r.xMax - 12, r.yMin + 5), .65f, MeshPen.Alpha(WorkshopUi.Blue, .32f));
        }

        void Segments(Rect r)
        {
            const int count = 7;
            float cell = (r.width - 2) / count;
            for (int i = 0; i < count; i++)
            {
                var a = new Vector2(r.xMin + 1 + cell * i, r.yMin + 1);
                float occupied = Mathf.Clamp01(fraction * count - i), w = cell - 1.5f;
                pen.Rect(a.x, a.y, w, r.height - 2, new Color32(228, 231, 233, 255));
                if (occupied > 0)
                {
                    pen.Rect(a.x, a.y, w * occupied, r.height - 2, color);
                    pen.Line(a + new Vector2(1, 2), a + new Vector2(Mathf.Max(1, w * occupied - 1), r.height - 4), .65f, new Color(1, 1, 1, .5f));
                }
                pen.Line(a, a + new Vector2(w, 0), .7f, MeshPen.Alpha(WorkshopUi.Ink, .55f));
                pen.Line(a, a + new Vector2(0, r.height - 2), .7f, MeshPen.Alpha(WorkshopUi.Ink, .55f));
                pen.Line(a + new Vector2(0, r.height - 2), a + new Vector2(w, r.height - 2), .7f, MeshPen.Alpha(WorkshopUi.Ink, .55f));
                pen.Line(a + new Vector2(w, 0), a + new Vector2(w, r.height - 2), .7f, MeshPen.Alpha(WorkshopUi.Ink, .55f));
            }
        }

        void Polygon(Vector2[] points, Color fill, Color ink, float weight)
        {
            Vector2 center = rectTransform.rect.center, previous = points[points.Length - 1];
            foreach (Vector2 next in points)
            { pen.Triangle(center, previous, next, fill); pen.Line(previous, next, weight, ink); previous = next; }
        }
    }
}
