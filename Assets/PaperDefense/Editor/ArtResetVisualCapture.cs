using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Offscreen workshop evidence from isolated, memory-only authoring sessions.</summary>
    public static class ArtResetVisualCapture
    {
        const string OutputFolder = "outputs/art-reset-2026-10-26";

        public static string[] CaptureWorkshop()
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(BalanceWorkbookExport.PlayerPath);
            if (balance == null) throw new InvalidOperationException("PlayerBalance is unavailable.");
            var previousCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .ToDictionary(canvas => canvas, canvas => canvas.enabled);
            var paths = new List<string>();
            try
            {
                foreach (var canvas in previousCanvases.Keys) canvas.enabled = false;
                foreach (var size in new[] { new Vector2Int(1600, 1000), new Vector2Int(1024, 768), new Vector2Int(800, 600) })
                    foreach (var family in new[] { AssemblyStructureFamily.Wall, AssemblyStructureFamily.Slow, AssemblyStructureFamily.Repair, AssemblyStructureFamily.Generator })
                        CaptureFamily(balance, family, size, paths);
                return paths.ToArray();
            }
            finally
            {
                foreach (var previous in previousCanvases)
                    if (previous.Key != null) previous.Key.enabled = previous.Value;
                Canvas.ForceUpdateCanvases();
            }
        }

        static void CaptureFamily(GameBalanceAsset balance, AssemblyStructureFamily family, Vector2Int size, List<string> paths)
        {
            GameObject runObject = null, hudObject = null;
            try
            {
                runObject = new GameObject("Temporary art reset run — memory only"); runObject.SetActive(false);
                var run = runObject.AddComponent<RunController>(); run.balanceAsset = balance; run.AutomaticSimulation = false;
                run.InitializeWorkshop(false);
                // The controller stays inactive; only its separate in-memory HUD needs to render.
                hudObject = new GameObject("Temporary art reset workshop HUD");
                var hud = hudObject.AddComponent<WorkshopHud>(); hud.Initialize(run);
                var panel = hud.Composite ?? throw new InvalidOperationException("The sequential workshop is unavailable.");
                var canvas = hud.GetComponentInChildren<Canvas>(true);
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.referenceResolution = new Vector2(size.x, size.y);
                var board = panel.Root.parent as RectTransform;
                if (board == null) throw new InvalidOperationException("The workshop board is unavailable.");
                board.sizeDelta = scaler.referenceResolution; panel.Reflow(size.x, size.y);
                // A synchronous offscreen render must not inherit the editor Game View's scale.
                canvas.scaleFactor = 1;
                canvas.gameObject.SetActive(true); canvas.enabled = true;
                Click(panel, "Composite route structure"); Click(panel, "Structure family " + family);
                string stem = "structure-" + family.ToString().ToLowerInvariant();
                if (size.x == 1600) Capture(panel, stem + "-family", size, paths);
                Click(panel, "Structure next"); Click(panel, "Structure category Shell"); Click(panel, "Structure inspect shell.light");
                Capture(panel, stem + "-parts", size, paths);
                Click(panel, "Structure next");
                Capture(panel, stem + "-review", size, paths);
            }
            finally
            {
                if (hudObject != null) UnityEngine.Object.DestroyImmediate(hudObject);
                if (runObject != null) UnityEngine.Object.DestroyImmediate(runObject);
            }
        }

        static void Click(CompositeWorkshopPanel panel, string name)
        {
            var button = panel.Root.GetComponentsInChildren<UnityEngine.UI.Button>(true).SingleOrDefault(item => item.name == name);
            if (button == null || !button.IsInteractable()) throw new InvalidOperationException("Capture control unavailable: " + name + "; " + panel.Status);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }

        static void Capture(CompositeWorkshopPanel panel, string name, Vector2Int size, List<string> paths)
        {
            panel.RefreshStatus(); Canvas.ForceUpdateCanvases();
            string path = ReleaseVisualCapture.Capture(name + "-" + size.x + "x" + size.y, size.x, size.y, OutputFolder);
            paths.Add(Path.GetFullPath(path));
        }
    }
}
