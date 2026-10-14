using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace PaperDefense.Tests
{
    public sealed class FanVolleyHudTests
    {
        [Test] public void MouseGuidesRejectDuplicatesUndoErrorFirstAndCloseCancelsGesture()
        {
            var root=new GameObject("Volley input",typeof(RectTransform),typeof(Canvas));
            try
            {
                var draft=new TowerBlueprint("fan","Fan",ChassisKind.Standard,StructureKind.Fan);
                var panel=new FanVolleyPanel(root.transform,Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),value=>draft=value,(bp,m,f)=>FanVolleyProbe.Run(bp,m,f,GameBalance.CreateDefault()));
                panel.Open(draft,0);
                void Draw(float x,float y)
                {
                    Canvas.ForceUpdateCanvases();var pointer=new PointerEventData(null){button=PointerEventData.InputButton.Left,pointerId=1,position=panel.Canvas.LocalPointToScreen(default)};
                    panel.Canvas.OnPointerDown(pointer);Assert.That(panel.Canvas.HasGesture,Is.True);
                    pointer.position=panel.Canvas.LocalPointToScreen(new Point2(x,y));panel.Canvas.OnDrag(pointer);panel.Canvas.OnPointerUp(pointer);
                }
                Draw(0,2);Draw(-1,2);Assert.That(draft.GetFanVolley(0).Guides.Count,Is.EqualTo(2));Assert.That(panel.Recording.Completed,Is.True);
                var accepted=draft;Draw(-1,2);Assert.That(draft,Is.SameAs(accepted));Assert.That(panel.Recording,Is.Null);
                panel.Record();Assert.That(panel.Recording,Is.Null);panel.Undo();Assert.That(draft,Is.SameAs(accepted));Assert.That(panel.Recording,Is.Not.Null);
                root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Volley select 0").onClick.Invoke();panel.DeleteSelected();
                Assert.That(draft.GetFanVolley(0).Guides.Count,Is.EqualTo(1));panel.Undo();Assert.That(draft.GetFanVolley(0).Guides.Count,Is.EqualTo(2));
                var pending=new PointerEventData(null){button=PointerEventData.InputButton.Left,position=panel.Canvas.LocalPointToScreen(default)};panel.Canvas.OnPointerDown(pending);
                panel.Close();panel.Canvas.OnPointerUp(pending);Assert.That(panel.Canvas.HasGesture,Is.False);Assert.That(panel.Recording,Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void WorkshopExposesVolleyEditorAndChangingPresetDoesNotSpendResources()
        {
            var host=new GameObject("Volley workshop");host.SetActive(false);
            try
            {
                var run=host.AddComponent<RunController>();run.InitializeWorkshop(false,allowLegacyFixtureDefaults:true);
                var hud=host.AddComponent<WorkshopHud>();hud.Initialize(run);
                var fanButton=host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="First Fan");fanButton.onClick.Invoke();
                var edit=host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Mortar footprint 0");
                Assert.That(edit.gameObject.activeSelf,Is.True,"The mount geometry button must also expose fan distribution.");
                int gold=run.Workshop.Gold,ink=run.Session.Ink;edit.onClick.Invoke();
                var preset=host.GetComponentsInChildren<Button>(true).SingleOrDefault(b=>b.name=="Volley preset 1");Assert.That(preset,Is.Not.Null);preset.onClick.Invoke();
                Assert.That(hud.Draft.GetFanVolley(0).Allocate(6),Is.EqualTo(new[]{6}));
                Assert.That(run.Workshop.Gold,Is.EqualTo(gold));Assert.That(run.Session.Ink,Is.EqualTo(ink));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
