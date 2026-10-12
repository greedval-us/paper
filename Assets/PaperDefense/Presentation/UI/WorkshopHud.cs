using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>The album edits immutable drawings; a run receives four frozen choices.</summary>
    public sealed class WorkshopHud : MonoBehaviour
    {
        const int AlbumRowsPerPage = 8;
        RunController run;
        Font font;
        GameObject canvasObject, trialPanel, trialShade, qualityPanel, qualityShade;
        RectTransform root;
        Text gold, stats, status, briefing, trialResults, trialSettings, trialFirst, trialSecond, trialPlayback, trialConditions;
        Text profileSaveStatus;
        Button retryProfileSave;
        Text qualityExplanation, trialFirstHeader, trialSecondHeader, qualityModeLabel;
        InputField title;
        BlueprintSketch sketch;
        readonly BlueprintSketch[] albumSketches = new BlueprintSketch[AlbumRowsPerPage];
        public DrawingEditorPanel DrawingEditor { get; private set; }
        WorkshopMapSketch map;
        TrialLaneSketch trialDiagramFirst, trialDiagramSecond;
        readonly Button[] albumButtons = new Button[AlbumRowsPerPage];
        readonly Text[] albumLabels = new Text[AlbumRowsPerPage], slotLabels = new Text[ProfileLimits.LoadoutSlotCount];
        Button[] partButtons, firstWeapons, secondWeapons, moduleButtons;
        readonly Button[] chassisButtons = new Button[2];
        Text[] partLabels;
        string[] partIds;
        StructureKind[] mountKinds;
        TowerModules[] moduleKinds;
        int albumPage, trialBudget = 400;
        float refresh;
        bool counterfire, qualityTrial;
        TrialScenario trialScenario;
        public TowerBlueprint Draft { get; private set; }
        public string Status { get; private set; } = "Собирай свободно. Золото открывает деталь навсегда; экземпляры строятся за штрихи.";

        public void Initialize(RunController controller)
        {
            run = controller;
            var mounts = new List<StructureKind>();
            var modules = new List<TowerModules>();
            var unlocks = new List<string>();
            foreach (var part in PartCatalog.All)
            {
                if (part.MountKind.HasValue) mounts.Add(part.MountKind.Value);
                if (part.Module != TowerModules.None) modules.Add(part.Module);
                if (!part.InitiallyUnlocked) unlocks.Add(part.Id);
            }
            mountKinds = mounts.ToArray(); firstWeapons = new Button[mountKinds.Length]; secondWeapons = new Button[mountKinds.Length + 1];
            moduleKinds = modules.ToArray();
            moduleButtons = new Button[moduleKinds.Length];
            partIds = unlocks.ToArray(); partButtons = new Button[partIds.Length]; partLabels = new Text[partIds.Length];
            font = Resources.Load<Font>("NotebookHand") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Workshop HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var board = new GameObject("Workshop paper", typeof(RectTransform), typeof(Image));
            board.transform.SetParent(canvasObject.transform, false); root = board.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.sizeDelta = new Vector2(1600, 1000);
            board.GetComponent<Image>().color = NotebookPalette.Paper; board.GetComponent<Image>().raycastTarget = true;
            BuildComposer(); BuildAlbum(); BuildLoadout(); BuildTrial(); BuildQualityDetails();
            DrawingEditor = new DrawingEditorPanel(root, font, drawing => { Draft = Draft.WithDrawing(drawing); SetStatus("Авторские штрихи применены к черновику. Сохрани страницу в альбом."); });
            Draft = run.Workshop.Album.Count > 0 ? run.Workshop.Album[0] : NewDrawing();
            title.SetTextWithoutNotify(Draft.Name);
            run.WorkshopChanged += Refresh;
            Refresh();
        }

        void OnDestroy() { if (run != null) run.WorkshopChanged -= Refresh; }
        void Update()
        {
            if (run == null) return;
            canvasObject.SetActive(run.InWorkshop);
            if (!run.InWorkshop) return;
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0) { refresh = .15f; Refresh(); }
        }

        TowerBlueprint NewDrawing() => TowerBlueprint.NewDraft("drawing-" + Guid.NewGuid().ToString("N"), "Мой рисунок", ChassisKind.Standard, StructureKind.Gun);
        void NewDraft() { Draft = NewDrawing(); title.SetTextWithoutNotify(Draft.Name); SetStatus("Новый лист. Выбери шасси, до двух стволов и модули."); }
        void SetDraft(ChassisKind chassis, StructureKind first, StructureKind? second, TowerModules modules, string selectedPart = null)
        {
            Draft = Draft.WithName(title.text).WithAssembly(chassis, first, second, modules);
            if (selectedPart != null) foreach (var part in run.Workshop.Parts) if (part.Id == selectedPart) { Status = part.Description; break; }
            Refresh();
        }
        void SetStatus(string value) { Status = value; Refresh(); }

        void BuildComposer()
        {
            Label(root, "МАСТЕРСКАЯ РИСУНКОВ", new Rect(35, 17, 1050, 57), 38, NotebookPalette.Navy, true);
            gold = Label(root, "", new Rect(1170, 23, 395, 46), 27, NotebookPalette.Pencil, true, TextAnchor.MiddleRight);
            Label(root, "Детали открываются навсегда. Собирай и испытывай бесплатно — за золото покупается право использовать деталь.", new Rect(37, 78, 1525, 31), 18, NotebookPalette.Navy);
            var left = Panel(root, "Composer", new Rect(28, 126, 532, 570), new Color(1, .985f, .91f));
            Label(left, "01  СОБЕРИ ЧЕРТЁЖ", new Rect(18, 10, 490, 35), 25, NotebookPalette.Blue, true);
            title = Input(left, "Blueprint name", new Rect(18, 54, 490, 43));
            title.onEndEdit.AddListener(value => { if (Draft != null) SetDraft(Draft.Chassis, Draft.FirstWeapon, Draft.SecondWeapon, Draft.Modules); });
            Label(left, "Шасси · два крепления · один цвет в бою", new Rect(18, 107, 490, 29), 18, NotebookPalette.Navy);
            chassisButtons[0] = Button(left, "Chassis Standard", new Rect(18, 141, 235, 44), "ОБЫЧНОЕ  /  10", () => SetDraft(ChassisKind.Standard, Draft.FirstWeapon, Draft.SecondWeapon, Draft.Modules, "chassis.standard"));
            chassisButtons[1] = Button(left, "Chassis Heavy", new Rect(268, 141, 240, 44), "ТЯЖЁЛОЕ  /  14", () => SetDraft(ChassisKind.Heavy, Draft.FirstWeapon, Draft.SecondWeapon, Draft.Modules, "chassis.heavy"));
            Label(left, "Первое крепление", new Rect(18, 196, 490, 24), 18, NotebookPalette.Navy, true);
            for (int i = 0; i < mountKinds.Length; i++)
            {
                var kind = mountKinds[i]; string partId = PartCatalog.GetMountId(kind);
                firstWeapons[i] = Button(left, "First " + kind, new Rect(18 + i % 4 * 123, 225 + i / 4 * 38, 115, 32), WeaponName(kind), () => SetDraft(Draft.Chassis, kind, Draft.SecondWeapon, Draft.Modules, partId));
                secondWeapons[i] = Button(left, "Second " + kind, new Rect(18 + i % 4 * 123, 335 + i / 4 * 38, 115, 32), WeaponName(kind), () => SetDraft(Draft.Chassis, Draft.FirstWeapon, kind, Draft.Modules, partId));
                firstWeapons[i].GetComponentInChildren<Text>().fontSize = secondWeapons[i].GetComponentInChildren<Text>().fontSize = 13;
            }
            Label(left, "Второе крепление · общая подача", new Rect(18, 306, 490, 24), 18, NotebookPalette.Navy, true);
            int emptyIndex = mountKinds.Length;
            secondWeapons[emptyIndex] = Button(left, "Second None", new Rect(18 + emptyIndex % 4 * 123, 335 + emptyIndex / 4 * 38, 115, 32), "ПУСТО", () => SetDraft(Draft.Chassis, Draft.FirstWeapon, null, Draft.Modules));
            secondWeapons[emptyIndex].GetComponentInChildren<Text>().fontSize = 13;
            Label(left, "Оборудование · тоже занимает нагрузку", new Rect(18, 412, 490, 25), 18, NotebookPalette.Navy, true);
            for (int i = 0; i < moduleKinds.Length; i++)
            {
                var module = moduleKinds[i];
                PartCatalog.TryGetModule(module, out var part);
                moduleButtons[i] = Button(left, "Module " + module, new Rect(18 + i * 123, 445, 115, 33), part.ButtonLabel, () => SetDraft(Draft.Chassis, Draft.FirstWeapon, Draft.SecondWeapon, Draft.Modules ^ module, part.Id));
                moduleButtons[i].GetComponentInChildren<Text>().fontSize = 12;
            }
            Button(left, "New blueprint", new Rect(18, 487, 140, 43), "НОВЫЙ", NewDraft);
            Button(left, "Save blueprint", new Rect(170, 487, 200, 43), "В АЛЬБОМ", SaveDraft);
            Button(left, "Delete blueprint", new Rect(382, 487, 126, 43), "УДАЛИТЬ", () => SetStatus(run.DeleteBlueprint(Draft.Id, out var reason) ? "Рисунок удалён из альбома." : reason));
            Button(left, "Save blueprint as new", new Rect(18, 537, 490, 27), "СОХРАНИТЬ КОПИЮ НА НОВОЙ СТРАНИЦЕ", SaveAsNew);

            var center = Panel(root, "Live drawing", new Rect(578, 126, 475, 570), new Color(.97f, .97f, .91f));
            Label(center, "02  НАРИСУЙ И ПРОВЕРЬ", new Rect(18, 10, 440, 35), 24, NotebookPalette.Blue, true);
            var illustration = new GameObject("Live blueprint sketch", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
            illustration.transform.SetParent(center, false); Place(illustration.GetComponent<RectTransform>(), new Rect(30, 58, 415, 191));
            sketch = illustration.GetComponent<BlueprintSketch>(); sketch.raycastTarget = false;
            Button(center, "Open drawing editor", new Rect(22, 251, 432, 42), "НАРИСОВАТЬ СВОИ ЛИНИИ  →", () => DrawingEditor.Open(Draft));
            stats = Label(center, "", new Rect(22, 305, 432, 181), 17, NotebookPalette.Navy);
            Button(center, "Drawing quality details", new Rect(22, 487, 432, 30), "ПОЧЕМУ ТАКОЕ КАЧЕСТВО?", ShowQualityDetails);
            Button(center, "Open trial", new Rect(22, 523, 432, 39), "ИСПЫТАТЬ НА ТЕСТОВОМ ЛИСТЕ  →", () => { SetTrial(true); RefreshTrialSettings(); });
        }

        void BuildAlbum()
        {
            var album = Panel(root, "Blueprint album", new Rect(1071, 126, 500, 570), new Color(1, .98f, .86f));
            Label(album, "03  АЛЬБОМ ЧЕРТЕЖЕЙ", new Rect(16, 10, 465, 35), 24, NotebookPalette.Blue, true);
            Label(album, "Сохраняй много вариантов. В бой возьми четыре.", new Rect(18, 47, 460, 27), 17, NotebookPalette.Navy);
            for (int i = 0; i < albumButtons.Length; i++)
            {
                int index = i;
                albumButtons[i] = Button(album, "Album drawing " + i, new Rect(18, 82 + i * 43, 460, 41), "", () => SelectAlbum(albumPage * AlbumRowsPerPage + index), out albumLabels[i]);
                var thumb = new GameObject("Album thumbnail " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
                thumb.transform.SetParent(albumButtons[i].transform, false); Place(thumb.GetComponent<RectTransform>(), new Rect(4, 1, 48, 35));
                albumSketches[i] = thumb.GetComponent<BlueprintSketch>(); albumSketches[i].raycastTarget = false;
                Place(albumLabels[i].rectTransform, new Rect(58, 1, 394, 39)); albumLabels[i].fontSize = 13;
            }
            Button(album, "Album previous", new Rect(18, 435, 224, 33), "←  ПРЕДЫДУЩИЕ", () => { albumPage = Mathf.Max(0, albumPage - 1); Refresh(); });
            Button(album, "Album next", new Rect(254, 435, 224, 33), "СЛЕДУЮЩИЕ  →", () => { albumPage = Mathf.Min(Mathf.Max(0, (run.Workshop.Album.Count - 1) / AlbumRowsPerPage), albumPage + 1); Refresh(); });
            for (int i = 0; i < partIds.Length; i++)
            {
                int index = i;
                partButtons[i] = Button(album, "Unlock " + partIds[i], new Rect(18 + i % 4 * 117, 483 + i / 4 * 39, 109, 35), "", () => SetStatus(run.UnlockPart(partIds[index], out var reason) ? "Деталь открыта навсегда. Экземпляры не расходуют золото." : reason), out partLabels[i]);
                Place(partLabels[i].rectTransform, new Rect(1, 1, 107, 33));
                partLabels[i].fontSize = 11;
            }
        }

        void BuildLoadout()
        {
            Label(root, "04  ЧЕТЫРЕ ЧЕРТЕЖА В БОЙ  ·  число экземпляров ограничено только штрихами и местом", new Rect(33, 708, 1528, 33), 23, NotebookPalette.Blue, true);
            for (int i = 0; i < slotLabels.Length; i++)
            {
                int slot = i;
                Button(root, "Assign slot " + i, new Rect(32 + i * 306, 748, 294, 74), "", () => SetStatus(run.AssignBlueprint(slot, Draft.Id, out var reason) ? "Рисунок назначен в слот " + (slot + 1) + "." : reason), out slotLabels[i]);
            }
            Button(root, "Start run", new Rect(1262, 748, 307, 74), "НАЧАТЬ ПАРТИЮ  →", () => SetStatus(run.StartRun(out var reason) ? "Чертежи взяты в бой. Детали менять можно после партии." : reason));
            var diagram = new GameObject("Pre-run map", typeof(RectTransform), typeof(CanvasRenderer), typeof(WorkshopMapSketch));
            diagram.transform.SetParent(root, false); Place(diagram.GetComponent<RectTransform>(), new Rect(35, 839, 295, 80));
            map = diagram.GetComponent<WorkshopMapSketch>(); map.raycastTarget = false;
            briefing = Label(root, "", new Rect(352, 839, 1208, 68), 17, NotebookPalette.Navy);
            profileSaveStatus = Label(root, "", new Rect(35, 909, 1205, 42), 15, NotebookPalette.Navy);
            profileSaveStatus.name = "Profile save status";
            status = Label(root, "", new Rect(35, 953, 1525, 35), 17, NotebookPalette.Blue, false, TextAnchor.MiddleLeft);
            retryProfileSave = Button(root, "Retry profile save", new Rect(1262, 909, 307, 42), "ПОВТОРИТЬ ЗАПИСЬ", () =>
                SetStatus(run.RetrySave(out var reason) ? reason : "Повтор записи: " + reason));
        }

        void BuildTrial()
        {
            trialShade = Panel(root, "Trial background", new Rect(0, 0, 1600, 1000), new Color(.17f, .22f, .29f, .18f)).gameObject;
            trialShade.GetComponent<Image>().raycastTarget = true;
            trialPanel = Panel(root, "Test sheet", new Rect(240, 100, 1120, 800), NotebookPalette.Paper).gameObject;
            trialPanel.GetComponent<Image>().raycastTarget = true;
            Label(trialPanel.transform, "ТЕСТОВЫЙ ЛИСТ  /  ОДИНАКОВЫЙ БЮДЖЕТ", new Rect(28, 20, 1060, 46), 30, NotebookPalette.Blue, true);
            trialConditions = Label(trialPanel.transform, "", new Rect(30, 81, 1060, 54), 17, NotebookPalette.Navy);
            string[] names = { "ТОЛПА", "БЕГУНЫ", "БРОНЯ", "КЛЯКСЫ" };
            for (int i = 0; i < names.Length; i++)
            { int scenario = i; Button(trialPanel.transform, "Trial " + i, new Rect(30 + i * 204, 145, 191, 48), names[i], () => { trialScenario = (TrialScenario)scenario; RefreshTrialSettings(); }); }
            Button(trialPanel.transform, "Counterfire", new Rect(858, 145, 230, 48), "ОБРАТНЫЙ ОГОНЬ", () => { counterfire = !counterfire; RefreshTrialSettings(); });
            int[] budgets = { 200, 400, 800 };
            for (int i = 0; i < budgets.Length; i++)
            { int budget = budgets[i]; Button(trialPanel.transform, "Trial budget " + budget, new Rect(30 + i * 188, 209, 175, 42), budget + " ШТРИХОВ", () => { trialBudget = budget; RefreshTrialSettings(); }); }
            Button(trialPanel.transform, "Trial quality mode", new Rect(602, 209, 486, 42), "", () => { qualityTrial = !qualityTrial; RefreshTrialSettings(); }, out qualityModeLabel);
            trialSettings = Label(trialPanel.transform, "", new Rect(30, 270, 1058, 50), 21, NotebookPalette.Navy, true);
            trialPlayback = Label(trialPanel.transform, "ЗАПИСЬ ИСПЫТАНИЯ ×4  ·  нажми «Провести тест»", new Rect(30, 314, 1058, 27), 17, NotebookPalette.Blue);
            trialDiagramFirst = LaneDiagram(trialPanel.transform, "Blueprint test lane", new Rect(30, 347, 505, 153));
            trialDiagramSecond = LaneDiagram(trialPanel.transform, "Reference test lane", new Rect(582, 347, 505, 153));
            trialFirstHeader = Label(trialPanel.transform, "ТВОЙ РИСУНОК", new Rect(403, 517, 310, 29), 18, NotebookPalette.Blue, true, TextAnchor.MiddleCenter);
            trialSecondHeader = Label(trialPanel.transform, "ОДИН ПУЛЕМЁТ", new Rect(740, 517, 348, 29), 18, NotebookPalette.Blue, true, TextAnchor.MiddleCenter);
            trialResults = Label(trialPanel.transform, "Нажми «Провести тест».", new Rect(30, 552, 354, 166), 15, NotebookPalette.Navy);
            trialFirst = Label(trialPanel.transform, "", new Rect(403, 552, 310, 166), 15, NotebookPalette.Navy, false, TextAnchor.UpperCenter);
            trialSecond = Label(trialPanel.transform, "", new Rect(740, 552, 348, 166), 15, NotebookPalette.Navy, false, TextAnchor.UpperCenter);
            Button(trialPanel.transform, "Run trial", new Rect(30, 724, 503, 55), "ПРОВЕСТИ ТЕСТ", RunTest);
            Button(trialPanel.transform, "Close trial", new Rect(557, 724, 531, 55), "ВЕРНУТЬСЯ К ЧЕРТЕЖУ", () => SetTrial(false));
            SetTrial(false);
        }
        void BuildQualityDetails()
        {
            qualityShade = Panel(root, "Drawing quality shade", new Rect(0, 0, 1600, 1000), new Color(.12f, .18f, .28f, .4f)).gameObject;
            qualityShade.GetComponent<Image>().raycastTarget = true;
            qualityPanel = Panel(root, "Drawing quality details panel", new Rect(330, 140, 940, 720), NotebookPalette.Paper).gameObject;
            qualityPanel.GetComponent<Image>().raycastTarget = true;
            Label(qualityPanel.transform, "КАЧЕСТВО РИСУНКА", new Rect(28, 20, 884, 48), 30, NotebookPalette.Blue, true);
            qualityExplanation = Label(qualityPanel.transform, "", new Rect(28, 86, 884, 535), 21, NotebookPalette.Navy);
            qualityExplanation.name = "Drawing quality explanation";
            Button(qualityPanel.transform, "Close quality details", new Rect(28, 644, 884, 48), "ВЕРНУТЬСЯ К ЧЕРТЕЖУ", () => { qualityPanel.SetActive(false); qualityShade.SetActive(false); });
            qualityPanel.SetActive(false); qualityShade.SetActive(false);
        }
        void ShowQualityDetails()
        {
            var assessment = Draft.Assessment;
            var preview = run.PreviewBlueprint(Draft);
            string text = "Качество: " + assessment.Quality.ToString("0") + " / 100 · темп " + (preview.QualityMultiplier * 100).ToString("0") + "%\n";
            text += "Оценка сравнивает контуры с образцом с допуском к дрожи руки. Повторные линии не добавляют баллов; украшения не оцениваются.\n\n";
            if (Draft.Origin == DrawingOrigin.LegacyStandard) text += "Стандартный рисунок: сохранён прежний темп. Для нового варианта нарисуй собственные линии.\n";
            foreach (var part in assessment.PartFeedback)
                text += DrawingEditorPanel.Name(part.Part) + ": " + part.Quality.ToString("0") + " / 100\n" + part.Message + "\n";
            text += "\nКачество меняет только интервал действий после общей подачи. Цена, урон, прочность, защита и дальность от рисунка не меняются.";
            qualityExplanation.text = text; qualityShade.SetActive(true); qualityPanel.SetActive(true);
        }

        void SaveDraft()
        {
            Draft = Draft.WithName(title.text);
            SetStatus(run.SaveBlueprint(Draft, out var reason) ? "«" + Draft.Name + "» сохранён. Назначь его в один из четырёх слотов." : reason);
        }
        void SaveAsNew()
        {
            var candidate = Draft.AsNew("drawing-" + Guid.NewGuid().ToString("N"), title.text);
            if (!run.SaveBlueprint(candidate, out var reason)) { SetStatus(reason); return; }
            Draft = candidate; SetStatus("Копия сохранена на новой странице. Исходная страница сохранена отдельно.");
        }
        void SelectAlbum(int index)
        {
            if (index < 0 || index >= run.Workshop.Album.Count) return;
            Draft = run.Workshop.Album[index]; title.SetTextWithoutNotify(Draft.Name);
            SetStatus("Открыт «" + Draft.Name + "». Изменения применятся после сохранения; партия берёт отдельный набор.");
        }
        void Refresh()
        {
            if (run == null || Draft == null || gold == null) return;
            gold.text = "ЗОЛОТО  " + run.Workshop.Gold;
            albumPage = Mathf.Clamp(albumPage, 0, Mathf.Max(0, (run.Workshop.Album.Count - 1) / AlbumRowsPerPage));
            var balance = run.Session.Balance;
            chassisButtons[0].GetComponentInChildren<Text>().text = "ОБЫЧНОЕ  /  " + balance.ChassisCapacity(ChassisKind.Standard);
            chassisButtons[1].GetComponentInChildren<Text>().text = "ТЯЖЁЛОЕ  /  " + balance.ChassisCapacity(ChassisKind.Heavy);
            moduleButtons[0].GetComponentInChildren<Text>().text = "БРОНЯ  +" + balance.ModuleLoad(TowerModules.Armor);
            for (int i = 0; i < chassisButtons.Length; i++) Choice(chassisButtons[i], (int)Draft.Chassis == i);
            for (int i = 0; i < mountKinds.Length; i++)
            {
                Choice(firstWeapons[i], Draft.FirstWeapon == mountKinds[i]);
                Choice(secondWeapons[i], Draft.SecondWeapon == mountKinds[i]);
            }
            for (int i = 0; i < moduleKinds.Length; i++) Choice(moduleButtons[i], (Draft.Modules & moduleKinds[i]) != 0);
            Choice(secondWeapons[mountKinds.Length], !Draft.SecondWeapon.HasValue);
            var preview = run.PreviewBlueprint(Draft, MaterialTier.Pencil);
            int deficit = Mathf.Max(0, preview.Load - preview.Capacity);
            stats.text = "НАГРУЗКА  " + preview.Load + " / " + preview.Capacity + (deficit > 0 ? "   НЕ ХВАТАЕТ " + deficit : "") +
                "\nЦена: " + preview.Cost + " штр. · " + preview.Health.ToString("0") + " HP · защита " + (preview.Defense * 100).ToString("0") + "%" +
                "\nПостройка: " + preview.BuildSeconds.ToString("0.0") + " с    Дальность: " + preview.Range.ToString("0.0") +
                "\nТемп: " + preview.FireRate.ToString("0.00") + " действий/с · подача " + (preview.FeedMultiplier * 100).ToString("0") + "%" +
                "\nКачество: " + preview.DrawingQuality.ToString("0") + "/100 · темп " + (preview.QualityMultiplier * 100).ToString("0") + "%" +
                "\nОкраска: " + preview.ColorCost + " чернил" + (run.Session.Balance.LimitTowerColor ?
                    " · цвет ≤" + run.Session.Balance.ColorTargetsPerWindow + " цели / " + run.Session.Balance.ColorWindowSeconds.ToString("0.#") + " с" : "") +
                (preview.Valid ? "" : "\n" + preview.Reason);
            stats.color = preview.Valid ? NotebookPalette.Navy : NotebookPalette.Red;
            sketch.SetBlueprint(Draft);
            status.text = Status;
            RefreshSaveStatus();
            if (trialPanel.activeSelf && trialDiagramFirst.HasRecording)
                trialPlayback.text = "ЗАПИСЬ ИСПЫТАНИЯ ×4  ·  " + trialDiagramFirst.PlaybackSeconds.ToString("0.0") + " с  ·  реальные позиции, снаряды и выстрелы";
            for (int i = 0; i < albumButtons.Length; i++)
            {
                int index = albumPage * AlbumRowsPerPage + i;
                albumButtons[i].gameObject.SetActive(index < run.Workshop.Album.Count);
                if (index >= run.Workshop.Album.Count) continue;
                var bp = run.Workshop.Album[index];
                var albumPreview = run.PreviewBlueprint(bp);
                albumLabels[i].text = bp.Name + " · " + (bp.Origin == DrawingOrigin.LegacyStandard ? "стандартный" : bp.Assessment.Quality.ToString("0") + "/100") +
                    " · " + albumPreview.Cost + " штр.\n" + WeaponName(bp.FirstWeapon) + (bp.SecondWeapon.HasValue ? "+" + WeaponName(bp.SecondWeapon.Value) : "") +
                    " · " + albumPreview.Health.ToString("0") + " HP · " + albumPreview.FireRate.ToString("0.0") + "/с";
                albumLabels[i].fontSize = 13;
                albumSketches[i].SetBlueprint(bp);
                albumButtons[i].GetComponent<Image>().color = bp.Id == Draft.Id ? new Color(.8f, .87f, .94f) : Color.white;
            }
            var loadout = run.Workshop.Loadout;
            for (int i = 0; i < slotLabels.Length; i++)
            {
                slotLabels[i].text = (i + 1) + "  " + (i < loadout.Count ? loadout[i].Name : "пусто") + "\n↑ назначить выбранный рисунок";
            }
            for (int i = 0; i < partIds.Length; i++)
            {
                bool unlocked = run.Workshop.IsUnlocked(partIds[i]);
                PartCatalog.TryGet(partIds[i], out var definition);
                string name = definition.UnlockLabel;
                int price = 0;
                foreach (var part in run.Workshop.Parts) if (part.Id == partIds[i]) price = part.GoldCost;
                partLabels[i].text = name + "\n" + (unlocked ? "ОТКРЫТО ✓" : price + " золота");
                partButtons[i].interactable = !unlocked;
            }
            string wells = "";
            if (run.Session != null)
                for (int i = 0; i < run.Session.Wells.Count; i++)
                { var well = run.Session.Wells[i]; wells += (i > 0 ? "   ·   " : "") + ColorName(well.Color) + " " + well.Cell; }
            string threat = run.Session != null && run.Session.Balance.Waves.Count > 0 ? run.Session.Balance.Waves[0].Title : "Первый штурм с запада";
            map.SetSession(run.Session);
            bool runners=false,gnawers=false,spitters=false;
            foreach(var wave in run.Session.Balance.Waves)
            {runners|=wave.RunnerFraction>0;gnawers|=wave.GnawerFraction>0;spitters|=wave.SpitterFraction>0;}
            string enemies="каракули"+(runners?", бегуны":"")+(gnawers?", грызуны":"")+(spitters?", плевуны":"");
            briefing.text = "КАРТА " + run.Session.Balance.Width + " × " + run.Session.Balance.Height + "  ·  База в центре  ·  Точки: " + wells +
                "\nУгрозы партии: "+enemies+". Стены перехватывают прямые сгустки."+
                (run.Session.Balance.Preset == GameBalancePreset.Target ?
                    "\nСтарт: " + run.Session.Balance.StartingInk + " штрихов · ожидание не даёт штрихов · доход за врагов и волны. Точки открывают фронт." :
                    "\nНачало: " + threat + ". Активация точки открывает дополнительный фронт.");
        }
        void RefreshSaveStatus()
        {
            var profile = run.Workshop;
            bool failed = profile.SaveState == ProfileSaveState.WriteFailed;
            bool protectedSource = profile.SaveState == ProfileSaveState.RecoveryRequired;
            string scope = profile.SaveState == ProfileSaveState.MemoryOnly ? "ТОЛЬКО В ПАМЯТИ" :
                protectedSource ? "ВОССТАНОВЛЕНИЕ ТРЕБУЕТ ВНИМАНИЯ" : failed ? "ОШИБКА ЗАПИСИ" :
                profile.HasUnsavedChanges ? "ЕСТЬ НЕСОХРАНЁННЫЕ ДАННЫЕ" : "СОХРАНЕНО";
            string detail = profile.SaveMessage;
            if (profile.HasUnsavedChanges) detail += " При аварийном закрытии несохранённые изменения могут потеряться.";
            profileSaveStatus.text = scope + " · " + detail;
            profileSaveStatus.color = failed || protectedSource ? NotebookPalette.Red : NotebookPalette.Navy;
            // Failed manual commands remain on their original buttons; retry never replays a purchase.
            retryProfileSave.gameObject.SetActive(profile.HasUnsavedChanges && !protectedSource);
        }
        void SetTrial(bool visible) { trialShade.SetActive(visible); trialPanel.SetActive(visible); }
        void RefreshTrialSettings()
        {
            bool blotTraining = trialScenario == TrialScenario.Blots;
            trialSettings.text = "Сценарий: " + (blotTraining ? "кляксы" : trialScenario == TrialScenario.Crowd ? "толпа" : trialScenario == TrialScenario.Fast ? "бегуны" : "броня") + "    Бюджет: " + trialBudget + "    Ответный огонь: " + (blotTraining ? "НЕТ ВРАГОВ" : counterfire ? "ДА" : "НЕТ");
            trialConditions.text = "Готовые карандашные башни, фиксированные позиции, без стен и наград. " +
                (blotTraining ? "Заданные кляксы: конечный запас, без врагов и новых пятен. До очистки или 60 с." : "Без клякс. Ответный огонь: 10 урона / 2,4 с в радиусе 12,5.");
            qualityModeLabel.text = qualityTrial ? "СРАВНЕНИЕ: ТВОЁ И 100% КАЧЕСТВА" : "СРАВНЕНИЕ: С ОДИНОЧНЫМИ ПУЛЕМЁТАМИ";
            trialFirstHeader.text = qualityTrial ? "ТВОЁ КАЧЕСТВО " + Draft.Assessment.Quality.ToString("0") : "ТВОЙ РИСУНОК";
            trialSecondHeader.text = qualityTrial ? "100% качества · та же сборка" : "ОДИН ПУЛЕМЁТ";
            ClearTrial();
        }
        void ClearTrial()
        {
            trialDiagramFirst.SetResult(null, 0, 1); trialDiagramSecond.SetResult(null, 0, 1);
            trialFirst.text = trialSecond.text = ""; trialResults.text = "Нажми «Провести тест».";
            trialPlayback.text = "ЗАПИСЬ ИСПЫТАНИЯ ×4  ·  готовый рисунок против одиночных пулемётов";
        }
        void RunTest()
        {
            var preview = run.PreviewBlueprint(Draft, MaterialTier.Pencil);
            if (!preview.Valid) { ClearTrial(); trialResults.text = preview.Reason; return; }
            var comparison = qualityTrial ? run.RunQualityTrial(Draft, trialScenario, counterfire, trialBudget) : run.RunTrial(Draft, trialScenario, counterfire, trialBudget);
            if (comparison == null)
            {
                SetStatus("Сначала откройте детали этой сборки. Испытание не расходует золото и штрихи.");
                ClearTrial(); trialResults.text = Status; return;
            }
            var a = comparison.First; var b = comparison.Second;
            trialResults.text = "Копий / потрачено штрихов\nФактический урон\nПрорывов\nВыжило башен\nПростой без цели, с\nДлительность, с\nНе завершили маршрут\nОчищено клякс\nНе очищены";
            trialFirst.text = TrialValues(a); trialSecond.text = TrialValues(b);
            float started = Time.unscaledTime, duration = Mathf.Max(a.DurationSeconds, b.DurationSeconds);
            trialDiagramFirst.SetResult(a, started, duration); trialDiagramSecond.SetResult(b, started, duration);
        }
        static string TrialValues(TrialReport report) => report.Copies + " / " + report.Cost + "\n" + report.Damage.ToString("0") + "\n" + report.Leaks + "\n" +
            report.SurvivingTowers + "\n" + report.IdleSeconds.ToString("0.0") + "\n" + report.DurationSeconds.ToString("0.0") + "\n" + report.UnresolvedEnemies + "\n" + report.BlotsCleared + "\n" + report.UnresolvedBlots;
        static void Choice(Button button, bool chosen) => button.GetComponent<Image>().color = chosen ? new Color(.8f, .87f, .94f) : Color.white;
        TrialLaneSketch LaneDiagram(Transform parent, string name, Rect box)
        {
            var diagram = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TrialLaneSketch)); diagram.transform.SetParent(parent, false);
            Place(diagram.GetComponent<RectTransform>(), box); var graphic = diagram.GetComponent<TrialLaneSketch>(); graphic.raycastTarget = false; return graphic;
        }
        internal static string WeaponName(StructureKind kind)
            => PartCatalog.TryGetMount(kind, out var part) ? part.ButtonLabel : kind.ToString().ToUpperInvariant();
        internal static string ColorName(InkColor color) => color == InkColor.Red ? "красная" : color == InkColor.Blue ? "синяя" : color == InkColor.Green ? "зелёная" : "без цвета";

        RectTransform Panel(Transform parent, string name, Rect box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); Place(rect, box); var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(.15f, .24f, .35f, .5f); outline.effectDistance = new Vector2(1, -1);
            return rect;
        }
        Text Label(Transform parent, string text, Rect box, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), box);
            var label = go.GetComponent<Text>(); label.font = font; label.text = text; label.fontSize = size; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color; label.alignment = align; label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        Button Button(Transform parent, string name, Rect box, string text, Action action) => Button(parent, name, box, text, action, out _);
        Button Button(Transform parent, string name, Rect box, string text, Action action, out Text label)
        {
            var rect = Panel(parent, name, box, Color.white); rect.GetComponent<Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            button.onClick.AddListener(() => action());
            // NotebookHand's line height exceeds its point size; compact controls need the full vertical space.
            float verticalInset = box.height <= 35 ? 1 : 4;
            label = Label(rect, text, new Rect(6, verticalInset, box.width - 12, box.height - verticalInset * 2),
                box.height <= 30 || box.height > 60 ? 16 : 17, NotebookPalette.Navy, true, TextAnchor.MiddleCenter);
            return button;
        }
        InputField Input(Transform parent, string name, Rect box)
        {
            var rect = Panel(parent, name, box, Color.white); rect.GetComponent<Image>().raycastTarget = true;
            var input = rect.gameObject.AddComponent<InputField>(); input.targetGraphic = rect.GetComponent<Image>(); input.characterLimit = 32;
            input.textComponent = Label(rect, "", new Rect(10, 5, box.width - 20, box.height - 10), 22, NotebookPalette.Navy);
            return input;
        }
        static void Place(RectTransform rect, Rect box)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(box.x, -box.y); rect.sizeDelta = box.size; }
    }

    /// <summary>A live hand-drawn diagram, including both weapon mounts and armor plates.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BlueprintSketch : Graphic
    {
        TowerBlueprint blueprint;
        public void SetBlueprint(TowerBlueprint value) { if (blueprint == value) return; blueprint = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (blueprint == null) return;
            Vector2 center = rectTransform.rect.center;
            if (blueprint.Origin == DrawingOrigin.PlayerDrawing)
            {
                float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .68f;
                foreach (var part in blueprint.Drawing.Parts)
                {
                    if (!DrawingTemplateCatalog.IsEnabled(blueprint, part.Key)) continue;
                    Point2 pivot = DrawingTemplateCatalog.Pivot(blueprint, part.Key);
                    foreach (var stroke in part.Strokes)
                        DrawingCanvasGraphic.Draw(vh, stroke.Points, center + new Vector2(pivot.X, pivot.Y) * scale, scale, NotebookPalette.Blue, .018f);
                }
                return;
            }
            float legacyScale = Mathf.Min(1, Mathf.Min(rectTransform.rect.width / 270, rectTransform.rect.height / 250));
            // Standard pages retain their historical diagram, fitted to both the preview and thumbnail.
            int before = vh.currentVertCount;
            float radius = blueprint.Chassis == ChassisKind.Heavy ? 76 : 65;
            Ring(vh, center, radius, 4, NotebookPalette.Blue);
            Ring(vh, center, radius - 13, 2, NotebookPalette.Blue);
            for (int side = -1; side <= 1; side += 2)
            {
                Line(vh, center + new Vector2(side * radius * .7f, -42), center + new Vector2(side * (radius + 20), -72), 5, NotebookPalette.Navy);
                if ((blueprint.Modules & TowerModules.Armor) != 0)
                { Line(vh, center + new Vector2(side * (radius + 8), -39), center + new Vector2(side * (radius + 8), 40), 13, NotebookPalette.Navy); }
            }
            Weapon(vh, center + (blueprint.SecondWeapon.HasValue ? Vector2.left * 28 : Vector2.zero), blueprint.FirstWeapon);
            if (blueprint.SecondWeapon.HasValue) Weapon(vh, center + Vector2.right * 28, blueprint.SecondWeapon.Value);
            if ((blueprint.Modules & TowerModules.Feed) != 0) Ring(vh, center - Vector2.up * 22, 15, 5, NotebookPalette.Green);
            if ((blueprint.Modules & TowerModules.Tracking) != 0)
            { Ring(vh, center + new Vector2(radius + 24, 57), 15, 3, NotebookPalette.Red); Line(vh, center + new Vector2(radius, 26), center + new Vector2(radius + 24, 57), 3, NotebookPalette.Navy); }
            if ((blueprint.Modules & TowerModules.Rail) != 0)
            {
                Line(vh, center + new Vector2(-34, -52), center + new Vector2(34, -52), 4, NotebookPalette.Navy);
                Line(vh, center + new Vector2(-34, -63), center + new Vector2(34, -63), 4, NotebookPalette.Navy);
                for (int i = -2; i <= 2; i++) Line(vh, center + new Vector2(i * 14, -49), center + new Vector2(i * 14, -66), 2, NotebookPalette.Blue);
            }
            UIVertex vertex = new UIVertex();
            for (int i = before; i < vh.currentVertCount; i++)
            { vh.PopulateUIVertex(ref vertex, i); vertex.position = (Vector3)center + (vertex.position - (Vector3)center) * legacyScale; vh.SetUIVertex(vertex, i); }
        }
        static void Weapon(VertexHelper vh, Vector2 origin, StructureKind kind)
        {
            if (DoodleGlyphs.UsesTemplateMount(kind))
            {
                var geometry = DoodleGlyphs.StandardMount(kind);
                int start = vh.currentVertCount;
                foreach (Vector2 vertex in geometry.Vertices) vh.AddVert(origin + vertex * 190, NotebookPalette.Blue, Vector2.zero);
                for (int i = 0; i < geometry.Indices.Count; i += 3)
                    vh.AddTriangle(start + geometry.Indices[i], start + geometry.Indices[i + 1], start + geometry.Indices[i + 2]);
                return;
            }
            float length = kind == StructureKind.Piercer ? 106 : kind == StructureKind.Mortar ? 49 : 83;
            float width = kind == StructureKind.Mortar ? 24 : kind == StructureKind.Piercer ? 12 : 17;
            Vector2 end = origin + Vector2.up * length;
            Line(vh, origin, end, width + 7, NotebookPalette.Navy); Line(vh, origin, end, width, NotebookPalette.Blue);
            Line(vh, end + Vector2.left * (width + 4), end + Vector2.right * (width + 4), 6, NotebookPalette.Navy);
        }
        internal static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color color)
        { for (int i = 0; i < 32; i++) { float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16; Line(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color); } }
        internal static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = to - from; if (direction.sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector2.zero); vh.AddVert(to - normal, color, Vector2.zero);
            vh.AddVert(to + normal, color, Vector2.zero); vh.AddVert(from + normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopMapSketch : Graphic
    {
        IRunReadModel session;
        public void SetSession(IRunReadModel value) { if (session == value) return; session = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (session == null) return;
            Rect rect = rectTransform.rect; Vector2 low = rect.min + Vector2.one * 8, high = rect.max - Vector2.one * 8;
            BlueprintSketch.Line(vh, low, new Vector2(high.x, low.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, low, new Vector2(low.x, high.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, high, new Vector2(high.x, low.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, high, new Vector2(low.x, high.y), 2, NotebookPalette.Navy);
            Vector2 home = Point(session.BaseCell, low, high);
            BlueprintSketch.Ring(vh, home, 7, 4, NotebookPalette.Navy);
            for (int i = 0; i < session.Wells.Count; i++)
            { var well = session.Wells[i]; BlueprintSketch.Ring(vh, Point(well.Cell, low, high), 5, 4, DoodleGlyphs.InkTone(well.Color)); }
            int sides = session.Balance.Waves.Count > 0 ? session.Balance.Waves[0].Sides : 1;
            if ((sides & 1) != 0) Arrow(vh, new Vector2(low.x + 2, home.y), Vector2.right);
            if ((sides & 2) != 0) Arrow(vh, new Vector2(high.x - 2, home.y), Vector2.left);
            if ((sides & 4) != 0) Arrow(vh, new Vector2(home.x, high.y - 2), Vector2.down);
            if ((sides & 8) != 0) Arrow(vh, new Vector2(home.x, low.y + 2), Vector2.up);
        }
        Vector2 Point(Cell cell, Vector2 low, Vector2 high) => new Vector2(Mathf.Lerp(low.x, high.x, (cell.X + .5f) / session.Balance.Width), Mathf.Lerp(low.y, high.y, (cell.Y + .5f) / session.Balance.Height));
        static void Arrow(VertexHelper vh, Vector2 at, Vector2 direction)
        {
            Vector2 tip = at + direction * 20, side = new Vector2(-direction.y, direction.x) * 6;
            BlueprintSketch.Line(vh, at, tip, 3, NotebookPalette.Red);
            BlueprintSketch.Line(vh, tip, tip - direction * 8 + side, 3, NotebookPalette.Red);
            BlueprintSketch.Line(vh, tip, tip - direction * 8 - side, 3, NotebookPalette.Red);
        }
    }

    /// <summary>Playback of value snapshots captured from the actual trial simulation.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrialLaneSketch : Graphic
    {
        TrialReport report;
        int frameIndex;
        float started, duration;
        public bool HasRecording => report != null && report.Frames.Count > 0;
        public float PlaybackSeconds => Mathf.Repeat((Time.unscaledTime - started) * 4, Mathf.Max(.1f, duration + .5f));
        public void SetResult(TrialReport value, float startTime, float totalDuration)
        { report = value; started = startTime; duration = totalDuration; frameIndex = 0; SetVerticesDirty(); }
        void Update()
        {
            if (!HasRecording) return;
            float seconds = PlaybackSeconds;
            int next = 0;
            for (int i = 1; i < report.Frames.Count && report.Frames[i].Seconds <= seconds; i++) next = i;
            if (next == frameIndex) return;
            frameIndex = next; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (!HasRecording) return;
            Rect rect = rectTransform.rect;
            Vector2 low = rect.min + new Vector2(8, 7), high = rect.max - new Vector2(8, 7);
            for (int y = 8; y <= 36; y += 4)
                BlueprintSketch.Line(vh, Point(8, y, low, high), Point(50, y, low, high), 1, MeshPen.Alpha(NotebookPalette.Blue, .12f));
            for (int x = 8; x <= 50; x += 4)
                BlueprintSketch.Line(vh, Point(x, 8, low, high), Point(x, 36, low, high), 1, MeshPen.Alpha(NotebookPalette.Blue, .12f));
            BlueprintSketch.Line(vh, Point(48, 8, low, high), Point(48, 36, low, high), 2, MeshPen.Alpha(NotebookPalette.Red, .65f));
            var frame = report.Frames[frameIndex];
            foreach (var blot in frame.Blots)
            {
                Vector2 at = Point(blot.Cell.X + .5f, blot.Cell.Y + .5f, low, high);
                BlueprintSketch.Ring(vh, at, 2.7f, 2, MeshPen.Alpha(NotebookPalette.Marker, .65f));
            }
            foreach (var tower in frame.Towers)
            {
                Vector2 at = Point(tower.Cell.X + .5f, tower.Cell.Y + .5f, low, high);
                if (tower.Health <= 0)
                { BlueprintSketch.Line(vh, at - Vector2.one * 3, at + Vector2.one * 3, 2, NotebookPalette.Red); continue; }
                if (tower.Blueprint != null && tower.Blueprint.Origin == DrawingOrigin.PlayerDrawing)
                { DrawAuthorTower(vh, tower, low, high); continue; }
                BlueprintSketch.Ring(vh, at, 4, 2, NotebookPalette.Blue);
                Vector2 aim = Point(tower.Aim.X, tower.Aim.Y, low, high) - at;
                if (aim.sqrMagnitude < 1) aim = Vector2.left;
                Vector2 end = at + aim.normalized * 9;
                BlueprintSketch.Line(vh, at, end, 2, NotebookPalette.Blue);
                if (tower.Blueprint != null && tower.Blueprint.SecondWeapon.HasValue)
                    BlueprintSketch.Line(vh, at + Vector2.up * 3, end + Vector2.up * 3, 2, NotebookPalette.Blue);
                if (tower.Blueprint != null && (tower.Blueprint.Modules & TowerModules.Armor) != 0)
                    BlueprintSketch.Line(vh, at + new Vector2(4, -4), at + new Vector2(4, 4), 3, NotebookPalette.Navy);
            }
            foreach (var enemy in frame.Enemies)
            {
                if (enemy.Health <= 0) continue;
                Vector2 at = Point(enemy.Position.X, enemy.Position.Y, low, high);
                BlueprintSketch.Ring(vh, at, enemy.Kind == EnemyKind.Gnawer ? 3 : 2, 1.5f, NotebookPalette.Navy);
                if (enemy.DrawingTier == DrawingTier.Hatched) BlueprintSketch.Line(vh, at - Vector2.one * 2, at + Vector2.one * 2, 1, NotebookPalette.Navy);
            }
            foreach (var shot in frame.Shots)
            {
                Vector2 from = Point(shot.From.X, shot.From.Y, low, high), to = Point(shot.To.X, shot.To.Y, low, high);
                if (shot.Kind == StructureKind.Blotter)
                { BlueprintSketch.Line(vh, from, to, 1, NotebookPalette.Green); BlueprintSketch.Ring(vh, to, Mathf.Max(3, shot.Radius * (high.y - low.y) / 28), 1, NotebookPalette.Green); }
                else BlueprintSketch.Line(vh, from, to, 1, MeshPen.Alpha(NotebookPalette.Blue, .65f));
            }
            foreach (var projectile in frame.Projectiles)
            {
                float progress = Mathf.Clamp01(1 - projectile.Remaining / Mathf.Max(.001f, projectile.Duration));
                Vector2 from = Point(projectile.From.X, projectile.From.Y, low, high), to = Point(projectile.To.X, projectile.To.Y, low, high);
                Vector2 at = Vector2.Lerp(from, to, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * 15;
                BlueprintSketch.Ring(vh, at, 2, 2, NotebookPalette.Marker);
            }
        }
        static void DrawAuthorTower(VertexHelper vh, StructureView tower, Vector2 low, Vector2 high)
        {
            Point2 origin = new Point2(tower.Cell.X + .5f, tower.Cell.Y + .5f);
            foreach (var part in tower.Blueprint.Drawing.Parts)
            {
                Point2 pivot = DrawingTemplateCatalog.Pivot(tower.Blueprint, part.Key);
                bool moving = part.Key == DrawingPartKey.Mount0 || part.Key == DrawingPartKey.Mount1;
                Point2 aim = part.Key == DrawingPartKey.Mount1 ? tower.SecondaryAim : tower.PrimaryAim;
                float angle = moving && (aim.X != 0 || aim.Y != 0)
                    ? Mathf.Atan2(aim.Y - origin.Y - pivot.Y, aim.X - origin.X - pivot.X) - Mathf.PI * .5f : 0;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                foreach (var stroke in part.Strokes)
                    for (int i = 1; i < stroke.Points.Count; i++)
                    {
                        var a = stroke.Points[i - 1]; var b = stroke.Points[i];
                        Vector2 from = Point(origin.X + pivot.X + a.X * c - a.Y * s, origin.Y + pivot.Y + a.X * s + a.Y * c, low, high);
                        Vector2 to = Point(origin.X + pivot.X + b.X * c - b.Y * s, origin.Y + pivot.Y + b.X * s + b.Y * c, low, high);
                        BlueprintSketch.Line(vh, from, to, 1, NotebookPalette.Blue);
                    }
            }
        }
        static Vector2 Point(float x, float y, Vector2 low, Vector2 high) => new Vector2(Mathf.Lerp(low.x, high.x, (x - 8) / 42), Mathf.Lerp(low.y, high.y, (y - 8) / 28));
    }
}
