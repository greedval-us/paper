#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AlbumUiTests
    {
        GameObject canvas;
        AlbumPanel Panel(AssemblyCommandFixture fixture, Action<AlbumEntry> edit = null, Action assigned = null, int? slot = null)
        {
            canvas = new GameObject("Isolated modern album", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1024, 768);
            var panel = new AlbumPanel(canvas.transform, fixture.Run, TextResourcesAsset.Default, edit, () => { }, assigned, slot);
            panel.Reflow(1024, 768); return panel;
        }
        Button Button(string name) => canvas.GetComponentsInChildren<Button>().Single(button => button.name == name);
        int CardCount => canvas.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Album choose ", StringComparison.Ordinal));
        static string Diagnostic(AssemblyCommandResult result) => result.Code + " | " + string.Join("; ", result.Diagnostics.Select(issue => issue.Code + ":" + issue.Path + ":" + string.Join(",", issue.Arguments)));
        [TearDown] public void TearDown() { if (canvas != null) Object.DestroyImmediate(canvas); }

        [TestCase(AlbumFilter.All, 8)] [TestCase(AlbumFilter.Towers, 4)]
        [TestCase(AlbumFilter.Structures, 4)] [TestCase(AlbumFilter.Standard, 8)] [TestCase(AlbumFilter.Mine, 0)]
        public void FiltersUseTheWholeEntryAlbum(AlbumFilter filter, int expected)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Panel(fixture).SetFilter(filter);
                Assert.That(CardCount, Is.EqualTo(expected));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void EmptyMineFilterStillOffersTheStandardPages()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var panel = Panel(fixture); panel.SetFilter(AlbumFilter.Mine);
                Assert.That(canvas.GetComponentsInChildren<Text>().Any(text => text.name == "Album empty" && !string.IsNullOrWhiteSpace(text.text)), Is.True);
                Button("Album show standard").onClick.Invoke();
                Assert.That(CardCount, Is.EqualTo(8));
            }
        }

        [Test] public void FilteredCardEditsItsIdentityAndVariantBecomesSelectedByIdentity()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                AlbumEntry opened = null;
                var panel = Panel(fixture, entry => opened = entry); panel.SetFilter(AlbumFilter.Towers);
                Button("Album choose album.guard").onClick.Invoke(); Button("Album edit").onClick.Invoke();
                Assert.That(opened.Id, Is.EqualTo("album.guard"));
                Button("Album variant").onClick.Invoke();
                string savedId = panel.SelectedId;
                Assert.That(savedId, Is.Not.EqualTo("album.guard"));
                Assert.That(fixture.Run.Workshop.Entries.Single(entry => entry.Id == savedId).Name, Is.EqualTo(opened.Name));
                panel.SetFilter(AlbumFilter.Mine);
                Assert.That(CardCount, Is.EqualTo(1));
                Assert.That(Button("Album choose " + savedId), Is.Not.Null);
                fixture.Restart();
                Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == savedId), Is.True);
            }
        }

        [Test] public void DeleteVariantRequiresConfirmationAndClearsOnlyItsAssignedSlot()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var result = fixture.Commands.SaveEntryAsVariant(fixture.Gun(), Guid.NewGuid()); Assert.That(result.Success, Is.True);
                Assert.That(fixture.Commands.AssignSlot(7, result.ResultId, Guid.NewGuid()).Success, Is.True);
                var panel = Panel(fixture); panel.SetFilter(AlbumFilter.Mine);
                Button("Album choose " + result.ResultId).onClick.Invoke(); Button("Album delete").onClick.Invoke();
                Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == result.ResultId), Is.True);
                Button("Album confirm delete").onClick.Invoke();
                Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == result.ResultId), Is.False);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[7], Is.Null);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo("album.guard"));
                Assert.That(panel.SelectedId, Is.Null);
            }
        }

        [Test] public void DestinationPickerMovesAnExistingIdOnlyAfterConfirmation()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                int callbacks = 0; Panel(fixture, assigned: () => callbacks++, slot: 7);
                Button("Album choose album.guard").onClick.Invoke();
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo("album.guard"));
                Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo("album.generator"));
                Button("Album cancel move").onClick.Invoke();
                Assert.That(callbacks, Is.Zero);
                Button("Album choose album.guard").onClick.Invoke(); Button("Album confirm move").onClick.Invoke();
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0], Is.Null);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo("album.guard"));
                Assert.That(callbacks, Is.EqualTo(1));
            }
        }

        [Test] public void FailedAssignmentKeepsThePickerAndProfileUntouched()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                int callbacks = 0; var panel = Panel(fixture, assigned: () => callbacks++, slot: 7); string before = fixture.State();
                fixture.Store.RefuseWrites = true;
                Button("Album choose album.guard").onClick.Invoke(); Button("Album confirm move").onClick.Invoke();
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(callbacks, Is.Zero); Assert.That(panel.Root.gameObject.activeInHierarchy, Is.True);
                Assert.That(canvas.GetComponentsInChildren<Text>().Any(text => text.name == "Album status" && text.text.Contains("Injected assembly transaction write refusal")), Is.True);
            }
        }

        [Test] public void RestoreMissingStartersPreservesMoneyNamesAndSlotsAndPersistsOnce()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                Assert.That(fixture.Commands.DeleteEntry("album.wall", Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Commands.SaveEntry(fixture.Gun("album.guard", "Моя сохранённая страница"), Guid.NewGuid()).Success, Is.True);
                string beforeSlots = string.Join("|", fixture.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id ?? ""));
                var operation = Guid.NewGuid(); int calls = fixture.Store.SaveCalls;
                var restored = fixture.Commands.RestoreStarterEntries(operation);
                Assert.That(restored.Success, Is.True, Diagnostic(restored));
                var repeated = fixture.Commands.RestoreStarterEntries(operation);
                Assert.That(repeated.Success, Is.True, Diagnostic(repeated));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(calls + 1));
                Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(8));
                Assert.That(fixture.Run.Workshop.Entries.Single(entry => entry.Id == "album.guard").Name, Is.EqualTo("Моя сохранённая страница"));
                Assert.That(string.Join("|", fixture.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id ?? "")), Is.EqualTo(beforeSlots));
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94)); Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(390));
                fixture.Restart(); Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(8));
                Assert.That(fixture.SavedData.Operations.Single(r => r.OperationId == operation.ToString("N")).ResultId, Is.Null);
                var reopened = fixture.Commands.RestoreStarterEntries(operation);
                Assert.That(reopened.Success, Is.True, Diagnostic(reopened));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(calls + 1));
            }
        }

        [Test] public void FullAlbumRestoreReportsTheWholeSpaceDeficitWithoutPartialWrites()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390, data => {
                var source = data.Entries[0].CreateSnapshot();
                data.Entries = Enumerable.Range(0, 63).Select(index => new AlbumEntryData(new AlbumEntry("custom." + index, "Страница " + index, 1, source.Snapshot))).ToArray();
                data.Album = Array.Empty<BlueprintData>(); data.Loadout = new string[8];
            }))
            {
                string before = fixture.State(); var panel = Panel(fixture); Button("Album restore").onClick.Invoke();
                Assert.That(fixture.State(), Is.EqualTo(before)); Assert.That(fixture.Store.SaveCalls, Is.Zero);
                var result = fixture.Commands.RestoreStarterEntries(Guid.NewGuid());
                Assert.That(result.Code, Is.EqualTo("profile.entry.restore_space"));
                Assert.That(result.Diagnostics.Single().Arguments.Single(), Is.EqualTo("7"));
                Assert.That(canvas.GetComponentsInChildren<Text>().Single(text => text.name == "Album status").text, Does.Contain("7"));
            }
        }

        [Test] public void RefusedRestoreDoesNotPublishAndTheSameOperationCanBeRetried()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                Assert.That(fixture.Commands.DeleteEntry("album.repair", Guid.NewGuid()).Success, Is.True);
                string before = fixture.State(); byte[] saved = fixture.SavedBytes; var operation = Guid.NewGuid(); fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Commands.RestoreStarterEntries(operation).Code, Is.EqualTo("profile.write_failed"));
                Assert.That(fixture.State(), Is.EqualTo(before)); Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
                fixture.Store.RefuseWrites = false;
                var retried = fixture.Commands.RestoreStarterEntries(operation);
                Assert.That(retried.Success, Is.True, Diagnostic(retried));
                Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == "album.repair"), Is.True);
            }
        }

        [Test] public void NativeSubmitKeepsFilterControlAliveUntilItsHandlerFinishes()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Panel(fixture); var events = new GameObject("Isolated album events", typeof(EventSystem));
                try
                {
                    var button = Button("Album filter Mine"); EventSystem.current.SetSelectedGameObject(button.gameObject);
                    ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    Assert.That(CardCount, Is.Zero); Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Album filter Mine"));
                    UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                }
                finally { Object.DestroyImmediate(events); }
            }
        }

        [UnityTest] public IEnumerator KeyboardSelectionRevealsTheLastCardInsideTheCatalogue()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Panel(fixture); var events = new GameObject("Isolated album scroll events", typeof(EventSystem));
                try
                {
                    Canvas.ForceUpdateCanvases();
                    var last = Button("Album choose album.generator").GetComponent<RectTransform>();
                    EventSystem.current.SetSelectedGameObject(last.gameObject);
                    yield return null; Canvas.ForceUpdateCanvases();
                    var scroll = canvas.GetComponentsInChildren<ScrollRect>().Single();
                    var corners = new Vector3[4]; last.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = scroll.viewport.InverseTransformPoint(corner);
                        Assert.That(point.y, Is.InRange(scroll.viewport.rect.yMin - 1, scroll.viewport.rect.yMax + 1));
                    }
                    ExecuteEvents.Execute(last.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    last = Button("Album choose album.generator").GetComponent<RectTransform>();
                    scroll = canvas.GetComponentsInChildren<ScrollRect>().Single();
                    last.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = scroll.viewport.InverseTransformPoint(corner);
                        Assert.That(point.y, Is.InRange(scroll.viewport.rect.yMin - 1, scroll.viewport.rect.yMax + 1));
                    }
                }
                finally { Object.DestroyImmediate(events); }
            }
        }
    }
}
#endif
