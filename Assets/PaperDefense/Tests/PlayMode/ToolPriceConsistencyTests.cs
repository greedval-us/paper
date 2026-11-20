using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class ToolPriceConsistencyTests
    {
        [Test]
        public void WallQuoteUsesTheSameRoundedPriceAsItsPayment()
        {
            var root = new GameObject("Isolated tool pricing"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); asset.startingInk = 10000;
                asset.weapons.Single(w => w.kind == StructureKind.Wall).cost = 125;
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.Initialize();
                var cell = new Cell(4, 4); var quote = run.Session.QuoteBuild(new[] { cell }, StructureKind.Wall, MaterialTier.Marker);
                Assert.That(quote.Valid, Is.True, quote.Reason);
                Assert.That(quote.Cost, Is.EqualTo(401), "Observed Unity Mono quote before the shared-formula extraction.");
                int before = run.Session.Ink;
                Assert.That(run.Build(new[] { cell }, StructureKind.Wall, MaterialTier.Marker, out var reason), Is.True, reason);
                Assert.That(before - run.Session.Ink, Is.EqualTo(quote.Cost));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }
    }
}
