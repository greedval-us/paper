#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AlbumReleaseLifecycleTests
    {
        AssemblyCommandFixture fixture;
        GameObject root;
        PlayerGameRoot game;

        [UnitySetUp] public IEnumerator SetUp()
        {
            fixture = new AssemblyCommandFixture();
            root = new GameObject("Album release lifecycle player"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(fixture.BalanceAsset, true);
            root.SetActive(true);
            Assert.That(game.Run.AutomaticSimulation, Is.True, "Exercise the live player update and simulation lifecycle.");
            yield return Settle();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
            fixture?.Dispose();
        }

        [UnityTest] public IEnumerator RepeatedFullPlayerCycleDoesNotRetainSettledSceneObjects()
        {
            // Two real warmups match the built-player release probe. No UI, simulation,
            // allocation counter, or object-count tolerance is replaced by the fixture.
            yield return FullCycle(); yield return FullCycle();
            var before = SceneObjectHistogram();
            yield return FullCycle();
            var after = SceneObjectHistogram();
            AssertNoGrowth(before, after);
        }

        [UnityTest] public IEnumerator ClosingAnAlbumReleasesItsTooltipObjectsWithThePage()
        {
            // Album keyboard focus creates an event-driven hint. Replacing the page must
            // release that hint even though the outer player Canvas remains alive.
            Click("Open album"); Click("Album close"); yield return Settle();
            var before = SceneObjectHistogram();
            Click("Open album"); Click("Album close"); yield return Settle();
            AssertNoGrowth(before, SceneObjectHistogram());
        }

        IEnumerator FullCycle()
        {
            Click("Open album"); Click("Album filter All"); Click("Album choose album.guard"); Click("Album edit");
            Click("Composite library"); Click("Composite library back"); Click("Composite step 2");
            Click("Composite trial"); Click("Composite trial run");
            Assert.That(game.GetComponentInChildren<WorkshopHud>(true).Composite.LastAssemblyTrial, Is.Not.Null);
            yield return Settle();
            Click("Composite close trial"); Click("Composite return"); Click("Album close");
            Click("Open campaign"); Click("Prepare mission"); Click("Start battle"); Click("Launch wave");
            yield return Settle();
            Click("Battle pause"); Click("Leave battle"); Click("Confirm action"); Click("Back");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Assert.That(game.Run.InWorkshop, Is.True);
            yield return Settle();
        }

        static IEnumerator Settle() { for (int i = 0; i < 30; i++) yield return null; }

        void Click(string name)
        {
            var button = root.GetComponentsInChildren<Button>().Single(control => control.name == name && control.isActiveAndEnabled);
            Assert.That(button.IsInteractable(), Is.True, name);
            Assert.That(EventSystem.current, Is.Not.Null);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        Dictionary<string, int> SceneObjectHistogram()
        {
            // Include inactive hints and every scene object owned by this player, rather
            // than comparing only the current page or only active objects.
            return Resources.FindObjectsOfTypeAll<Transform>()
                .Where(item => item.gameObject.scene.IsValid() && item.IsChildOf(root.transform))
                .GroupBy(item => Hierarchy(item) + " [" + string.Join(",", item.GetComponents<Component>()
                    .Select(component => component == null ? "<missing>" : component.GetType().FullName).OrderBy(name => name)) + "]")
                .ToDictionary(group => group.Key, group => group.Count());
        }

        static string Hierarchy(Transform item)
        {
            var parts = new Stack<string>();
            while (item != null) { parts.Push(item.name); item = item.parent; }
            return string.Join("/", parts);
        }

        static void AssertNoGrowth(Dictionary<string, int> before, Dictionary<string, int> after)
        {
            string[] growth = after.Select(pair => new { pair.Key, Before = before.TryGetValue(pair.Key, out int count) ? count : 0, After = pair.Value })
                .Where(item => item.After > item.Before).OrderBy(item => item.Key)
                .Select(item => item.Before + " -> " + item.After + " " + item.Key).ToArray();
            Assert.That(growth, Is.Empty, "Settled scene objects retained by the player:\n" + string.Join("\n", growth));
        }
    }
}
#endif
