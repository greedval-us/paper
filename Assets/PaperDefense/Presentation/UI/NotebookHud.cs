using System;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed class NotebookHud : MonoBehaviour
    {
        const int WallToolIndex = ProfileLimits.LoadoutSlotCount, SpikesToolIndex = WallToolIndex + 1;
        const int EraseToolIndex = SpikesToolIndex + 1, UpgradeToolIndex = EraseToolIndex + 1;
        const int BlotterToolIndex = UpgradeToolIndex + 1, PaintToolIndex = BlotterToolIndex + 1;
        static readonly Color Navy = new Color(.09f, .16f, .30f);
        static readonly Color Paper = new Color(.98f, .96f, .87f);
        static readonly Color Red = new Color(.78f, .15f, .17f);
        static readonly Color Blue = new Color(.13f, .36f, .69f);
        static readonly Color Purple = new Color(.53f, .19f, .66f);
        RunController run;
        DrawingInput input;
        Font font;
        GameObject canvasObject;
        RectTransform root;
        Text hp, ink, wave, threats, forecast, message, toolHint, launchText, pauseText, speedText, progress, inspect, blotterPrice;
        Image hpFill;
        Button launch;
        readonly Button[] tools = new Button[PaintToolIndex + 1];
        readonly Text[] toolPrices = new Text[SpikesToolIndex + 1], toolNames = new Text[PaintToolIndex + 1], wellLabels = new Text[3], colorLabels = new Text[4];
        readonly Button[] wellActions = new Button[3], colors = new Button[4];
        readonly Button[] tiers = new Button[3];
        GameObject upgradePanel, outcomePanel, pauseBadge;
        Text outcomeTitle, outcomeBody;
        float refresh;

        public void Initialize(RunController controller, DrawingInput drawing)
        {
            run = controller; input = drawing;
            font = Resources.Load<Font>("NotebookHand") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Notebook HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var board = new GameObject("Notebook layout 1600x1000",typeof(RectTransform)); board.transform.SetParent(canvasObject.transform,false);
            root=board.GetComponent<RectTransform>();root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);root.pivot=new Vector2(.5f,.5f);
            root.sizeDelta=new Vector2(1600,1000);
            BuildHeader(); BuildNotes(); BuildTools(); BuildDialogs();
            input.SelectionChanged += RefreshSelection;
            RefreshSelection(); Refresh();
        }

        void OnDestroy() { if (input != null) input.SelectionChanged -= RefreshSelection; }
        void Update()
        {
            if (run == null) return;
            canvasObject.SetActive(!run.InWorkshop);
            if (run.InWorkshop) return;
            refresh -= Time.unscaledDeltaTime; if (refresh <= 0) { refresh = .08f; Refresh(); }
        }

        void BuildHeader()
        {
            var header = Panel(root, "Header", new Rect(24, 18, 1552, 122), Paper);
            Label(header, "ДЕРЖИ ЛИНИЮ!", new Rect(22, 9, 380, 53), 38, Navy, FontStyle.Bold);
            Label(header, "НАРИСУЙ ОБОРОНУ. ПЕРЕЖИВИ ОСАДУ.", new Rect(26, 68, 385, 28), 15, Navy);
            hp = Label(header, "БАЗА  100 / 100", new Rect(432, 13, 205, 40), 23, Navy, FontStyle.Bold);
            var bar = Panel(header, "Health", new Rect(437, 66, 180, 16), new Color(.86f,.85f,.75f), false);
            hpFill = Fill(bar, "Health ink", new Color(.30f,.57f,.34f));
            ink = Label(header, "420", new Rect(665, 8, 160, 49), 36, Blue, FontStyle.Bold);
            Label(header, "ШТРИХИ  /  запас", new Rect(667, 65, 183, 27), 17, Navy);
            wave = Label(header, "ЛИСТ 01", new Rect(867, 11, 242, 43), 25, Navy, FontStyle.Bold);
            progress = Label(header, "подготовка", new Rect(869, 63, 228, 29), 16, Navy);
            launch = MakeButton(header, "Launch wave", new Rect(1122, 15, 255, 75), "НАЧАТЬ ВОЛНУ  →", 21, Blue,
                () => run.NextWave(), out launchText);
            MakeButton(header, "Pause", new Rect(1392, 15, 136, 33), "ПАУЗА  [Space]", 13, Navy,
                () => run.TogglePause(), out pauseText);
            MakeButton(header, "Speed", new Rect(1392, 58, 136, 33), "ТЕМП ×1  [F]", 13, Navy,
                () => run.ToggleSpeed(), out speedText);
        }

        void BuildNotes()
        {
            var note = Panel(root, "Field notes", new Rect(35, 166, 232, 390), new Color(1,.98f,.86f));
            note.localEulerAngles = new Vector3(0,0,2);
            Label(note, "ЧЕРНИЛЬНИЦЫ", new Rect(14,12,210,35), 23, Navy, FontStyle.Bold);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                wellLabels[i] = Label(note, "", new Rect(14, 56 + i * 98, 203, 44), 16, Navy);
                wellActions[i] = MakeButton(note, "Activate well " + i, new Rect(14, 103 + i * 98, 137, 31), "ЗАХВАТИТЬ", 13, Blue,
                    () => { if (index < run.Session.Wells.Count) input.SetMessage(run.ActivateWell(run.Session.Wells[index].Id, out var reason) ? "Чернильница активна. Защити её до конца волны для цветной порции." : reason); }, out _);
                MakeButton(note, "Focus well " + i, new Rect(157, 103 + i * 98, 60, 31), "→", 18, Navy,
                    () => { if (index < run.Session.Wells.Count) input.Focus(run.Session.Wells[index].Cell); }, out _);
            }
            MakeButton(note, "Focus base", new Rect(14, 351, 203, 28), "К БАЗЕ  →", 15, Navy, () => input.Focus(run.Session.BaseCell), out _);
            Label(root, "Цвет — за окраску,\nне за каждую пулю.\n\nКолёсико — ближе\nСКМ — двигать лист", new Rect(43,574,225,140), 17, Blue);
            var danger = Panel(root, "Forecast", new Rect(1335,170,231,222), new Color(1,.95f,.74f));
            danger.localEulerAngles = new Vector3(0,0,-2);
            Label(danger, "СЛЕДОМ ИДУТ…", new Rect(13,8,215,35), 22, Red, FontStyle.Bold);
            forecast = Label(danger, "", new Rect(14,53,203,150), 18, Navy);
            threats = Label(root, "НА ЛИСТЕ\n0", new Rect(1355,430,210,104), 29, Red, FontStyle.Bold);
            inspect = Label(root, "Наведи на орудие.\n[P] — приоритет\n[Tab] — радиус", new Rect(1355,571,213,116), 17, Navy);
            pauseBadge = Panel(root, "Paused badge", new Rect(580,158,440,54), new Color(1,.96f,.72f)).gameObject;
            Label(pauseBadge.transform, "ПАУЗА — можно чертить", new Rect(15,5,410,42), 24, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
            pauseBadge.SetActive(false);
        }

        void BuildTools()
        {
            var footer = Panel(root, "Drawing desk", new Rect(24,794,1552,188), Paper);
            string[] materialNames = { "Q  КАРАНДАШ", "РУЧКА", "ФЛОМАСТЕР" };
            Color[] materialColors = { new Color(.61f,.39f,.12f), Blue, Purple };
            for (int i = 0; i < 3; i++)
            { int index = i; tiers[i] = MakeButton(footer, "Material " + i, new Rect(18+i*158,8,148,34), materialNames[i], 15,
                materialColors[i], () => input.SelectTier((MaterialTier)index), out _); }
            string[] colorNames = { "БЕЗ ЦВЕТА", "КРАСНЫЕ", "СИНИЕ", "ЗЕЛЁНЫЕ" };
            Color[] colorInks = { Navy, Red, Blue, NotebookPalette.Green };
            for (int i = 0; i < 4; i++)
            { int index = i; colors[i] = MakeButton(footer, "Ink color " + i, new Rect(500+i*158,8,148,34), colorNames[i], 14,
                colorInks[i], () => input.SelectColor((InkColor)index), out colorLabels[i]); }
            MakeButton(footer, "Restart", new Rect(1152,8,179,34), "ЗАНОВО  [R]", 15, Navy, () => run.Restart(), out _);
            MakeButton(footer, "Open workshop", new Rect(1342,8,188,34), "В МАСТЕРСКУЮ", 15, Navy, () => run.OpenWorkshop(), out _);
            string[] names = { "1 ЧЕРТЁЖ", "2 ЧЕРТЁЖ", "3 ЧЕРТЁЖ", "4 ЧЕРТЁЖ", "5 СТЕНА", "6 ШИПЫ", "E ЛАСТИК", "U ОБВЕСТИ", "B ПРОМОКАШКА", "C ОКРАСИТЬ" };
            string[] descriptions = { "выбран в мастерской", "выбран в мастерской", "выбран в мастерской", "выбран в мастерской", "рисуй линией", "штрихуй землю", "вернуть часть", "следующий материал", "очистить участок", "выбранный цвет" };
            for (int i = 0; i < tools.Length; i++)
            {
                int index = i;
                tools[i] = MakeButton(footer, "Tool " + i, new Rect(18 + i*152,54,143,91), "", 17, Navy, () => {
                    if (index < ProfileLimits.LoadoutSlotCount) input.SelectSlot(index);
                    else if (index == WallToolIndex) input.SelectTool(StructureKind.Wall);
                    else if (index == SpikesToolIndex) input.SelectTool(StructureKind.Spikes);
                    else input.SelectMode(index == EraseToolIndex ? DrawingMode.Erase : index == UpgradeToolIndex ? DrawingMode.Upgrade : index == BlotterToolIndex ? DrawingMode.Blotter : DrawingMode.Paint);
                }, out _);
                toolNames[i] = Label(tools[i].transform, names[i], new Rect(5,1,133,33), 14, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
                Label(tools[i].transform, descriptions[i], new Rect(5,36,133,23), 12, Navy, FontStyle.Normal, TextAnchor.MiddleCenter);
                var price = Label(tools[i].transform, "", new Rect(5,62,133,24), 14, Blue, FontStyle.Normal, TextAnchor.MiddleCenter);
                if (i < toolPrices.Length) toolPrices[i] = price; else if (i == BlotterToolIndex) blotterPrice = price; else price.text = i == EraseToolIndex ? "с учётом износа" : i == UpgradeToolIndex ? "доплата разницы" : "один картридж";
            }
            toolHint = Label(footer, "", new Rect(20,151,1230,28), 16, Navy);
            Label(footer, "ПКМ / Esc — отменить", new Rect(1262,151,273,28), 16, Navy, FontStyle.Normal, TextAnchor.MiddleRight);
            message = Label(root, "", new Rect(270,748,1055,34), 19, Blue, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void BuildDialogs()
        {
            upgradePanel = Panel(root, "Upgrade choices", new Rect(300,280,1000,340), Paper).gameObject;
            upgradePanel.GetComponent<Image>().raycastTarget=true;
            Label(upgradePanel.transform, "НОВЫЙ ПРИЁМ", new Rect(30,18,940,49), 34, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
            Label(upgradePanel.transform, "Выбери, как изменится твоя оборона", new Rect(30,75,940,36), 22, Navy, FontStyle.Normal, TextAnchor.MiddleCenter);
            string[] titles = { "БЫСТРЫЙ ШТРИХ", "ГУСТЫЕ ЧЕРНИЛА", "ЧЕРНОВАЯ РАБОТА" };
            string[] bodies = { "Орудия стреляют\nчаще", "Постройки прочнее,\nурон выше", "Больше возврат\nпри стирании" };
            for (int i=0; i<3; i++)
            {
                int choice = i;
                var b = MakeButton(upgradePanel.transform, "Upgrade " + i, new Rect(32+i*322,137,292,164), "", 20, Blue,
                    () => {run.ChooseUpgrade((RunUpgrade)choice); input.SetMessage("Новый приём освоен. Готовь следующий рубеж!");}, out _);
                Label(b.transform, titles[i], new Rect(12,14,268,43), 21, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
                Label(b.transform, bodies[i], new Rect(12,64,268,83), 21, Navy, FontStyle.Normal, TextAnchor.MiddleCenter);
            }
            upgradePanel.SetActive(false);
            outcomePanel = Panel(root, "Run result", new Rect(420,260,760,420), Paper).gameObject;
            outcomePanel.GetComponent<Image>().raycastTarget=true;
            outcomeTitle = Label(outcomePanel.transform, "", new Rect(25,25,710,65), 42, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
            outcomeBody = Label(outcomePanel.transform, "", new Rect(25,100,710,222), 19, Navy, FontStyle.Normal, TextAnchor.MiddleCenter);
            MakeButton(outcomePanel.transform, "Try another defense", new Rect(140,340,480,61), "В МАСТЕРСКУЮ  →", 22, Blue, () => run.OpenWorkshop(), out _);
            outcomePanel.SetActive(false);
        }

        void RefreshSelection()
        {
            if (run == null || run.Session == null) return;
            var loadout = run.Workshop.Loadout;
            blotterPrice.text = run.Session.Balance.BlotterCost + " штр. / R " + run.Session.Balance.BlotterRadius.ToString("0");
            for (int i=0;i<tools.Length;i++)
            {
                bool selected = i < ProfileLimits.LoadoutSlotCount ? input.Mode == DrawingMode.Build && input.BlueprintSlot == i :
                    i == WallToolIndex ? input.Mode == DrawingMode.Build && input.BlueprintSlot < 0 && input.Tool == StructureKind.Wall :
                    i == SpikesToolIndex ? input.Mode == DrawingMode.Build && input.BlueprintSlot < 0 && input.Tool == StructureKind.Spikes :
                    i == EraseToolIndex ? input.Mode == DrawingMode.Erase : i == UpgradeToolIndex ? input.Mode == DrawingMode.Upgrade : i == BlotterToolIndex ? input.Mode == DrawingMode.Blotter : input.Mode == DrawingMode.Paint;
                tools[i].GetComponent<Image>().color = selected ? new Color(.78f,.87f,.95f) : Paper;
            }
            for (int i=0;i<3;i++) tiers[i].GetComponent<Image>().color = (int)input.Tier == i ? new Color(.89f,.84f,.98f) : Paper;
            string[] colorNames = { "БЕЗ ЦВЕТА", "КРАСНЫЕ", "СИНИЕ", "ЗЕЛЁНЫЕ" };
            for (int i = 0; i < colors.Length; i++)
            { colors[i].GetComponent<Image>().color = (int)input.Color == i ? new Color(.82f,.9f,.84f) : Paper; colorLabels[i].text = colorNames[i] + (i == 0 ? "" : " " + run.Session.ColoredInk((InkColor)i)); }
            for (int i=0;i<toolPrices.Length;i++)
            {
                if (i < ProfileLimits.LoadoutSlotCount)
                {
                    if (i >= loadout.Count) continue;
                    var bp = loadout[i]; var preview = run.PreviewBlueprint(bp, input.Tier);
                    toolNames[i].text = (i + 1) + " " + bp.Name;
                    toolPrices[i].text = preview.Cost + " штр." + (input.Color == InkColor.None ? "" : " + " + preview.ColorCost + " цв.");
                }
                else
                {
                    int cost = run.Session.Balance.ToolCost(i == WallToolIndex ? StructureKind.Wall : StructureKind.Spikes, input.Tier);
                    toolPrices[i].text = cost + " штр. / клетка";
                }
            }
        }

        void Refresh()
        {
            if (run == null || run.Session == null || hp == null) return;
            var s = run.Session;
            for (int i = 0; i < wellLabels.Length; i++)
            {
                bool exists = i < s.Wells.Count;
                wellLabels[i].gameObject.SetActive(exists); wellActions[i].gameObject.SetActive(exists);
                if (!exists) continue;
                var well = s.Wells[i];
                wellLabels[i].text = WorkshopHud.ColorName(well.Color).ToUpperInvariant() + "  " + Mathf.CeilToInt(well.Health) + " / " + Mathf.CeilToInt(well.MaxHealth) +
                    "\n" + (well.Threatened ? "ПОД АТАКОЙ!" : well.Active ? "+" + s.Balance.WellInkPerWave + " за волну" : "Захват: " + s.Balance.WellActivationCost + " штр.");
                wellLabels[i].color = well.Threatened ? Red : Navy;
                wellActions[i].interactable = !well.Active;
            }
            hp.text = "БАЗА  " + Mathf.CeilToInt(s.BaseHealth) + " / " + Mathf.CeilToInt(s.Balance.BaseHealth);
            hpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s.BaseHealth / s.Balance.BaseHealth),1);
            ink.text = s.Ink.ToString("N0");
            wave.text = "ВОЛНА " + s.WaveIndex.ToString("00") + " / " + s.Balance.Waves.Count.ToString("00");
            progress.text = s.Phase == RunPhase.Assault ? "в пути ещё " + s.RemainingToSpawn :
                s.Phase == RunPhase.Victory ? "лист удержан" : s.Phase == RunPhase.Defeat ? "попробуем иначе" :
                "передышка: " + Mathf.CeilToInt(s.Countdown) + " с";
            threats.text = "НА ЛИСТЕ\n" + s.Enemies.Count.ToString("N0");
            int next = s.Phase == RunPhase.Assault ? s.WaveIndex-1 : s.WaveIndex;
            if (next >= 0 && next < s.Balance.Waves.Count)
            {
                var w = s.Balance.Waves[next];
                string directions = ((w.Sides&1)!=0 ? "ЗАПАД  " : "") + ((w.Sides&2)!=0 ? "ВОСТОК  " : "") +
                    ((w.Sides&4)!=0 ? "СЕВЕР  " : "") + ((w.Sides&8)!=0 ? "ЮГ" : "");
                forecast.text = w.Title + "\n" + directions + "\n" + w.Count + " врагов\n" +
                    (w.SpitterFraction > 0 ? "Плевуны: следи за замахом!" : w.GnawerFraction>0 ? "Есть грызуны стен!" : w.RunnerFraction>0 ? "Берегись бегунов!" : "Каракули идут толпой") +
                    (w.HatchedFraction > 0 ? "\nПлотная штриховка" : w.OutlinedFraction > 0 ? "\nЕсть обведённые" : "");
            }
            else forecast.text = "Лист закончен.\nКаждый штрих\nимел значение.";
            launch.interactable = (s.Phase == RunPhase.Planning || s.Phase == RunPhase.Intermission) && !s.PendingUpgrade;
            launchText.text = s.Phase == RunPhase.Assault ? "ДЕРЖИ ЛИНИЮ!" : "НАЧАТЬ ВОЛНУ  [N]";
            pauseText.text = run.Paused ? "ПРОДОЛЖИТЬ  [Space]" : "ПАУЗА  [Space]";
            speedText.text = "ТЕМП ×" + run.Speed + "  [F]";
            pauseBadge.SetActive(run.Paused && s.Phase != RunPhase.Defeat && s.Phase != RunPhase.Victory);
            message.text = input.Message;
            toolHint.text = input.Mode == DrawingMode.Erase ? "Ластик: зажми ЛКМ над своими постройками. Возврат учитывает повреждения." :
                input.Mode == DrawingMode.Upgrade ? "Обвести: нажми на постройку, чтобы поднять материал. Плати только разницу." :
                input.Mode == DrawingMode.Blotter ? "Промокашка: очистить участок, радиус " + s.Balance.BlotterRadius.ToString("0") + ". Готовность: " + (s.BlotterCooldown > 0 ? s.BlotterCooldown.ToString("0.0") + " с" : "ГОТОВА") :
                input.Mode == DrawingMode.Paint ? "Окрашивание: нажми на башню. Один выбранный цвет; стоимость зависит от её сборки." :
                "ЛКМ — " + (input.Tool == StructureKind.Wall || input.Tool == StructureKind.Spikes ? "проведи линию" : "поставь орудие") +
                ". Чертёж: " + input.PreviewCost + " штр.   [Q] материал   [Tab] радиусы";
            if (input.Mode == DrawingMode.Build && input.PreviewReason.Length > 0)
                toolHint.text = "Чертёж: " + input.PreviewCost + " штр. — " + input.PreviewReason;
            toolHint.color = input.PreviewReason.Length > 0 ? Red : Navy;
            if (input.HasHover)
            {
                var st = s.StructureAt(input.HoverCell);
                if (st.HasValue && st.Value.WaitingForClearance)
                    inspect.text = "ЧЕРТЁЖ ОЖИДАЕТ\nВраг занимает клетку.\n[E] — отменить и вернуть часть";
                else if (st.HasValue && !st.Value.IsComplete)
                    inspect.text = "СТРОИТСЯ\nДо готовности: " + st.Value.BuildRemaining.ToString("0.0") + " с\nНа паузе стройка ждёт.";
                else if (st.HasValue && st.Value.Kind != StructureKind.Wall && st.Value.Kind != StructureKind.Spikes)
                    inspect.text = (st.Value.Blueprint != null ? st.Value.Blueprint.Name : "Орудие") + ": " + Mathf.CeilToInt(st.Value.Health) + " HP\nЦвет: " + WorkshopHud.ColorName(st.Value.Color) + "\n[P] " + (st.Value.Priority==TargetPriority.Nearest ? "ближайшие" : st.Value.Priority==TargetPriority.Strongest ? "прочные" : "грызуны");
                else inspect.text = "Наведи на орудие.\n[P] — приоритет\n[Tab] — радиус";
            }
            else inspect.text = "Наведи на орудие.\n[P] — приоритет\n[Tab] — радиус";
            upgradePanel.SetActive(s.PendingUpgrade);
            bool finished = s.Phase == RunPhase.Victory || s.Phase == RunPhase.Defeat;
            outcomePanel.SetActive(finished);
            if (finished)
            {
                outcomeTitle.text = s.Phase == RunPhase.Victory ? "ЛИНИЯ УДЕРЖАНА!" : "СТРАНИЦА НЕ СДАЁТСЯ";
                outcomeTitle.color = s.Phase == RunPhase.Victory ? Blue : Red;
                var stats = s.Statistics;
                double idle = stats.IdleSeconds(StructureKind.Gun) + stats.IdleSeconds(StructureKind.Piercer)
                            + stats.IdleSeconds(StructureKind.Mortar) + stats.IdleSeconds(StructureKind.Spikes);
                string breach = "Рубежи выдержали.";
                if (stats.Breaches.Count > 0)
                {
                    var last = stats.Breaches[stats.Breaches.Count - 1];
                    breach = "Последний пролом: " + last.Cell + " на " + last.Elapsed.ToString("0.0") + " с.";
                }
                outcomeBody.text = "Волн: " + s.WaveIndex + "   ·   Остановлено: " + s.Kills + "   ·   Пик орды: " + stats.PeakAliveEnemies +
                    "\nУрон: пулемёт " + stats.Damage(StructureKind.Gun).ToString("N0") + " / пробой " + stats.Damage(StructureKind.Piercer).ToString("N0") +
                    " / мортира " + stats.Damage(StructureKind.Mortar).ToString("N0") + " / шипы " + stats.Damage(StructureKind.Spikes).ToString("N0") +
                    "\nПростой орудий без цели: " + idle.ToString("0.0") + " с." +
                    "\nВложено: " + (stats.BuildInvestment + stats.UpgradeInvestment) + "   ·   Возвращено: " + stats.RefundedInk + "   ·   Потеряно: " + stats.LostInvestment +
                    "\n" + breach + "   Улучшений: " + stats.Upgrades.Count + "   Золото: +" + s.GoldEarned +
                    "\n" + (s.Phase == RunPhase.Victory ? "Твой рисунок выдержал финальную осаду." : "Попробуй второй рубеж или другой прострел.");
            }
            RefreshSelection();
        }

        RectTransform Panel(Transform parent, string name, Rect box, Color color, bool outline = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent,false);
            var r = go.GetComponent<RectTransform>(); Place(r,box);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            if (outline) {var o=go.AddComponent<Outline>(); o.effectColor=Navy; o.effectDistance=new Vector2(1.4f,-1.4f); o.useGraphicAlpha=false;}
            return r;
        }
        Image Fill(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>(); r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        Text Label(Transform parent, string content, Rect box, int size, Color color, FontStyle style=FontStyle.Normal, TextAnchor align=TextAnchor.UpperLeft)
        {
            var go=new GameObject("Text " + content.Replace('\n',' '),typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            Place(go.GetComponent<RectTransform>(),box);var text=go.GetComponent<Text>();text.font=font;text.text=content;text.fontSize=size;
            text.color=color;text.fontStyle=style;text.alignment=align;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        Button MakeButton(Transform parent,string name,Rect box,string title,int size,Color color,Action action,out Text text)
        {
            var r=Panel(parent,name,box,Paper);var image=r.GetComponent<Image>();image.raycastTarget=true;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
            var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.88f,.93f,1);colors.pressedColor=new Color(.68f,.80f,.94f);
            colors.disabledColor=new Color(.8f,.8f,.8f,.6f);b.colors=colors;b.onClick.AddListener(()=>action());
            var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
            text=Label(r,title,new Rect(6,3,box.width-12,box.height-6),size,color,FontStyle.Bold,TextAnchor.MiddleCenter);return b;
        }
        static void Place(RectTransform r,Rect box)
        {r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(box.x,-box.y);r.sizeDelta=new Vector2(box.width,box.height);}
    }
}
