#if UNITY_EDITOR
using System.Collections;
using System.Linq;
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
        [UnityTest, Explicit("Captures compact trial controls and real tower/support reports under equal production conditions.")]
        public IEnumerator CaptureCompactTrial()
        {
            captures.Clear();
            for (int index = 0; index < Sizes.Length; index++)
            {
                if (index > 0) { yield return TearDown(); yield return SetUp(); }
                var size = Sizes[index]; shell.ApplyLayout(size.x, size.y);
                OpenCapturedTrial(game.Run.Workshop.LoadoutEntries[0], size);
                Capture("range-ready", size);
                Press("Composite trial run"); Capture("range-results", size);
                Press("Composite trial pause"); Capture("range-paused", size);
                Press("Composite trial overview"); Capture("range-overview", size);
                Press("Composite trial help"); Capture("range-conditions", size);
                Press("Close context help");
                Press("Composite trial scenario Blots"); Press("Composite trial run");
                Capture("range-blots", size);
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(Find<Button>("Composite trial help").gameObject,
                    new BaseEventData(EventSystem.current));
                Canvas.ForceUpdateCanvases(); Capture("range-keyboard-hint", size);
                EventSystem.current.SetSelectedGameObject(null);
                Press("Composite trial header return");
                foreach (var family in new[] { AssemblyStructureFamily.Wall, AssemblyStructureFamily.Repair, AssemblyStructureFamily.Generator })
                {
                    var entry = game.Run.Workshop.Entries.First(item => item.Kind == AlbumEntryKind.Structure &&
                        item.Snapshot.Recipe.Structure.Family == family);
                    OpenCapturedTrial(entry, size); Press("Composite trial run");
                    Capture("range-" + family.ToString().ToLowerInvariant(), size);
                    Press("Composite trial header return");
                }
                Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            }
            WriteManifest("compact-trial-manifest.json");
        }

        void OpenCapturedTrial(AlbumEntry entry, Vector2Int size)
        {
            game.EditAlbumEntry(entry);
            ((RectTransform)Workshop.Root.parent).sizeDelta = size;
            Workshop.Reflow(size.x, size.y);
            Press("Composite trial");
        }
    }
}
#endif
