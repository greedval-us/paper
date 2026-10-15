using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PaperDefense.Application;
using PaperDefense.Core;
using System.Collections.Generic;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Editor
{
    /// <summary>Repeatable evidence from the real Game View, using an explicitly memory-only player.</summary>
    public static class FullGameUiCapture
    {
        public static string Folder { get; set; } = "docs/validation/2026-10-15-full-game-ui";
        public static PlayerGameRoot Game { get; private set; }
        public static string LastCapture { get; private set; }
        static int originalIndex = -1;
        static Action pending;
        static double deadline, earliestCapture;
        static int afterFrame, requestedWidth, requestedHeight;
        static readonly Queue<Action> queue = new Queue<Action>();
        public static int Remaining => queue.Count + (pending != null ? 1 : 0);
        public static void Enqueue(string name, int width, int height, Action prepare)
        {
            queue.Enqueue(() => { prepare(); Capture(name, width, height); });
            if (pending == null && queue.Count == 1) EditorApplication.delayCall += Next;
        }
        static void Next()
        {
            if (pending != null || queue.Count == 0) return;
            try { queue.Dequeue()(); }
            catch (Exception e) { LastCapture = "ERROR: " + e; queue.Clear(); Debug.LogError(LastCapture); }
        }
        public static WorkshopWizard Wizard => Game.GetComponentInChildren<WorkshopHud>(true).Guided;
        public static void CompleteDrawing()
        {
            var drawing = Wizard.Drawing; var session = drawing.Session;
            session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f,-.35f), new DrawingPoint(.35f,.35f), out _);
            foreach (var mount in session.Blueprint.WeaponMounts)
                session.AddRectangle(DrawingBindings.Mount(mount.SlotIndex), new DrawingPoint(-.0625f,0), new DrawingPoint(.0625f,.5f), out _);
            drawing.Canvas.FinishStroke();
        }
        public static void WorkshopStep(int step, bool complete = false)
        {
            Game.Run.OpenWorkshop(); Game.Navigate(PlayerPage.Workshop); Wizard.NewDraft();
            for (int i = 0; i < Mathf.Min(2, step); i++) Wizard.Next();
            if (complete) CompleteDrawing();
            if (step == 3) Wizard.Next();
        }
        public static void Planning(bool wave = false)
        {
            Game.Run.OpenWorkshop(); Game.StartBattle();
            string reason; int count = 0;
            for (int x = 12; x < Game.Run.Session.Balance.Width - 8 && count < 6; x += 6)
                for (int y = 8; y < Game.Run.Session.Balance.Height - 6 && count < 6; y += 8)
                    if (Game.Run.BuildBlueprint(new[] { new Cell(x,y) },count % 4, MaterialTier.Pencil, InkColor.None,out reason)) count++;
            if (wave) { Game.Run.NextWave(); for (int i = 0; i < 120; i++) Game.Run.StepSimulation(); }
        }
        // A disk-free store is used only to inspect purchase and write-failure presentation.
        public sealed class PreviewStore : IWorkshopProfileStore
        {
            public bool Fail;
            public ProfileLoadResult Load()
            {
                var profile = new WorkshopProfile(Game.Run.Content, AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(PlayerGameSceneTools.BalancePath).CreateWorkshopBalance()); profile.GrantGold(2500);
                return new ProfileLoadResult(profile, ProfileLoadSource.Primary, "UI preview memory store", true);
            }
            public ProfileSaveResult Save(WorkshopProfile value) => new ProfileSaveResult(!Fail, Fail ? "Проверка интерфейса: запись недоступна." : "");
        }
        public static PreviewStore AttachPreviewStore(bool fail)
        {
            var store = new PreviewStore { Fail = fail }; Game.Run.InitializeWorkshop(store); return store;
        }
        public static void Terminal(RunPhase phase)
        {
            Planning();
            // Terminal fixtures exercise the production result presenter; no profile or balance asset is edited.
            var session = typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game.Run);
            typeof(GameSession).GetProperty("Phase").SetValue(session, phase);
            if (phase == RunPhase.Victory) typeof(GameSession).GetProperty("WaveIndex").SetValue(session, Game.Run.Session.Balance.Waves.Count);
            Game.Navigate(PlayerPage.Result);
        }

        public static void CreateFixture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (UnityEngine.Object.FindAnyObjectByType<PlayerGameRoot>() != null) throw new InvalidOperationException("A player already exists.");
            var root = new GameObject("UI validation — memory-only profile"); root.SetActive(false);
            var camera = new GameObject("UI validation camera", typeof(Camera)); camera.tag = "MainCamera"; camera.transform.SetParent(root.transform);
            Game = root.AddComponent<PlayerGameRoot>();
            Game.Configure(AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(PlayerGameSceneTools.BalancePath), true);
            root.SetActive(true); Game.Run.AutomaticSimulation = false;
            UnityEngine.Application.runInBackground = true;
        }

        public static void Click(string name)
        {
            var button = Game.GetComponentsInChildren<Button>().FirstOrDefault(x => x.name == name && x.IsInteractable());
            if (button == null) throw new InvalidOperationException("No available control: " + name);
            button.onClick.Invoke();
        }

        public static void SetResolution(int width, int height)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            var type = group.GetType(); int count = (int)type.GetMethod("GetTotalCount").Invoke(group, null), index = -1;
            for (int i = 0; i < count; i++)
            {
                var size = type.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)size.GetType().GetProperty("width").GetValue(size) == width && (int)size.GetType().GetProperty("height").GetValue(size) == height) { index = i; break; }
            }
            if (index < 0)
            {
                var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "UI QA " + width + "×" + height);
                type.GetMethod("AddCustomSize").Invoke(group, new[] { size }); index = count;
            }
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            var selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (originalIndex < 0) originalIndex = (int)selected.GetValue(view);
            selected.SetValue(view, index); view.Repaint();
        }

        public static void Capture(string name, int width, int height)
        {
            if (pending != null) throw new InvalidOperationException("Capture already pending.");
            SetResolution(width, height); requestedWidth = width; requestedHeight = height;
            afterFrame = Time.frameCount + 12; earliestCapture = EditorApplication.timeSinceStartup + .3; deadline = EditorApplication.timeSinceStartup + 20;
            LastCapture = "pending: " + name;
            pending = () =>
            {
                LastCapture = PlayerVisualCapture.Capture(name, width, height, Folder);
                var labels = Game.GetComponentsInChildren<Text>().Where(t => t.text.Length > 0 && t.preferredHeight > t.rectTransform.rect.height + .5f)
                    .Select(t => t.name + ": " + t.text.Replace('\n', ' ') + " [" + t.preferredHeight + "/" + t.rectTransform.rect.height + "]").ToArray();
                File.WriteAllLines(Folder + "/" + name + "-text-check.txt", labels);
            };
            EditorApplication.update += Tick;
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { LastCapture = "ERROR: resolution/frame timeout"; Finish(); return; }
            if (Time.frameCount < afterFrame || EditorApplication.timeSinceStartup < earliestCapture || Screen.width != requestedWidth || Screen.height != requestedHeight) return;
            try { pending(); }
            catch (Exception error) { LastCapture = "ERROR: " + error; }
            finally { Finish(); if (queue.Count > 0) EditorApplication.delayCall += Next; }
        }
        static void Finish() { pending = null; EditorApplication.update -= Tick; }
        public static void RestoreGameView()
        {
            queue.Clear(); Finish(); if (originalIndex < 0) return;
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(EditorWindow.GetWindow(type), originalIndex);
            originalIndex = -1;
        }
    }
}
