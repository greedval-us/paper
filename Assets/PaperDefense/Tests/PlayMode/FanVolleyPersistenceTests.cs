using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
namespace PaperDefense.Tests
{
    public sealed class FanVolleyPersistenceTests
    {
        static TowerBlueprint Page()=>new TowerBlueprint("directions","Directions",ChassisKind.Standard,StructureKind.Fan)
            .WithFanVolley(0,new FanVolley(new[]{new FanGuide(new[]{default(Point2),new Point2(.5f,1),new Point2(0,2)})}));
        [Test] public void RealProfileStoreRetainsAuthoredGuideAndFutureVersionsProtectBothFiles()
        {
            string directory=Path.Combine(Path.GetTempPath(),"PaperVolleyTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                string path=Path.Combine(directory,"profile.json");var profile=new WorkshopProfile();
                profile.GrantGold(100);Assert.That(profile.Unlock("weapon.fan",out _),Is.True);
                Assert.That(profile.SaveBlueprint(Page(),out string reason),Is.True,reason);
                Assert.That(new WorkshopProfileStore(path).Save(profile).Success,Is.True);
                var loaded=new WorkshopProfileStore(path).Load();Assert.That(loaded.Profile.Album.Single(p=>p.Id=="directions").GetFanVolley(0).Guides[0].Points[1].X,Is.EqualTo(.5f));
                var data=profile.ToData();var page=data.Album.Single(p=>p.Id=="directions");
                Assert.That(page.WeaponMountMetadataVersion,Is.EqualTo(2));
                string future=JsonUtility.ToJson(data).Replace("\"VolleyVersion\":1","\"VolleyVersion\":99");
                Assert.That(future,Does.Contain("\"VolleyVersion\":99"));
                File.WriteAllText(path,future);File.WriteAllText(path+".bak",JsonUtility.ToJson(new WorkshopProfile().ToData()));
                byte[] primary=File.ReadAllBytes(path),backup=File.ReadAllBytes(path+".bak");var store=new WorkshopProfileStore(path);var recovery=store.Load();
                Assert.That(recovery.CanWrite,Is.False);Assert.That(store.Save(recovery.Profile).Success,Is.False);
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(path+".bak"),Is.EqualTo(backup));
            }
            finally
            {
                string full=Path.GetFullPath(directory);Assert.That(full.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase),Is.True);
                Assert.That(Path.GetFileName(full).StartsWith("PaperVolleyTests-"),Is.True);Directory.Delete(full,true);
            }
        }
        [Test] public void CopiesPreserveOnlyUnchangedMountsAndOldPagesStayLegacy()
        {
            var page=Page();Assert.That(new BlueprintData(page).ToBlueprint().GetFanVolley(0),Is.Not.Null);
            var alternative=new FanVolley(new[]{FanGuide.FromAngle(20)});
            Assert.That(page.WithAssembly(page.ChassisId,new[]{new WeaponMountSelection(0,page.FirstWeaponId,volley:alternative)},page.ModuleSelections).GetFanVolley(0),Is.SameAs(alternative));
            Assert.That(page.WithName("New").AsVariant("v","V").WithAssembly(ChassisKind.Heavy,StructureKind.Fan,StructureKind.Gun,TowerModules.None).GetFanVolley(0),Is.SameAs(page.GetFanVolley(0)));
            Assert.That(page.WithWeaponMount(0,"weapon.gun").WithWeaponMount(0,"weapon.fan").GetFanVolley(0),Is.Null);
            Assert.That(new BlueprintData(new TowerBlueprint("old","Old",ChassisKind.Standard,StructureKind.Fan)).ToBlueprint().GetFanVolley(0),Is.Null);
        }
    }
}
