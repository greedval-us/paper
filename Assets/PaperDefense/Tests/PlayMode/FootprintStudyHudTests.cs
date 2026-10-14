using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class FootprintStudyHudTests
    {
        [Test] public void ComparisonLocksDraftUndoAndReopeningEditorClosesOldStudy()
        {
            var host=new GameObject("Study lifecycle");host.SetActive(false);
            try
            {
                var run=host.AddComponent<RunController>();run.InitializeWorkshop(false,allowLegacyFixtureDefaults:true);
                var hud=host.AddComponent<WorkshopHud>();hud.Initialize(run);
                var editor=hud.MortarFootprintEditor;var original=run.Workshop.Loadout[3];editor.Open(original,0);
                editor.SetContour(FootprintStudy.CreateShape(FootprintStudyShape.Longitudinal).Authored);
                var draft=hud.Draft;
                host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Footprint study").onClick.Invoke();
                Assert.That(editor.Study.IsOpen,Is.True);
                host.SetActive(true);
                var repeat=host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Study repeat 0");
                var up=repeat.FindSelectableOnUp();
                Assert.That(up,Is.Not.Null);
                Assert.That(up.name,Does.StartWith("Study "),"Navigation must stay inside the comparison.");
                foreach(var button in repeat.transform.parent.GetComponentsInChildren<Button>())
                    foreach(var neighbor in new[]{button.FindSelectableOnUp(),button.FindSelectableOnDown(),button.FindSelectableOnLeft(),button.FindSelectableOnRight()})
                        Assert.That(neighbor.transform.IsChildOf(repeat.transform.parent),Is.True,"Every navigation edge must stay in the modal.");
                Assert.That(host.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Study matrix").text,Does.Not.Contain("/27"));
                editor.Undo();Assert.That(hud.Draft,Is.SameAs(draft),"Undo must not edit the draft behind a comparison.");
                editor.Open(original,0);Assert.That(editor.Study.IsOpen,Is.False);Assert.That(editor.Study.Report,Is.Null);
                editor.Close();
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test] public void StudyIsReachableRunsWithoutSpendingAndSelectedFormCanBeSavedAsVariant()
        {
            var host=new GameObject("Isolated shape study");host.SetActive(false);
            try
            {
                var run=host.AddComponent<RunController>();run.InitializeWorkshop(false,allowLegacyFixtureDefaults:true);
                var hud=host.AddComponent<WorkshopHud>();hud.Initialize(run);
                var original=run.Workshop.Loadout[3];var before=hud.Draft;
                int gold=run.Workshop.Gold,ink=run.Session.Ink,album=run.Workshop.Album.Count;long tick=run.Session.TickIndex;
                hud.MortarFootprintEditor.Open(original,0);
                var open=host.GetComponentsInChildren<Button>(true).SingleOrDefault(b=>b.name=="Footprint study");
                Assert.That(open,Is.Not.Null,"Stage three must be reachable from the footprint editor.");open.onClick.Invoke();
                Assert.That(hud.Draft,Is.SameAs(before));
                host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Study run").onClick.Invoke();
                Assert.That(host.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Study matrix").text,Does.Contain("/27"));
                for(int scenario=0;scenario<3;scenario++){hud.MortarFootprintEditor.Study.SelectScenario((FootprintStudyScenario)scenario);hud.MortarFootprintEditor.Study.StartReplay();}
                var matrix=host.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Study matrix");
                Assert.That(matrix.preferredHeight,Is.LessThanOrEqualTo(matrix.rectTransform.rect.height),"All three result rows must remain visible.");
                Assert.That(run.Workshop.Gold,Is.EqualTo(gold));Assert.That(run.Session.Ink,Is.EqualTo(ink));Assert.That(run.Session.TickIndex,Is.EqualTo(tick));
                Assert.That(run.Workshop.Album.Count,Is.EqualTo(album));Assert.That(run.Workshop.Loadout[3],Is.SameAs(original));
                host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Study use 2").onClick.Invoke();
                Assert.That(hud.Draft.FirstFootprint.IsContour,Is.True);Assert.That(hud.Draft.Drawing.GeometryHash,Is.EqualTo(original.Drawing.GeometryHash));
                hud.MortarFootprintEditor.Close();
                host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Save blueprint as new").onClick.Invoke();
                Assert.That(hud.Draft.Id,Is.Not.EqualTo(original.Id));Assert.That(run.Workshop.Album.Any(p=>p.Id==hud.Draft.Id),Is.True);
                Assert.That(original.FirstFootprint.IsCircle,Is.True);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
