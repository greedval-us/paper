using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace PaperDefense.Tests
{
    public sealed class DesignChangeFeedbackTests
    {
        GameObject root; Font font; GameBalance rules;
        [SetUp] public void SetUp()
        { root=new GameObject("Isolated change feedback",typeof(RectTransform),typeof(Canvas));font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");rules=GameBalance.CreateDefault(); }
        [TearDown] public void TearDown(){UnityEngine.Object.DestroyImmediate(root);}
        Text Label(string name)=>root.GetComponentsInChildren<Text>(true).SingleOrDefault(t=>t.name==name);
        void Click(string name)=>root.GetComponentsInChildren<Button>(true).Single(b=>b.name==name).onClick.Invoke();
        [Test] public void DrawingKeepsOpeningNumbersAcrossTwoEditsUndoAndReopen()
        {
            var controller=new GameObject("Isolated comparison controller");controller.transform.SetParent(root.transform);controller.SetActive(false);
            var run=controller.AddComponent<RunController>();run.InitializeWorkshop(false,allowLegacyFixtureDefaults:true);
            var initial=new TowerBlueprint("drawing-change","Drawing",ChassisKind.Standard,StructureKind.Gun).AsFreeform();
            var session=new DrawingEditSession(initial,initial.Drawing);
            session.AddRectangle(DrawingBindings.Base,new DrawingPoint(-.35f,-.35f),new DrawingPoint(.35f,.35f),out _);
            session.AddRectangle(DrawingBindings.Mount0,new DrawingPoint(-.0625f,0),new DrawingPoint(.0625f,.5f),out _);
            initial=initial.WithFreeformDrawing(session.Snapshot);
            var panel=new DrawingEditorPanel(root.transform,font,_=>{},context:new DrawingEditorContext{Preview=p=>run.PreviewBlueprint(p)});
            panel.Open(initial);panel.Canvas.SelectedBinding=DrawingBindings.Mount0;
            var label=Label("Drawing selected mount stats");Assert.That(label.text,Does.Contain("→"));string original=label.text;
            Click("Drawing height larger");string once=label.text;Click("Drawing height larger");
            Assert.That(label.text,Is.Not.EqualTo(once));panel.Canvas.Undo();Assert.That(label.text,Is.EqualTo(once));
            panel.Canvas.ShowGrid=false;Assert.That(label.text,Is.EqualTo(once));panel.Canvas.Undo();Assert.That(label.text,Is.EqualTo(original));
            Click("Drawing height larger");var edited=panel.Session.Blueprint.WithFreeformDrawing(panel.Session.Snapshot);
            panel.Close(false);panel.Open(edited);panel.Canvas.SelectedBinding=DrawingBindings.Mount0;
            Assert.That(label.text,Is.Not.EqualTo(once));Assert.That(initial.Drawing.GeometryHash,Is.Not.EqualTo(edited.Drawing.GeometryHash));
        }
        [Test] public void VolleyComparisonKeepsOpeningStateThroughSelectionAndUndo()
        {
            var bp=new TowerBlueprint("fan-change","Fan",ChassisKind.Standard,StructureKind.Fan);
            var panel=new FanVolleyPanel(root.transform,font,_=>{},(p,m,f)=>FanVolleyProbe.Run(p,m,f,rules));
            panel.Open(bp,0);var label=Label("Volley change comparison");Assert.That(label,Is.Not.Null);
            string original=label.text;panel.Preset(3);string changed=label.text;
            Assert.That(changed,Is.Not.EqualTo(original));Assert.That(changed,Does.Contain("6"));
            Click("Volley select 0");panel.Record();Assert.That(label.text,Is.EqualTo(changed));
            panel.Undo();Assert.That(label.text,Is.EqualTo(original));
        }
        [Test] public void ArmorComparisonTracksWorldDirectionAndResetOnReopen()
        {
            rules.Construction.ArmorDefense=.25f;rules.Effects.MaximumDefense=.6f;
            var bp=new TowerBlueprint("armor-change","Armor",ChassisKind.Standard,StructureKind.Gun,modules:TowerModules.Armor);
            var draft=bp;
            var panel=new ArmorReinforcementPanel(root.transform,font,p=>draft=p,(p,s)=>ArmorProbe.Run(p,rules,s),p=>TowerCatalog.Defense(p,rules),rules.Effects.MaximumDefense);
            panel.Open(bp);var label=Label("Armor change comparison");Assert.That(label,Is.Not.Null);
            string original=label.text;Click("Armor front");string front=label.text;
            panel.Rotate();Assert.That(label.text,Is.Not.EqualTo(front));
            panel.Undo();Assert.That(label.text,Is.EqualTo(front));panel.Undo();Assert.That(label.text,Is.EqualTo(original));
            Click("Armor front");panel.Open(draft);Assert.That(label.text,Does.Contain("60% → 60%"));
        }
        [Test] public void FootprintComparisonUsesPinnedAAndRejectsInvalidResult()
        {
            var bp=new TowerBlueprint("mortar-change","Mortar",ChassisKind.Standard,StructureKind.Mortar);
            var panel=new MortarFootprintPanel(root.transform,font,_=>{},(p,m,a,b,f)=>MortarFootprintProbe.Compare(p,m,a,b,rules,f),_=>null);
            panel.Open(bp,0);var label=Label("Footprint change comparison");Assert.That(label,Is.Not.Null);
            string original=label.text;Click("Footprint longitudinal");string changed=label.text;Assert.That(changed,Is.Not.EqualTo(original));
            panel.PinBaseline();Assert.That(label.text,Is.Not.EqualTo(changed));
            panel.SetContour(new[]{new Point2(-1,0),new Point2(1,0),new Point2(0,0)});
            Assert.That(panel.Recording,Is.Null);Assert.That(label.text,Does.Not.Contain("→"));
            panel.Undo();Assert.That(label.text,Does.Contain("→"));
        }
        [Test] public void ScalingTheAuthoredContourDoesNotChangeTheComparedWorkingSize()
        {
            var shape=new[]{new Point2(-.5f,-1),new Point2(.5f,-1),new Point2(.5f,1),new Point2(-.5f,1)};
            var scaled=shape.Select(p=>new Point2(p.X*1.5f,p.Y*1.5f)).ToArray();
            string first=TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Footprint(MortarFootprint.Circle,MortarFootprint.FromContour(shape),2.4f));
            string second=TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Footprint(MortarFootprint.Circle,MortarFootprint.FromContour(scaled),2.4f));
            Assert.That(second,Is.EqualTo(first));Assert.That(first,Does.Contain("18,1"));
        }
        [Test] public void ArmorPendingComparisonUsesTheCapAndCancelRestoresTheAcceptedNumbers()
        {
            rules.Construction.ArmorDefense=.25f;rules.Effects.MaximumDefense=.6f;
            var initial=new TowerBlueprint("pending-change","Armor",ChassisKind.Standard,StructureKind.Gun,modules:TowerModules.Armor)
                .WithReinforcement(new ArmorDistribution(new[]{6,0,0,0}));var draft=initial;
            var panel=new ArmorReinforcementPanel(root.transform,font,p=>draft=p,(p,s)=>ArmorProbe.Run(p,rules,s),p=>TowerCatalog.Defense(p,rules),rules.Effects.MaximumDefense);
            panel.Open(initial);Canvas.ForceUpdateCanvases();string original=Label("Armor change comparison").text;
            var pointer=new PointerEventData(null){pointerId=9,button=PointerEventData.InputButton.Left,position=panel.Canvas.LocalPointToScreen(new Point2(0,1))};
            panel.Canvas.OnPointerDown(pointer);
            Assert.That(Label("Armor change comparison").text,Does.Contain("50% → 58,3%"));
            Assert.That(Label("Armor change comparison").text,Does.Contain("предварительный"));Assert.That(draft,Is.SameAs(initial));
            panel.Canvas.CancelGesture();Assert.That(Label("Armor change comparison").text,Is.EqualTo(original));
            string capped=TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Armor(new ArmorDistribution(new[]{8,0,0,0}),new ArmorDistribution(new[]{12,0,0,0}),.25f,.6f));
            Assert.That(capped,Does.Contain("60% → 60%"));Assert.That(capped,Does.Contain("без изменений"));
        }
        [Test] public void VolleyComparisonReportsDistributionWithoutInventingNewPellets()
        {
            var definition=rules.ResolveContent().GetWeapon("weapon.fan");
            var a=new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(30)});
            var b=new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(0),FanGuide.FromAngle(30)});
            string message=TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Volley(a,b,definition));
            Assert.That(message,Does.Contain("3+3 → 2+2+2"));Assert.That(message,Does.Contain("всего 6 снарядов"));
            Assert.That(TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Volley(null,b,definition)),Does.Contain("Новый режим"));
            var bent=new FanVolley(new[]{new FanGuide(new[]{new Point2(0,0),new Point2(.7f,.5f),new Point2(0,2)})});
            Assert.That(TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Volley(new FanVolley(new[]{FanGuide.FromAngle(0)}),bent,definition)),Does.Contain("без изменений"));
        }
        [Test] public void SavedVolleyWithReducedPelletBudgetCanStillBeOpenedAndRepaired()
        {
            var input=LegacyContentCatalog.CreateInput(rules);input.Weapons.Single(w=>w.Id=="weapon.fan").VolleyPellets=2;
            foreach(var item in input.Chassis)item.TemplateId="template.change";
            foreach(var item in input.Weapons)item.TemplateId="template.change";
            foreach(var item in input.Modules)item.TemplateId="template.change";
            input.Templates.Add(new DrawingTemplateDefinitionInput{Id="template.change",DisplayName="Sample",Weight=25,
                Contours=new List<DrawingContourInput>{new DrawingContourInput{Points=new List<DrawingCoordinateInput>{new DrawingCoordinateInput(-.1f,0),new DrawingCoordinateInput(.1f,.4f)}}}});
            var content=input.CreateSnapshot();rules.Content=content;
            var saved=new TowerBlueprint("old-volley","Old",ChassisKind.Standard,StructureKind.Fan)
                .WithFanVolley(0,new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(0),FanGuide.FromAngle(30)})).WithContent(content);
            var draft=saved;
            var panel=new FanVolleyPanel(root.transform,font,p=>draft=p,(p,m,f)=>FanVolleyProbe.Run(p,m,f,rules));
            Assert.DoesNotThrow(()=>panel.Open(saved,0));Assert.That(Label("Volley change comparison").text,Does.Contain("недоступно"));
            Assert.DoesNotThrow(()=>panel.Preset(2));Assert.That(draft.GetFanVolley(0).Guides.Count,Is.EqualTo(2));
            Assert.That(Label("Volley change comparison").text,Does.Contain("недоступно"),"An incompatible opening sample must not silently become the repaired draft.");
            panel.Open(draft,0);Assert.That(Label("Volley change comparison").text,Does.Contain("1+1 → 1+1"));
        }
    }
}
