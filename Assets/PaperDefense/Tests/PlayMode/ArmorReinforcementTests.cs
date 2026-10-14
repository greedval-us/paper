using System;
using System.IO;
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
    public sealed class ArmorReinforcementTests
    {
        [Test] public void ABrushGestureSpendsOncePerSectorAndPendingUndoDoesNotLoseAcceptedWork()
        {
            var host=new GameObject("Armor pointer",typeof(RectTransform),typeof(Canvas));
            try
            {
                var rules=GameBalance.CreateDefault();rules.Construction.ArmorDefense=.25f;
                var draft=new TowerBlueprint("armor-input","Armor",ChassisKind.Standard,StructureKind.Gun,modules:TowerModules.Armor);
                var panel=new ArmorReinforcementPanel(host.transform,Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),value=>draft=value,
                    (bp,side)=>ArmorProbe.Run(bp,rules,side),bp=>TowerCatalog.Defense(bp,rules),rules.Effects.MaximumDefense);
                panel.Open(draft);Canvas.ForceUpdateCanvases();
                var pointer=new PointerEventData(null){pointerId=2,button=PointerEventData.InputButton.Left,position=panel.Canvas.LocalPointToScreen(new Point2(0,1))};
                panel.Canvas.OnPointerDown(pointer);Assert.That(panel.Canvas.HasGesture,Is.True);Assert.That(panel.Recording,Is.Null);
                for(int i=0;i<20;i++)panel.Canvas.OnDrag(pointer);
                Assert.That(host.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Armor budget").text,Does.Contain("11"),"Repeated movement must keep the pending stock preview.");
                pointer.position=panel.Canvas.LocalPointToScreen(new Point2(1,0));panel.Canvas.OnDrag(pointer);panel.Canvas.OnPointerUp(pointer);
                Assert.That(draft.Reinforcement.Points,Is.EqualTo(new[]{1,1,0,0}));Assert.That(draft.Reinforcement.Remaining,Is.EqualTo(10));Assert.That(panel.Recording,Is.Not.Null);
                var accepted=draft;panel.Canvas.OnPointerDown(pointer);panel.Undo();
                Assert.That(draft,Is.SameAs(accepted));Assert.That(panel.Canvas.HasGesture,Is.False);
                panel.Undo();Assert.That(draft.Reinforcement,Is.Null);
                panel.Canvas.OnPointerDown(pointer);panel.Close();panel.Canvas.OnPointerUp(pointer);
                Assert.That(draft.Reinforcement,Is.Null);Assert.That(panel.Recording,Is.Null);
                panel.Open(accepted);pointer.button=PointerEventData.InputButton.Right;
                panel.Canvas.OnPointerDown(pointer);panel.Canvas.OnPointerUp(pointer);
                Assert.That(draft.Reinforcement.Points,Is.EqualTo(new[]{1,0,0,0}));
                panel.Rotate();Assert.That(draft.Reinforcement.FacingQuarterTurns,Is.EqualTo(1));
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }
        [Test] public void WorkshopOffersASeparateReinforcementEditor()
        {
            var host=new GameObject("Armor workshop");host.SetActive(false);
            try
            {
                var run=host.AddComponent<RunController>();run.InitializeWorkshop(false,allowLegacyFixtureDefaults:true);
                var hud=host.AddComponent<WorkshopHud>();hud.Initialize(run);
                var button=host.GetComponentsInChildren<Button>(true).SingleOrDefault(b=>b.name=="Open reinforcement");
                Assert.That(button,Is.Not.Null,"Armor needs its own functional editor entry.");
                if(!hud.Draft.HasArmor)host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Module Armor").onClick.Invoke();
                int gold=run.Workshop.Gold,ink=run.Session.Ink;button.onClick.Invoke();
                Assert.That(hud.ReinforcementEditor.IsOpen,Is.True);
                host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Armor front").onClick.Invoke();
                Assert.That(hud.Draft.Reinforcement.Points,Is.EqualTo(new[]{12,0,0,0}));
                Assert.That(run.Workshop.Gold,Is.EqualTo(gold));Assert.That(run.Session.Ink,Is.EqualTo(ink));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
        [Test] public void RealProfileRoundtripKeepsArmorAndFutureVersionProtectsBothFiles()
        {
            string directory=Path.Combine(Path.GetTempPath(),"PaperArmorTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                string path=Path.Combine(directory,"profile.json");var profile=new WorkshopProfile();
                var page=new TowerBlueprint("armor-direction","Armor",ChassisKind.Standard,StructureKind.Gun,modules:TowerModules.Armor)
                    .WithReinforcement(new ArmorDistribution(new[]{6,3,0,3},3));
                Assert.That(profile.SaveBlueprint(page,out string reason),Is.True,reason);var store=new WorkshopProfileStore(path);
                Assert.That(store.Save(profile).Success,Is.True);var loaded=store.Load().Profile.Album.Single(p=>p.Id==page.Id);
                Assert.That(loaded.Reinforcement.Points,Is.EqualTo(page.Reinforcement.Points));Assert.That(loaded.Reinforcement.FacingQuarterTurns,Is.EqualTo(3));
                var data=profile.ToData();data.Album.Single(p=>p.Id==page.Id).Reinforcement.Version=99;
                File.WriteAllText(path,JsonUtility.ToJson(data));File.WriteAllText(path+".bak",JsonUtility.ToJson(new WorkshopProfile().ToData()));
                byte[] primary=File.ReadAllBytes(path),backup=File.ReadAllBytes(path+".bak");var recovery=store.Load();
                Assert.That(recovery.CanWrite,Is.False);Assert.That(store.Save(recovery.Profile).Success,Is.False);
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(path+".bak"),Is.EqualTo(backup));
            }
            finally
            {
                string full=Path.GetFullPath(directory);Assert.That(full.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase),Is.True);
                Assert.That(Path.GetFileName(full).StartsWith("PaperArmorTests-"),Is.True);Directory.Delete(full,true);
            }
        }
    }
}
