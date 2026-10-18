#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace PaperDefense.Tests
{
    public sealed class CompositeWorkshopHostTests
    {
        [UnityTest] public IEnumerator EditingANameSurvivesLiveHudUpdatesWithoutLosingFocusOrText()
        {
            var events = new GameObject("Composite live editing events", typeof(EventSystem));
            try
            {
                using (var fixture = new AssemblyCommandFixture())
                {
                    var hud = fixture.Root.AddComponent<WorkshopHud>();
                    hud.UseGuidedFlow = true; hud.Mode = WorkshopHudMode.Full; hud.Initialize(fixture.Run);
                    hud.Composite.OpenEntry(fixture.Gun());
                    fixture.Root.SetActive(true); hud.PresentationVisible = true;
                    var field = hud.Composite.Root.GetComponentInChildren<UnityEngine.UI.InputField>();
                    Assert.That(field, Is.Not.Null);
                    field.Select(); field.ActivateInputField(); field.text = "Незавершённое название";
                    yield return new WaitForSecondsRealtime(.4f);
                    Assert.That(field != null, Is.True, "Live status updates must preserve the active input control.");
                    Assert.That(field.text, Is.EqualTo("Незавершённое название"));
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(field.gameObject));
                    Assert.That(fixture.Store.SaveCalls, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(events); }
        }

        [Test] public void ConfiguredPlayerWorkshopUsesCompositePagesAndKeepsTheDraftWhenHidden()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var hud = fixture.Root.AddComponent<WorkshopHud>();
                hud.UseGuidedFlow = true; hud.Mode = WorkshopHudMode.Full; hud.Initialize(fixture.Run);
                Assert.That(fixture.Run.AssemblyCatalog, Is.Not.Null);
                Assert.That(hud.Composite, Is.Not.Null);
                Assert.That(hud.Guided, Is.Null);
                var draft = hud.Composite.DraftSession;
                draft.Begin(fixture.Gun()); draft.OpenWeapon(0);
                var before = JsonUtility.ToJson(new AlbumEntryData(draft.PreviewEntry));
                hud.PresentationVisible = false; hud.PresentationVisible = true;
                Assert.That(draft.IsComponentOpen, Is.True);
                Assert.That(JsonUtility.ToJson(new AlbumEntryData(draft.PreviewEntry)), Is.EqualTo(before));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void CompositeWorkshopDoesNotCreateInteractiveDrawingOrGeometryEditors()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var hud = fixture.Root.AddComponent<WorkshopHud>();
                hud.UseGuidedFlow = true; hud.Mode = WorkshopHudMode.Full; hud.Initialize(fixture.Run);
                Assert.That(hud.Composite, Is.Not.Null);
                Assert.That(hud.DrawingEditor, Is.Null);
                Assert.That(hud.MortarFootprintEditor, Is.Null);
                Assert.That(hud.FanVolleyEditor, Is.Null);
                Assert.That(hud.ReinforcementEditor, Is.Null);
                Assert.That(fixture.Root.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .Any(button => button.name == "Composite preparation"), Is.True);
            }
        }
    }
}
#endif
