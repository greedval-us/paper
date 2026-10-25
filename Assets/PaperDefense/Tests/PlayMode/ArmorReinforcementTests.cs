using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class ArmorReinforcementTests
    {
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
