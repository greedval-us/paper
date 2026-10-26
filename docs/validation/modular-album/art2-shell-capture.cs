if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Capture requires the isolated empty PlayMode scene.");
var fixtureType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t => t != null);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var paths = new System.Collections.Generic.List<string>();
string captureNames = UnityEditor.SessionState.GetString("PaperDefense.Art2.ShellNames","15-planning,15-overview,16-battle,17-tower,17-generator,17-repair,18-pause,19-upgrades,20-victory,20-pending,20-statistics,21-defeat,22-settings,23-help,24-empty,28-leave");
string captureSize = UnityEditor.SessionState.GetString("PaperDefense.Art2.CaptureSize", "");
foreach (var size in new[] { new UnityEngine.Vector2Int(1024, 768), new UnityEngine.Vector2Int(1280, 720), new UnityEngine.Vector2Int(1920, 1080), new UnityEngine.Vector2Int(1280, 960), new UnityEngine.Vector2Int(1600, 900) }
.Where(size => string.IsNullOrEmpty(captureSize) || captureSize == size.x + "x" + size.y))
foreach (var scenario in new[] { "battle", "victory", "defeat", "pending" })
{
    var fixture = (System.IDisposable)System.Activator.CreateInstance(fixtureType, flags, null, new object[] { scenario == "battle" ? 94 : 0, scenario == "battle" ? 390 : 0, null }, null);
    var owner = new UnityEngine.GameObject("Task13 isolated " + scenario); owner.SetActive(false);
    var target = new UnityEngine.RenderTexture(size.x, size.y, 24);
    try
    {
        var balance = (PaperDefense.Application.GameBalanceAsset)fixtureType.GetField("BalanceAsset", flags).GetValue(fixture);
        var preset = (PaperDefense.Application.BalancePresetAsset)fixtureType.GetField("Preset", flags).GetValue(fixture);
        var store = (PaperDefense.Application.IWorkshopProfileStore)fixtureType.GetField("Store", flags).GetValue(fixture);
        preset.rules.PlanningSeconds = preset.rules.IntermissionSeconds = 30;
        preset.rules.PassiveInkPerSecond = preset.rules.EarlyLaunchInkPerSecond = 0;
        if (scenario != "battle")
            for (int wave = 0; wave < preset.rules.Waves.Count; wave++) preset.rules.Waves[wave].Count = scenario == "defeat" && wave == 5 ? 1 : 0;
        var game = owner.AddComponent<PaperDefense.Presentation.PlayerGameRoot>(); game.Configure(balance, true);
        owner.SetActive(true); game.Run.AutomaticSimulation = false; game.Run.InitializeWorkshop(store); game.enabled = false;
        var shell = owner.GetComponentInChildren<PaperDefense.Presentation.PlayerShell>(); shell.enabled = false;
        var bootstrap = owner.GetComponentInChildren<PaperDefense.Presentation.PaperDefenseBootstrap>(); bootstrap.enabled = false;
        foreach (var responsive in owner.GetComponentsInChildren<PaperDefense.Presentation.WorkshopResponsiveLayout>(true)) responsive.enabled = false;
        var cameraObject = new UnityEngine.GameObject("Task13 canvas viewport", typeof(UnityEngine.Camera)); cameraObject.transform.SetParent(owner.transform);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>(); camera.enabled = false; camera.targetTexture = target;
        var layout = new PaperDefense.Presentation.PlayerLayout(size.x, size.y);
        foreach (var canvas in owner.GetComponentsInChildren<UnityEngine.Canvas>(true))
        {
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); if (scaler != null) scaler.enabled = false;
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.scaleFactor = layout.Scale;
        }
        shell.ApplyLayout(size.x, size.y);
        System.Action clearHints = () => {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            foreach (var tooltip in owner.GetComponentsInChildren<PaperDefense.Presentation.WorkshopTooltip>(true)) tooltip.OnPointerExit(null);
            foreach (var tooltip in owner.GetComponentsInChildren<PaperDefense.Presentation.PlayerTooltip>(true)) tooltip.OnPointerExit(null);
        };
        if (scenario == "battle")
        {
            System.Action<string> capturePage = name => {
                if (!captureNames.Split(',').Contains(name)) return;
                clearHints();
                UnityEngine.Canvas.ForceUpdateCanvases();
                paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("art2-" + name + "-" + size.x + "x" + size.y,
                    size.x, size.y, "docs/validation/modular-album/art2-screenshots"));
            };
            game.Navigate(PaperDefense.Presentation.PlayerPage.Home); capturePage("01-home");
            game.Navigate(PaperDefense.Presentation.PlayerPage.Map); capturePage("02-map");
            game.Navigate(PaperDefense.Presentation.PlayerPage.Preparation); capturePage("03-preparation");
            game.OpenAlbum();
            owner.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name.StartsWith("Album choose ")).onClick.Invoke();
            capturePage("09-album");
            shell.Album.SetFilter(PaperDefense.Presentation.AlbumFilter.Mine); capturePage("24-empty");
            game.CloseAlbum();
        }
        if (!game.StartBattle()) throw new System.InvalidOperationException("Capture run cannot start: " + game.Status);
        game.Drawing.enabled = false;
        var session = (PaperDefense.Core.GameSession)typeof(PaperDefense.Application.RunController).GetField("session", flags).GetValue(game.Run);
        var cells = new PaperDefense.Core.Cell[8];
        for (int slot = 0; slot < 8; slot++)
        {
            bool placed = false;
            for (double radius = 3.5; radius < 24 && !placed; radius += 2)
            for (int offset = 0; offset < 8 && !placed; offset++)
            {
                double angle = offset * System.Math.PI / 4 + System.Math.PI / 8;
                var cell = new PaperDefense.Core.Cell(session.BaseCell.X + (int)System.Math.Round(System.Math.Cos(angle) * radius), session.BaseCell.Y + (int)System.Math.Round(System.Math.Sin(angle) * radius));
                if (!game.Run.QuoteEntry(new[] { cell }, slot, PaperDefense.Core.MaterialTier.Pencil, PaperDefense.Core.InkColor.None).Valid) continue;
                string reason;
                if (!game.Run.BuildEntry(new[] { cell }, slot, PaperDefense.Core.MaterialTier.Pencil, PaperDefense.Core.InkColor.None, out reason)) throw new System.InvalidOperationException(reason);
                cells[slot] = cell; placed = true;
            }
            if (!placed) throw new System.InvalidOperationException("No capture cell for slot " + slot);
        }
        session.Tick(session.Structures.Max(s => s.BuildRemaining) + .1f);
        if (session.Phase != PaperDefense.Core.RunPhase.Planning || session.WaveIndex != 0 || session.Countdown <= 0 || session.Countdown > 30)
            throw new System.InvalidOperationException("Capture construction must remain in real planning with a normal positive countdown.");
        var hudField = typeof(PaperDefense.Presentation.PlayerShell).GetField("battle", flags);
        System.Action<string> capture = name =>
        {
            if (!captureNames.Split(',').Contains(name)) return;
            game.Drawing.enabled = false;
            var field = shell.Layout.NormalizedField; var world = bootstrap.worldCamera;
            world.targetTexture = target; world.rect = field;
            world.aspect = size.x * field.width / (size.y * field.height);
            world.orthographicSize = UnityEngine.Mathf.Max(session.Balance.Height * .5f + 1, session.Balance.Width * .5f / world.aspect + 1) / game.Drawing.Zoom;
            world.transform.position = new UnityEngine.Vector3(game.Drawing.CameraCenter.x, game.Drawing.CameraCenter.y, -10);
            ((PaperDefense.Presentation.PlayerBattleHud)hudField.GetValue(shell)).Refresh();
            typeof(PaperDefense.Presentation.NotebookWorldView).GetMethod("LateUpdate", flags).Invoke(game.World, null);
            clearHints();
            UnityEngine.Canvas.ForceUpdateCanvases();
            paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("art2-" + name + "-" + size.x + "x" + size.y, size.x, size.y, "docs/validation/modular-album/art2-screenshots"));
            if (name == "23-help")
            {
                var keys = owner.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                    .Where(text => text.transform.parent.name.StartsWith("Help key ")).ToArray();
                if (keys.Length != 12) throw new System.InvalidOperationException("Help must show all 12 real key bindings.");
                foreach (var key in keys)
                    if (key.cachedTextGenerator.characterCountVisible < key.text.Length)
                        throw new System.InvalidOperationException("Help key clips at " + size.x + "x" + size.y + ": " + key.text);
                System.IO.File.AppendAllText("docs/validation/modular-album/art2-help-key-proof.txt",
                    size.x + "x" + size.y + ": 12/12 complete key labels, including Space/Esc; catalog " + PaperDefense.Presentation.AssemblyVisualComposer.Revision + "\n");
            }
        };
        System.Action<string> click = name => owner.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == name).onClick.Invoke();
        if (scenario == "battle")
        {
            capture("15-planning"); click("Battle overview"); capture("15-overview"); click("Battle overview"); game.Run.NextWave();
            for (int tick = 0; tick < 600; tick++) game.Run.StepSimulation();
            capture("16-battle"); game.Drawing.Inspect(cells[1]); capture("17-tower");
            game.Drawing.Inspect(cells[7]); capture("17-generator"); game.Drawing.Inspect(cells[6]); capture("17-repair"); game.Drawing.Inspect(null);
            game.Pause(); capture("18-pause"); game.OpenSettings(); capture("22-settings");
            game.OpenSettings(true); capture("23-help"); game.CloseHelp(); game.CloseSettings();
            click("Leave battle"); capture("28-leave"); shell.CancelConfirmation();
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
                    if (scenario == "victory")
                    {
                        capture("19-upgrades" + wave);
                        if (wave == 4) capture("19-upgrades");
                    }
                    game.ChooseUpgrade(PaperDefense.Core.RunUpgrade.RapidFire);
                }
            }
            if (scenario == "defeat")
            {
                game.Run.NextWave();
                typeof(PaperDefense.Core.GameSession).GetMethod("DamageObjective", flags).Invoke(session, new object[] { -1, 100000f });
                game.Run.AdvanceFrame(1f / 30, false, 1);
            }
            game.Navigate(PaperDefense.Presentation.PlayerPage.Result); capture(scenario == "victory" ? "20-victory" : scenario == "defeat" ? "21-defeat" : "20-pending");
            if (scenario == "victory") { click("Result statistics"); capture("20-statistics"); click("Close result statistics"); }
        }
    }
    finally { UnityEngine.Object.DestroyImmediate(owner); target.Release(); UnityEngine.Object.DestroyImmediate(target); fixture.Dispose(); }
}
return string.Join("\n", paths);
