using System.Collections;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class BootstrapProfileIsolationTests
    {
        GameObject root;
        GameBalanceAsset balance;
        PaperDefenseBootstrap bootstrap;
        CountingStore store;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach(var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            root=new GameObject("Isolated launch arguments");root.SetActive(false);
            balance=ScriptableObject.CreateInstance<GameBalanceAsset>();balance.ResetToDefaults();balance.planningSeconds=1000;
            bootstrap=root.AddComponent<PaperDefenseBootstrap>();bootstrap.balance=balance;
            var camera=new GameObject("Isolated launch camera",typeof(Camera));camera.transform.SetParent(root.transform);
            bootstrap.worldCamera=camera.GetComponent<Camera>();
            store=new CountingStore(balance.CreateWorkshopBalance().ResolveContent());bootstrap.ProfileStoreOverride=store;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {Object.Destroy(root);Object.Destroy(balance);yield return null;}

        [TestCase(false,"-paper-memory-only",true)]
        [TestCase(false,"-paper-pressure",false)]
        [TestCase(false,"-paper-pressure-report",false)]
        [TestCase(false,"--paper-pressure",false)]
        [TestCase(false,"ordinary",false)]
        [TestCase(true,"ordinary",true)]
        public void ExactMemoryOnlyFlagOrConfiguredIsolationSelectMemoryOnly(bool configured,string argument,bool expected)
        {
            Assert.That(PaperDefenseBootstrap.ShouldUseMemoryOnlyProfile(configured,new[]{"PaperDefense.exe",argument}),Is.EqualTo(expected));
        }

        [Test] public void MissingArgumentsPreserveOnlyTheConfiguredIsolation()
        {
            Assert.That(PaperDefenseBootstrap.ShouldUseMemoryOnlyProfile(false,null),Is.False);
            Assert.That(PaperDefenseBootstrap.ShouldUseMemoryOnlyProfile(true,null),Is.True);
        }

        [TestCase("-paper-memory-only")]
        public void MemoryOnlyFlagIsAppliedDuringAwakeBeforeAnyProfileStoreLoad(string argument)
        {
            bootstrap.memoryOnlyProfile=false;bootstrap.ArgumentProvider=()=>new[]{"PaperDefense.exe",argument};
            root.SetActive(true);
            Assert.That(store.LoadCalls,Is.Zero,"Validation must choose MemoryOnly before asking a profile store to load.");
            Assert.That(store.SaveCalls,Is.Zero);
            Assert.That(bootstrap.Controller.Workshop.SaveState,Is.EqualTo(ProfileSaveState.MemoryOnly));
            bootstrap.Controller.gameObject.SendMessage("OnApplicationQuit",SendMessageOptions.RequireReceiver);
            Assert.That(store.SaveCalls,Is.Zero);
        }

        [Test] public void InspectorIsolationAlsoBypassesTheInjectedStoreDuringAwake()
        {
            bootstrap.memoryOnlyProfile=true;bootstrap.ArgumentProvider=()=>new[]{"PaperDefense.exe"};
            root.SetActive(true);
            Assert.That(store.LoadCalls,Is.Zero);Assert.That(bootstrap.Controller.Workshop.SaveState,Is.EqualTo(ProfileSaveState.MemoryOnly));
        }

        [TestCase("-pressure-report")]
        [TestCase("-paper-pressure")]
        public void OrdinaryLaunchStillLoadsItsConfiguredProfileStoreExactlyOnce(string argument)
        {
            bootstrap.memoryOnlyProfile=false;bootstrap.ArgumentProvider=()=>new[]{"PaperDefense.exe",argument,"isolated.json"};
            root.SetActive(true);
            Assert.That(store.LoadCalls,Is.EqualTo(1));Assert.That(bootstrap.Controller.Workshop.SaveState,Is.EqualTo(ProfileSaveState.Saved));
            Assert.That(bootstrap.Controller.Workshop.Gold,Is.EqualTo(321));
        }

        sealed class CountingStore:IWorkshopProfileStore
        {
            readonly ContentCatalogSnapshot content;
            internal CountingStore(ContentCatalogSnapshot content){this.content=content;}
            internal int LoadCalls,SaveCalls;
            public ProfileLoadResult Load()
            {
                LoadCalls++;var profile=new WorkshopProfile();profile.GrantGold(321);
                // Model a profile already saved with this catalog's permanent initial grants.
                profile.ConfigureContent(content);
                return new ProfileLoadResult(profile,ProfileLoadSource.Primary,"Only an injected in-memory store",true);
            }
            public ProfileSaveResult Save(WorkshopProfile profile)
            {SaveCalls++;return new ProfileSaveResult(true,"Only an injected in-memory write");}
        }
    }
}
