using System.Collections;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class NotebookIntegrationTests
    {
        GameObject root;
        PaperDefenseBootstrap game;
        GameBalanceAsset asset;
        Mouse mouse;
        InputSettings originalInputSettings, fixtureInputSettings;
        NativeInputIsolation nativeInput;

        [UnitySetUp] public IEnumerator SetUp()
        {
            nativeInput = new NativeInputIsolation();
            fixtureInputSettings=null;
            originalInputSettings=InputSystem.settings;
            // A failed earlier play run can leave the manager holding a destroyed
            // temporary object. Recreate its default runtime baseline before isolation.
            if(originalInputSettings==null)
            {
                originalInputSettings=ScriptableObject.CreateInstance<InputSettings>();
                originalInputSettings.hideFlags=HideFlags.HideAndDontSave;
                InputSystem.settings=originalInputSettings;
            }
            fixtureInputSettings=Object.Instantiate(originalInputSettings);
            fixtureInputSettings.hideFlags=HideFlags.HideAndDontSave;
            fixtureInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            fixtureInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // InputManager destroys generated HideAndDontSave settings when replacing them.
            // Retain the original instance for an exact restoration after this test.
            HideFlags originalFlags=originalInputSettings.hideFlags;
            try
            {
                if(originalFlags==HideFlags.HideAndDontSave)
                    originalInputSettings.hideFlags=HideFlags.None;
                InputSystem.settings=fixtureInputSettings;
            }
            finally {originalInputSettings.hideFlags=originalFlags;}
            Assert.That(originalInputSettings!=null,Is.True,"Replacing fixture settings must retain the original runtime settings object.");
            foreach(var existing in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(existing.gameObject);
            yield return null;
            root=new GameObject("Integration notebook");root.SetActive(false);
            asset=ScriptableObject.CreateInstance<GameBalanceAsset>();asset.ResetToDefaults();asset.planningSeconds=1000;
            game=root.AddComponent<PaperDefenseBootstrap>();game.balance=asset;game.memoryOnlyProfile=true;
            var camera=new GameObject("Integration camera",typeof(Camera));camera.transform.SetParent(root.transform);
            game.worldCamera=camera.GetComponent<Camera>();
            mouse=InputSystem.AddDevice<Mouse>("NotebookTestMouse");mouse.MakeCurrent();
            root.SetActive(true);game.Controller.Initialize();game.Controller.TogglePause();yield return null;yield return null;
            Assert.That(mouse.enabled,Is.True,$"Fixture mouse was disabled; focused={UnityEngine.Application.isFocused}; background={InputSystem.settings.backgroundBehavior}; editor={InputSystem.settings.editorInputBehaviorInPlayMode}");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            try
            {
                if(mouse!=null && mouse.added) InputSystem.RemoveDevice(mouse);
                Object.Destroy(root);Object.Destroy(asset);
                if(fixtureInputSettings!=null)
                {
                    bool originalAlive=originalInputSettings!=null;
                    InputSettings restore=originalInputSettings;
                    if(!originalAlive)
                    {
                        restore=ScriptableObject.CreateInstance<InputSettings>();
                        restore.hideFlags=HideFlags.HideAndDontSave;
                    }
                    InputSystem.settings=restore;
                    if(fixtureInputSettings!=null) Object.Destroy(fixtureInputSettings);
                    Assert.That(originalAlive,Is.True,"The original input settings must survive until restoration.");
                    Assert.That(InputSystem.settings,Is.SameAs(originalInputSettings));
                }
            }
            finally { nativeInput?.Dispose(); nativeInput = null; }
            yield return null;
        }

        [UnityTest] public IEnumerator FastMouseStrokeFillsEveryCellAndPauseKeepsItUnbuilt()
        {
            game.Drawing.SelectTool(StructureKind.Wall);
            Vector2 a=game.worldCamera.WorldToScreenPoint(new Vector3(9.5f,5.5f,0));
            Vector2 b=game.worldCamera.WorldToScreenPoint(new Vector3(15.5f,5.5f,0));
            Assert.That(Mouse.current,Is.SameAs(mouse),"The stroke fixture must own the current mouse before its press.");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=a}.WithButton(MouseButton.Left));yield return null;yield return null;
            Assert.That(Mouse.current,Is.SameAs(mouse),"Another mouse became current while delivering the stroke press.");
            Assert.That(mouse.position.ReadValue(),Is.EqualTo(a),"Queued pointer position was not delivered to the player input state.");
            Assert.That(mouse.leftButton.isPressed,Is.True,"Queued pointer press was not delivered to the player input state.");
            Assert.That(game.Drawing.HasHover,Is.True,$"Stroke press did not reach the map: screen={a}, camera={game.worldCamera.pixelRect}, message={game.Drawing.Message}");
            Assert.That(game.Drawing.HoverCell,Is.EqualTo(new Cell(9,5)),"The camera or pointer position changed after computing the first endpoint.");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=b}.WithButton(MouseButton.Left));yield return null;yield return null;
            Assert.That(Mouse.current,Is.SameAs(mouse),"Another mouse became current while delivering the stroke movement.");
            Assert.That(game.Drawing.HoverCell,Is.EqualTo(new Cell(15,5)),"The camera or pointer position changed after computing the last endpoint.");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=b});yield return null;yield return null;
            for(int x=9;x<=15;x++) Assert.That(game.Controller.Session.StructureAt(new Cell(x,5)),Is.Not.Null,$"Missing stroke cell {x}; drawing message={game.Drawing.Message}; preview reason={game.Drawing.PreviewReason}");
            var structure=game.Controller.Session.StructureAt(new Cell(12,5));
            Assert.That(structure.Value.BuildRemaining,Is.GreaterThan(0));
            Assert.That(game.Controller.Session.Elapsed,Is.EqualTo(0));
            game.Controller.TogglePause();yield return new WaitForSeconds(.65f);
            Assert.That(game.Controller.Session.StructureAt(new Cell(12,5)).Value.BuildRemaining,Is.Zero);
        }

        [UnityTest] public IEnumerator HudClickStartsWaveWithoutDrawingThroughButton()
        {
            Button button=null;
            foreach(var candidate in root.GetComponentsInChildren<Button>(true)) if(candidate.name=="Launch wave") button=candidate;
            Assert.That(button,Is.Not.Null);
            int before=game.Controller.Session.Structures.Count;
            Vector2 p=RectTransformUtility.WorldToScreenPoint(null,button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p});yield return null;yield return null;
            Assert.That(game.Controller.Session.Phase,Is.EqualTo(RunPhase.Assault));
            Assert.That(game.Controller.Session.Structures.Count,Is.EqualTo(before));
        }

        [UnityTest] public IEnumerator RestartRebindsPresentationAndKeepsOneHud()
        {
            var original=game.Controller.Session;
            game.Controller.Build(new[]{new Cell(10,10)},StructureKind.Mortar,MaterialTier.Pencil,out _);
            game.Controller.Restart();yield return null;
            Assert.That(game.Controller.Session,Is.Not.SameAs(original));
            Assert.That(game.Controller.Session.StructureAt(new Cell(10,10)),Is.Null);
            Assert.That(root.GetComponentsInChildren<NotebookHud>().Length,Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(5));
        }

        [UnityTest] public IEnumerator UnchangedPreviewAndStaticBasesDoNotUploadEveryRenderFrame()
        {
            var cells = new[] { new Cell(10,10), new Cell(11,10) };
            game.View.SetPreview(cells,StructureKind.Wall,MaterialTier.Pencil,true);
            int structures=game.View.StaticGeometryRefreshCount;
            game.View.SetPreview(cells,StructureKind.Wall,MaterialTier.Pencil,true);
            yield return null; yield return null;
            Assert.That(game.View.StaticGeometryRefreshCount,Is.EqualTo(structures));
            // DrawingInput may clear an explicit test preview on the first frame,
            // so compare again after the input has settled.
            game.View.SetPreview(cells,StructureKind.Wall,MaterialTier.Pencil,true);
            int previews=game.View.PreviewGeometryRefreshCount;
            game.View.SetPreview(cells,StructureKind.Wall,MaterialTier.Pencil,true);
            Assert.That(game.View.PreviewGeometryRefreshCount,Is.EqualTo(previews));
            Assert.That(game.Controller.Build(cells,StructureKind.Wall,MaterialTier.Pencil,out _),Is.True);
            yield return null;
            Assert.That(game.View.StaticGeometryRefreshCount,Is.GreaterThan(structures));
        }

        [Test] public void MortarLaunchDoesNotCreateAnExplosionAndVisualMarksAreBounded()
        {
            var events=new SimulationEventBuffer();
            events.Publish(1,SimulationEventKind.ProjectileLaunched,new Point2(1,1),new Point2(8,8),StructureKind.Mortar,2);
            game.View.AddEvent(events[0]);
            Assert.That(game.View.ActiveVisualEffects,Is.Zero);
            for(int i=0;i<1200;i++)
            {
                events.Publish(2,SimulationEventKind.ProjectileImpact,new Point2(1,1),new Point2(8,8),StructureKind.Mortar,2);
                game.View.AddEvent(events[events.Count-1]);
            }
            Assert.That(game.View.ActiveVisualEffects,Is.EqualTo(NotebookVisualThemeAsset.Default?.MaximumEffects ?? 256));
        }

        [Test] public void ScriptableBalanceCopiesSettingsWithoutSharingSessionDefinitions()
        {
            var config=ScriptableObject.CreateInstance<GameBalanceAsset>();config.ResetToDefaults();config.weapons[1].damage=123;
            var a=config.CreateBalance();var b=config.CreateBalance();a.Structure(StructureKind.Gun).Damage=1;
            Assert.That(b.Structure(StructureKind.Gun).Damage,Is.EqualTo(123));Object.DestroyImmediate(config);
        }
    }
}
