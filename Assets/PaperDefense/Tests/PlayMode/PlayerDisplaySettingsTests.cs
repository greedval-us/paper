using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class PlayerDisplaySettingsTests
    {
        [Test] public void ResolutionChoicesDeduplicateModesAndRetainTheCurrentWindowSize()
        {
            int requests = 0;
            var display = new PlayerDisplaySettings(new[] {
                new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                new Vector2Int(0, 720), new Vector2Int(1280, 720) }, new Vector2Int(1366, 768),
                (width, height, mode) => requests++);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1920, 1080) }, display.Resolutions);
            Assert.That(display.SelectedResolution, Is.EqualTo(new Vector2Int(1366, 768)));
            display.SelectRelative(-1);
            Assert.That(display.SelectedResolution, Is.EqualTo(new Vector2Int(1280, 720)));
            display.SelectRelative(-1);
            Assert.That(display.SelectedResolution, Is.EqualTo(new Vector2Int(1920, 1080)), "Choices wrap at either end.");
            display.CancelSelection();
            Assert.That(display.SelectedResolution, Is.EqualTo(new Vector2Int(1366, 768)));
            Assert.That(display.HasPendingChange, Is.False);
            Assert.That(requests, Is.Zero, "Browsing and cancelling do not change the monitor.");
        }

        [Test] public void EmptyOrInvalidNativeModesUseWindowSizesWithoutLosingTheCurrentSize()
        {
            var display = new PlayerDisplaySettings(new[] { new Vector2Int(0, 0), new Vector2Int(-1, 720) },
                new Vector2Int(1366, 768), (width, height, mode) => { });
            CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1366, 768),
                new Vector2Int(1600, 900), new Vector2Int(1920, 1080) }, display.Resolutions);
            Assert.That(display.SelectedResolution, Is.EqualTo(new Vector2Int(1366, 768)));
        }

        [Test] public void ApplyRequestsTheSelectedSizeOnceAndPreservesTheFullscreenMode()
        {
            var requests = new List<Vector2Int>();
            FullScreenMode requestedMode = FullScreenMode.Windowed;
            var display = new PlayerDisplaySettings(new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) },
                new Vector2Int(1280, 720), (width, height, mode) => { requests.Add(new Vector2Int(width, height)); requestedMode = mode; });
            Assert.That(display.Apply(FullScreenMode.ExclusiveFullScreen), Is.False);
            display.SelectRelative(1);
            Assert.That(requests, Is.Empty, "A pending choice must wait for Apply.");
            Assert.That(display.HasPendingChange, Is.True);
            Assert.That(display.Apply(FullScreenMode.ExclusiveFullScreen), Is.True);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1920, 1080) }, requests);
            Assert.That(requestedMode, Is.EqualTo(FullScreenMode.ExclusiveFullScreen));
            Assert.That(display.CurrentResolution, Is.EqualTo(new Vector2Int(1920, 1080)));
            Assert.That(display.HasPendingChange, Is.False);
            Assert.That(display.Apply(FullScreenMode.ExclusiveFullScreen), Is.False);
            Assert.That(requests.Count, Is.EqualTo(1));
        }

        [Test] public void EmptyNativeModesNeverRequestAnUnknownExclusiveFullscreenSize()
        {
            int requests = 0;
            var display = new PlayerDisplaySettings(new Vector2Int[0], new Vector2Int(1280, 720),
                (width, height, mode) => requests++);
            display.SelectRelative(1);
            Assert.That(display.Apply(FullScreenMode.ExclusiveFullScreen), Is.False,
                "Fallback window sizes are not evidence that an exclusive monitor mode is supported.");
            Assert.That(requests, Is.Zero);
            Assert.That(display.CurrentResolution, Is.EqualTo(new Vector2Int(1280, 720)));
            Assert.That(display.HasPendingChange, Is.True, "The player may still apply the choice in windowed mode.");
            Assert.That(display.Apply(FullScreenMode.Windowed), Is.True);
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(display.CurrentResolution, Is.EqualTo(new Vector2Int(1600, 900)));
        }

        [Test] public void WindowResizeKeepsAPendingWindowSizeWhenTheResolutionOptionsReorder()
        {
            var requests = new List<Vector2Int>();
            var display = new PlayerDisplaySettings(new Vector2Int[0], new Vector2Int(1280, 720),
                (width, height, mode) => requests.Add(new Vector2Int(width, height)));
            display.SelectRelative(1);
            Assert.That(display.SelectedResolution, Is.EqualTo(new Vector2Int(1600, 900)));
            Assert.That(display.CanApply(FullScreenMode.ExclusiveFullScreen), Is.False);
            // Leaving fullscreen or dragging the window can introduce a new sorted option.
            display.RefreshCurrentSize(1366, 768);
            Assert.That(display.CurrentResolution, Is.EqualTo(new Vector2Int(1366, 768)));
            Assert.That(display.SelectedResolution, Is.EqualTo(new Vector2Int(1600, 900)),
                "A display resize must retain the player's pending size rather than its old list index.");
            Assert.That(display.HasPendingChange, Is.True);
            Assert.That(requests, Is.Empty);
            Assert.That(display.Apply(FullScreenMode.Windowed), Is.True);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1600, 900) }, requests);
        }

        [Test] public void WindowResizeWithoutAPendingChoiceFollowsTheActualWindowSize()
        {
            int requests = 0;
            var display = new PlayerDisplaySettings(new Vector2Int[0], new Vector2Int(1280, 720),
                (width, height, mode) => requests++);
            display.RefreshCurrentSize(1366, 768);
            Assert.That(display.CurrentResolution, Is.EqualTo(new Vector2Int(1366, 768)));
            Assert.That(display.SelectedResolution, Is.EqualTo(new Vector2Int(1366, 768)));
            Assert.That(display.HasPendingChange, Is.False);
            Assert.That(requests, Is.Zero);
        }
    }
}
