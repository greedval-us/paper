using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    /// <summary>Characterization of saved part bindings and format limits before policy extraction.</summary>
    public sealed class DrawingPolicyContractTests
    {
        const TowerModules AllModules = TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking | TowerModules.Rail;

        static DrawingStroke Marker(DrawingPartKey key)
        {
            float offset = (int)key / 16f;
            return new DrawingStroke(new[] { new DrawingPoint(-.5f, -.5f + offset), new DrawingPoint(.5f, .5f - offset) });
        }

        static DrawingSnapshot EveryPart()
        {
            var parts = new DrawingPart[8];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = new DrawingPart((DrawingPartKey)i, new[] { Marker((DrawingPartKey)i) });
            return new DrawingSnapshot(parts);
        }

        static TowerBlueprint Page(StructureKind? second = StructureKind.Piercer, TowerModules modules = AllModules)
            => TowerBlueprint.NewDraft("policy", "Policy", ChassisKind.Standard, StructureKind.Gun, second, modules).WithDrawing(EveryPart());

        // Literal hashes were captured from the original entrypoint at HEAD 5ab2424.
        // Changing a binding, losing decoration, or changing canonical geometry must fail these cases.
        [TestCase("chassis", DrawingPartKey.Base, "a06d66b6d7fd7891")]
        [TestCase("first", DrawingPartKey.Mount0, "92b6bb72a4ceb3db")]
        [TestCase("second", DrawingPartKey.Mount1, "f0ef550f27e73105")]
        [TestCase("remove-second", DrawingPartKey.Mount1, "f0ef550f27e73105")]
        [TestCase("armor", DrawingPartKey.Armor, "4f184c1b7ba8f7ff")]
        [TestCase("feed", DrawingPartKey.Feed, "bd1ffbfff8865229")]
        [TestCase("tracking", DrawingPartKey.Tracking, "e583295cebaec9ab")]
        [TestCase("rail", DrawingPartKey.Rail, "899949a009d7430f")]
        public void BothAssemblyEditorsClearOnlyTheChangedPart(string change, DrawingPartKey removed, string expectedHash)
        {
            TowerBlueprint original = Page();
            ChassisKind chassis = change == "chassis" ? ChassisKind.Heavy : ChassisKind.Standard;
            StructureKind first = change == "first" ? StructureKind.Mortar : StructureKind.Gun;
            StructureKind? second = change == "remove-second" ? (StructureKind?)null :
                change == "second" ? StructureKind.Fan : StructureKind.Piercer;
            TowerModules modules = AllModules;
            if (change == "armor") modules &= ~TowerModules.Armor;
            if (change == "feed") modules &= ~TowerModules.Feed;
            if (change == "tracking") modules &= ~TowerModules.Tracking;
            if (change == "rail") modules &= ~TowerModules.Rail;

            TowerBlueprint changed = original.WithAssembly(chassis, first, second, modules);
            var editor = new DrawingEditSession(original);
            Assert.That(editor.ReplaceBlueprint(TowerBlueprint.NewDraft(original.Id, original.Name, chassis, first, second, modules)), Is.True);
            foreach (DrawingSnapshot result in new[] { changed.Drawing, editor.Snapshot })
            {
                Assert.That(result.GeometryHash, Is.EqualTo(expectedHash));
                Assert.That(result.PointCount, Is.EqualTo(14));
                foreach (DrawingPart part in result.Parts)
                    Assert.That(part.Strokes.Count, Is.EqualTo(part.Key == removed ? 0 : 1), part.Key.ToString());
            }
            Assert.That(original.Drawing.GeometryHash, Is.EqualTo("7e13935604a832fe"));
            Assert.That(original.Drawing.PointCount, Is.EqualTo(16));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo("7e13935604a832fe"));
            Assert.That(editor.Redo(), Is.True);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(expectedHash));
        }

        [Test]
        public void AssemblyReplacementRetainsUnsavedEditorStrokesRatherThanBlueprintDrawing()
        {
            TowerBlueprint original = TowerBlueprint.NewDraft("draft", "Draft", ChassisKind.Standard, StructureKind.Gun);
            var editor = new DrawingEditSession(original);
            Assert.That(editor.AddStroke(DrawingPartKey.Base, Marker(DrawingPartKey.Base).Points, out _), Is.True);
            Assert.That(editor.AddStroke(DrawingPartKey.Mount0, Marker(DrawingPartKey.Mount0).Points, out _), Is.True);
            Assert.That(editor.AddStroke(DrawingPartKey.Decoration, Marker(DrawingPartKey.Decoration).Points, out _), Is.True);
            Assert.That(original.Drawing.PointCount, Is.Zero);
            Assert.That(editor.ReplaceBlueprint(TowerBlueprint.NewDraft(original.Id, original.Name, original.Chassis, StructureKind.Mortar)), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Base).Strokes.Count, Is.EqualTo(1));
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.Zero);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Decoration).Strokes.Count, Is.EqualTo(1));
            Assert.That(editor.Snapshot.PointCount, Is.EqualTo(4));
        }

        [Test]
        public void ExactNoOpKeepsDormantSavedPartsWithoutAddingHistory()
        {
            TowerBlueprint original = Page(null, TowerModules.None);
            Assert.That(original.WithAssembly(original.Chassis, original.FirstWeapon, original.SecondWeapon, original.Modules), Is.SameAs(original));
            var editor = new DrawingEditSession(original);
            Assert.That(editor.ReplaceBlueprint(original), Is.False);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo("7e13935604a832fe"));
            Assert.That(editor.CanUndo, Is.False);
        }

        [Test]
        public void InstallingModuleClearsDormantLinesWhileKeepingExistingActiveParts()
        {
            TowerBlueprint original = Page(null, TowerModules.None);
            TowerBlueprint changed = original.WithAssembly(original.Chassis, original.FirstWeapon, null, TowerModules.Armor);
            var editor = new DrawingEditSession(original);
            Assert.That(editor.ReplaceBlueprint(TowerBlueprint.NewDraft(original.Id, original.Name, original.Chassis, original.FirstWeapon,
                modules: TowerModules.Armor)), Is.True);
            foreach (DrawingSnapshot result in new[] { changed.Drawing, editor.Snapshot })
            {
                Assert.That(result.PointCount, Is.EqualTo(6));
                foreach (DrawingPart part in result.Parts)
                    Assert.That(part.Strokes.Count, Is.EqualTo(part.Key == DrawingPartKey.Base || part.Key == DrawingPartKey.Mount0 ||
                        part.Key == DrawingPartKey.Decoration ? 1 : 0), part.Key.ToString());
            }
        }

        [Test]
        public void LegacyNoOpAndRenameKeepOriginButAssemblyChangeStartsAnEmptyAuthoredDraft()
        {
            var legacy = new TowerBlueprint("legacy", "Legacy", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(legacy.WithAssembly(legacy.Chassis, legacy.FirstWeapon, legacy.SecondWeapon, legacy.Modules), Is.SameAs(legacy));
            Assert.That(legacy.WithName("Renamed").Origin, Is.EqualTo(DrawingOrigin.LegacyStandard));
            TowerBlueprint changed = legacy.WithAssembly(ChassisKind.Heavy, legacy.FirstWeapon, null, TowerModules.None);
            Assert.That(changed.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(changed.Drawing.IsEmpty, Is.True);
        }

        [Test]
        public void EnabledPartsKeepTemplateOrderAndIgnoreDormantSavedLines()
        {
            Assert.That(DrawingTemplateCatalog.EnabledKeys(Page()), Is.EqualTo(new[] { DrawingPartKey.Base, DrawingPartKey.Mount0,
                DrawingPartKey.Mount1, DrawingPartKey.Armor, DrawingPartKey.Feed, DrawingPartKey.Tracking, DrawingPartKey.Rail, DrawingPartKey.Decoration }));
            TowerBlueprint single = Page(null, TowerModules.None);
            Assert.That(DrawingTemplateCatalog.EnabledKeys(single), Is.EqualTo(new[] { DrawingPartKey.Base, DrawingPartKey.Mount0, DrawingPartKey.Decoration }));
            Assert.That(DrawingTemplateCatalog.IsEnabled(null, DrawingPartKey.Base), Is.False);
            Assert.That(DrawingTemplateCatalog.IsEnabled(single, (DrawingPartKey)99), Is.False);
            Assert.That(single.Drawing.PointCount, Is.EqualTo(16));
        }

        [TestCase(TowerModules.Armor, DrawingPartKey.Armor)]
        [TestCase(TowerModules.Feed, DrawingPartKey.Feed)]
        [TestCase(TowerModules.Tracking, DrawingPartKey.Tracking)]
        [TestCase(TowerModules.Rail, DrawingPartKey.Rail)]
        public void EachOptionalPartUsesItsOwnModuleBinding(TowerModules module, DrawingPartKey enabledPart)
        {
            TowerBlueprint single = Page(null, module);
            foreach (DrawingPartKey key in Enum.GetValues(typeof(DrawingPartKey)))
                Assert.That(DrawingTemplateCatalog.IsEnabled(single, key), Is.EqualTo(key == DrawingPartKey.Base ||
                    key == DrawingPartKey.Mount0 || key == DrawingPartKey.Decoration || key == enabledPart), key.ToString());
        }

        [Test]
        public void SavedIdentifiersAndDensePartIndexContractStayCompatible()
        {
            Assert.That(new[] { (int)ChassisKind.Standard, (int)ChassisKind.Heavy }, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(new[] { (int)DrawingPartKey.Base, (int)DrawingPartKey.Mount0, (int)DrawingPartKey.Mount1, (int)DrawingPartKey.Armor,
                (int)DrawingPartKey.Feed, (int)DrawingPartKey.Tracking, (int)DrawingPartKey.Decoration, (int)DrawingPartKey.Rail },
                Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
            Assert.That(Enum.GetValues(typeof(DrawingPartKey)).Length, Is.EqualTo(8));
            Assert.That(new[] { (int)TowerModules.None, (int)TowerModules.Armor, (int)TowerModules.Feed, (int)TowerModules.Tracking, (int)TowerModules.Rail },
                Is.EqualTo(new[] { 0, 1, 2, 4, 8 }));
            Assert.That(DrawingSnapshot.Empty.Parts.Count, Is.EqualTo(8));
            foreach (DrawingPartKey key in Enum.GetValues(typeof(DrawingPartKey)))
                Assert.That(DrawingSnapshot.Empty.GetPart(key).Key, Is.EqualTo(key));
        }

        [TestCase(64, 48, true)]
        [TestCase(65, 48, false)]
        [TestCase(64, 49, false)]
        public void BlueprintIdentifierAndNameLimitsMatchSavedPageContract(int idLength, int nameLength, bool valid)
        {
            var page = new TowerBlueprint(new string('i', idLength), new string('Я', nameLength), ChassisKind.Standard, StructureKind.Gun);
            Assert.That(TowerCatalog.ValidateParts(page, out _), Is.EqualTo(valid));
        }

        [Test]
        public void FullAlbumStillAllowsReplacementButRejectsSixtyFifthPageAndFifthSlot()
        {
            var profile = new WorkshopProfile();
            for (int i = 4; i < 64; i++)
                Assert.That(profile.SaveBlueprint(new TowerBlueprint("page." + i, "Page " + i, ChassisKind.Standard, StructureKind.Gun), out _), Is.True);
            Assert.That(profile.SaveBlueprint(new TowerBlueprint("page.63", "Renamed", ChassisKind.Standard, StructureKind.Gun), out _), Is.True);
            Assert.That(profile.SaveBlueprint(new TowerBlueprint("overflow", "Overflow", ChassisKind.Standard, StructureKind.Gun), out _), Is.False);
            Assert.That(profile.AssignSlot(-1, "page.63", out _), Is.False);
            Assert.That(profile.AssignSlot(4, "page.63", out _), Is.False);
            WorkshopProfileData data = profile.ToData();
            Assert.That(data.Album.Length, Is.EqualTo(64));
            Assert.That(data.Loadout.Length, Is.EqualTo(4));
            WorkshopProfile restored = WorkshopProfile.FromData(data);
            Assert.That(restored.Album.Count, Is.EqualTo(64));
            Assert.That(restored.GetLoadoutBlueprints().Count, Is.EqualTo(4));
        }
    }
}
