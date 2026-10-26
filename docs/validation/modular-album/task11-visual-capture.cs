if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Capture requires the isolated empty PlayMode scene.");
var fixtureType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t => t != null);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var paths = new System.Collections.Generic.List<string>();
foreach (var size in new[] { new UnityEngine.Vector2Int(1024, 768), new UnityEngine.Vector2Int(1280, 720), new UnityEngine.Vector2Int(1920, 1080) })
foreach (var scenario in new[] { "battle", "victory", "defeat", "pending" })
{
    var fixture = (System.IDisposable)System.Activator.CreateInstance(fixtureType, flags, null, new object[] { 0, 0, null }, null);
    var owner = new UnityEngine.GameObject("Task11 isolated " + scenario); owner.SetActive(false);
    var target = new UnityEngine.RenderTexture(size.x, size.y, 24);
    try
    {
        var balance = (PaperDefense.Application.GameBalanceAsset)fixtureType.GetField("BalanceAsset", flags).GetValue(fixture);
        var preset = (PaperDefense.Application.BalancePresetAsset)fixtureType.GetField("Preset", flags).GetValue(fixture);
        var store = (PaperDefense.Application.IWorkshopProfileStore)fixtureType.GetField("Store", flags).GetValue(fixture);
        preset.rules.PlanningSeconds = preset.rules.IntermissionSeconds = 10000;
        preset.rules.PassiveInkPerSecond = preset.rules.EarlyLaunchInkPerSecond = 0;
        if (scenario != "battle")
            for (int wave = 0; wave < preset.rules.Waves.Count; wave++) preset.rules.Waves[wave].Count = scenario == "defeat" && wave == 5 ? 1 : 0;
        var game = owner.AddComponent<PaperDefense.Presentation.PlayerGameRoot>(); game.Configure(balance, true);
        owner.SetActive(true); game.Run.AutomaticSimulation = false; game.Run.InitializeWorkshop(store); game.enabled = false;
        var shell = owner.GetComponentInChildren<PaperDefense.Presentation.PlayerShell>(); shell.enabled = false;
        var bootstrap = owner.GetComponentInChildren<PaperDefense.Presentation.PaperDefenseBootstrap>(); bootstrap.enabled = false;
        foreach (var responsive in owner.GetComponentsInChildren<PaperDefense.Presentation.WorkshopResponsiveLayout>(true)) responsive.enabled = false;
        var cameraObject = new UnityEngine.GameObject("Task11 canvas viewport", typeof(UnityEngine.Camera)); cameraObject.transform.SetParent(owner.transform);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>(); camera.enabled = false; camera.targetTexture = target;
        var layout = new PaperDefense.Presentation.PlayerLayout(size.x, size.y);
        foreach (var canvas in owner.GetComponentsInChildren<UnityEngine.Canvas>(true))
        {
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); if (scaler != null) scaler.enabled = false;
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.scaleFactor = layout.Scale;
        }
        shell.ApplyLayout(size.x, size.y);
        if (!game.StartBattle()) throw new System.InvalidOperationException("Capture run cannot start: " + game.Status);
        game.Drawing.enabled = false;
        var session = (PaperDefense.Core.GameSession)typeof(PaperDefense.Application.RunController).GetField("session", flags).GetValue(game.Run);
        var cells = new PaperDefense.Core.Cell[8];
        for (int slot = 0; slot < 8; slot++)
        {
            bool placed = false;
            for (int radius = 3; radius < 18 && !placed; radius += 2)
            for (int offset = 0; offset < 8 && !placed; offset++)
            {
                double angle = offset * System.Math.PI / 4;
                var cell = new PaperDefense.Core.Cell(session.BaseCell.X + (int)System.Math.Round(System.Math.Cos(angle) * radius), session.BaseCell.Y + (int)System.Math.Round(System.Math.Sin(angle) * radius));
                if (!game.Run.QuoteEntry(new[] { cell }, slot, PaperDefense.Core.MaterialTier.Pencil, PaperDefense.Core.InkColor.None).Valid) continue;
                string reason;
                if (!game.Run.BuildEntry(new[] { cell }, slot, PaperDefense.Core.MaterialTier.Pencil, PaperDefense.Core.InkColor.None, out reason)) throw new System.InvalidOperationException(reason);
                cells[slot] = cell; placed = true;
            }
            if (!placed) throw new System.InvalidOperationException("No capture cell for slot " + slot);
        }
        session.Tick(session.Structures.Max(s => s.BuildRemaining) + .1f);
        var hudField = typeof(PaperDefense.Presentation.PlayerShell).GetField("battle", flags);
        System.Action<string> capture = name =>
        {
            game.Drawing.enabled = false;
            var field = shell.Layout.NormalizedField; var world = bootstrap.worldCamera;
            world.targetTexture = target; world.rect = field;
            world.aspect = size.x * field.width / (size.y * field.height);
            world.orthographicSize = UnityEngine.Mathf.Max(session.Balance.Height * .5f + 1, session.Balance.Width * .5f / world.aspect + 1);
            world.transform.position = new UnityEngine.Vector3(session.Balance.Width * .5f, session.Balance.Height * .5f, -10);
            ((PaperDefense.Presentation.PlayerBattleHud)hudField.GetValue(shell)).Refresh();
            typeof(PaperDefense.Presentation.NotebookWorldView).GetMethod("LateUpdate", flags).Invoke(game.World, null);
            UnityEngine.Canvas.ForceUpdateCanvases();
            paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("task11-" + name + "-" + size.x + "x" + size.y, size.x, size.y, "docs/validation/modular-album/screenshots"));
        };
        System.Action<string> click = name => owner.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == name).onClick.Invoke();
        if (scenario == "battle")
        {
            capture("planning"); game.Run.NextWave();
            for (int tick = 0; tick < 120; tick++) game.Run.StepSimulation();
            capture("battle"); game.Drawing.Inspect(cells[1]); capture("tower");
            game.Drawing.Inspect(cells[7]); capture("generator"); game.Drawing.Inspect(cells[6]); capture("repair"); game.Drawing.Inspect(null);
            game.Pause(); capture("pause"); game.OpenSettings(); capture("settings");
            game.OpenSettings(true); capture("help"); game.CloseHelp(); game.CloseSettings();
            click("Leave battle"); capture("leave"); shell.CancelConfirmation();
        }
        else
        {
            if (scenario == "pending") store.GetType().GetField("RefuseWrites", flags).SetValue(store, true);
            int count = scenario == "defeat" ? 5 : 8;
            for (int wave = 1; wave <= count; wave++)
            {
                game.Run.NextWave(); game.Run.AdvanceFrame(1f / 30, false, 1);
                if (game.Run.Session.PendingUpgrade)
                {
                    game.Navigate(PaperDefense.Presentation.PlayerPage.Upgrades);
                    if (scenario == "victory") capture("upgrades" + (wave == 2 ? "" : wave.ToString()));
                    game.ChooseUpgrade(PaperDefense.Core.RunUpgrade.RapidFire);
                }
            }
            if (scenario == "defeat")
            {
                game.Run.NextWave();
                typeof(PaperDefense.Core.GameSession).GetMethod("DamageObjective", flags).Invoke(session, new object[] { -1, 100000f });
                game.Run.AdvanceFrame(1f / 30, false, 1);
            }
            game.Navigate(PaperDefense.Presentation.PlayerPage.Result); capture(scenario);
            if (scenario == "victory") { click("Result statistics"); capture("statistics"); click("Close result statistics"); }
        }
    }
    finally { UnityEngine.Object.DestroyImmediate(owner); target.Release(); UnityEngine.Object.DestroyImmediate(target); fixture.Dispose(); }
}
return string.Join("\n", paths);
