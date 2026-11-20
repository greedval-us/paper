#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed partial class CurrentVisualCaptureTests
    {
        [UnityTest, Explicit("Captures the funded prism offer on the production workshop page.")]
        public IEnumerator CapturePrismStructureOffer()
        {
            using (var buyer = new AssemblyCommandFixture(gold: 160, xp: 230))
            {
                game.Run.InitializeWorkshop(buyer.Store);
                game.EditAlbumEntry(null);
                Press("Composite route structure");
                foreach (var size in Sizes)
                {
                    shell.ApplyLayout(size.x, size.y);
                    ((RectTransform)Workshop.Root.parent).sizeDelta = size;
                    Workshop.Reflow(size.x, size.y);
                    Press("Structure family Prism");
                    Find<ScrollRect>("Structure families").verticalNormalizedPosition = 0;
                    EventSystem.current.SetSelectedGameObject(null);
                    Assert.That(Find<Button>("Structure purchase family").IsInteractable(), Is.True);
                    Capture("structure-prism-offer", size);
                }
                yield return null;
            }
        }

        [UnityTest, Explicit("Captures compact album comparisons and workshop part consequences using frozen production recipes.")]
        public IEnumerator CaptureCompactWorkshopAlbum()
        {
            captures.Clear();
            for (int index = 0; index < Sizes.Length; index++)
            {
                if (index > 0) { yield return TearDown(); yield return SetUp(); }
                var size = Sizes[index]; shell.ApplyLayout(size.x, size.y);
                game.OpenAlbum();
                var search = Find<AlbumSearchInput>("Album search");
                EventSystem.current.SetSelectedGameObject(search.gameObject);
                search.ActivateInputField();
                search.text = "дозор";
                Capture("album-search", size);
                search.text = "";
                search.DeactivateInputField();
                shell.Album.SelectEntry("album.guard");
                Press("Album compare"); shell.Album.SelectEntry("album.double");
                Capture("album-comparison", size);
                var comparisonScroll = Find<ScrollRect>("Album inspector scroll");
                comparisonScroll.verticalNormalizedPosition = 0;
                Capture("album-comparison-weapons", size);
                shell.Album.SetFilter(AlbumFilter.Structures);
                shell.Album.SelectEntry(game.Run.Workshop.LoadoutEntries[4].Id);
                Capture("album-comparison-mixed", size);
                var compare = Find<Button>("Album compare");
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(compare.gameObject, new BaseEventData(EventSystem.current));
                yield return new WaitForSecondsRealtime(.18f);
                Capture("album-comparison-keyboard", size);
                EventSystem.current.SetSelectedGameObject(null);

                game.EditAlbumEntry(null);
                ((RectTransform)Workshop.Root.parent).sizeDelta = size; Workshop.Reflow(size.x, size.y);
                Press("Sequence next"); Press("Sequence category Head"); Press("Sequence inspect head.long");
                Capture("workshop-part-comparison", size);
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(Find<Button>("Sequence inspect head.long").gameObject,
                    new BaseEventData(EventSystem.current));
                yield return new WaitForSecondsRealtime(.18f);
                Capture("workshop-part-keyboard", size);
                EventSystem.current.SetSelectedGameObject(null);
                Press("Composite route structure"); Press("Structure step 0"); Press("Structure next");
                Press("Structure category Shell"); Press("Structure inspect shell.light");
                Capture("workshop-structure-comparison", size);
                Press("Structure candidate details"); Capture("workshop-structure-details", size);
                Press("Close context help");
                Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            }
            WriteManifest("compact-workshop-manifest.json");
        }
    }
}
#endif
