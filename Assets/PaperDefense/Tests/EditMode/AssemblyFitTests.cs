using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class AssemblyFitTests
    {
        static readonly string[] Ids = { "frame.standard", "hull.standard", "armor.none", "armor.plate",
            "feed.standard", "turn.standard", "core.gun", "head.short", "sight.standard", "magazine.standard", "trigger.standard" };
        static readonly ComponentCategory[] Categories = { ComponentCategory.Frame, ComponentCategory.Hull,
            ComponentCategory.Armor, ComponentCategory.Armor, ComponentCategory.Feed, ComponentCategory.Turn,
            ComponentCategory.Mechanism, ComponentCategory.Head, ComponentCategory.Sight, ComponentCategory.Magazine, ComponentCategory.Trigger };
        static PartRef Ref(string id, int revision = 1) => new PartRef(id, revision);
        static WeaponRecipe Gun(string head = "head.short", int revision = 1) => new WeaponRecipe(Ref("core.gun"),
            Ref(head, revision), Ref("sight.standard"), Ref("magazine.standard"), Ref("trigger.standard"));
        static AssemblyRecipe Recipe(bool pair = true, string armor = "armor.none", string head = "head.short", int revision = 1)
            => new AssemblyRecipe(new TowerRecipe(new FoundationRecipe(Ref("frame.standard"), Ref("hull.standard"),
                Ref(armor), Ref("feed.standard"), Ref("turn.standard")), pair
                ? new[] { new WeaponMountRecipe(0, Gun(head, revision)), new WeaponMountRecipe(1, Gun()) }
                : new[] { new WeaponMountRecipe(0, Gun(head, revision)) }));
        static AssemblyFitPointInput Point(float x, float y) => new AssemblyFitPointInput { X = x, Y = y };
        static AssemblyFitRegionInput Region(string motion, params AssemblyFitPointInput[] points)
            => new AssemblyFitRegionInput { Motion = motion, Points = points };
        static AssemblyFitCatalogInput Input(float spacing = .4756f, float radius = .23f)
        {
            return new AssemblyFitCatalogInput {
                Revision = "fit-fixture-v1",
                Parts = Ids.Select((id, i) => new AssemblyFitPartInput { Id = id, Revision = 1,
                    AssetRevision = "art-v1", Category = Categories[i], AttachType = "part." + Categories[i],
                    ExpectedSocketType = "part." + Categories[i] }).ToArray(),
                Frames = new[] { new AssemblyFitFrameInput { FrameId = "frame.standard", Revision = 1,
                    Mounts = new[] {
                        new AssemblyFitMountInput { SlotIndex = 0, SocketId = "mount.0", SocketType = "gun.standard", X = 0, Y = 0 },
                        new AssemblyFitMountInput { SlotIndex = 1, SocketId = "mount.1", SocketType = "gun.standard", X = spacing, Y = 0 }
                    }, Keepouts = Array.Empty<AssemblyFitKeepoutInput>() } },
                Weapons = new[] { new AssemblyFitWeaponInput { MechanismId = "core.gun", Revision = 1,
                    RequiredParts = Gun().Parts.Select(p => new PartRefInput { Id = p.Id, Revision = p.Revision }).ToArray(),
                    Regions = new[] { Region("rotating", Point(0, radius)),
                        Region("fixed", Point(-.02f, -.02f), Point(.02f, .02f)) } } }
            };
        }
        static string[] Codes(AssemblyRecipe recipe, AssemblyFitCatalogInput input)
            => AssemblyFitRules.Validate(recipe, new AssemblyFitCatalog(input)).Select(x => x.Code).ToArray();

        [Test]
        public void IntersectingPair_ReportsClearance()
        {
            Assert.That(Codes(Recipe(), Input(.2237952f, .29f)), Does.Contain("assembly.clearance"));
        }

        [Test]
        public void CorrectedDoubleGun_PassesWholeIndependentRotationEnvelopes()
        {
            Assert.That(Codes(Recipe(), Input()), Is.Empty);
        }

        [Test]
        public void RecoilEndpointBehindPivot_IsIncludedInSweptRadius()
        {
            var input = Input();
            input.Weapons[0].Regions = new[] { Region("recoiling", Point(.02f, -.04f)) };
            input.Weapons[0].RecoilLimit = .085f;
            var fits = new AssemblyFitCatalog(input);
            Assert.That(fits.TryGetWeapon("core.gun", 1, out var weapon), Is.True);
            Assert.That(AssemblyFitRules.SweptRadius(weapon), Is.EqualTo(Math.Sqrt(.02 * .02 + .125 * .125)).Within(.000001));
            Assert.That(Codes(Recipe(), input), Is.Empty);
            input.Frames[0].Mounts[1].X = .25f;
            Assert.That(Codes(Recipe(), input), Does.Contain("assembly.clearance"));
        }

        [Test]
        public void FixedSupportsCannotIntersectEachOther()
        {
            var input = Input(.2237952f, .01f);
            input.Weapons[0].Regions = new[] { Region("rotating", Point(0, .01f)),
                Region("fixed", Point(-.15f, -.01f), Point(.15f, .01f)) };
            Assert.That(Codes(Recipe(), input), Does.Contain("assembly.clearance"));
        }

        [Test]
        public void FixedSupportCannotEnterNeighbourMotionEnvelope()
        {
            var input = Input(.2237952f, .04f);
            input.Weapons[0].Regions = new[] { Region("rotating", Point(0, .04f)),
                Region("fixed", Point(.19f, -.01f), Point(.20f, .01f)) };
            Assert.That(Codes(Recipe(), input), Does.Contain("assembly.clearance"));
            Assert.That(Codes(Recipe(false), input), Is.Empty, "Declared seating within the same weapon remains permitted.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BodyAndSelectedArmorKeepouts_RejectSweptIntersection(bool armorOnly)
        {
            var input = Input();
            input.Frames[0].Keepouts = new[] { new AssemblyFitKeepoutInput {
                X = .20f, Y = -.02f, Width = .02f, Height = .04f, ArmorOnly = armorOnly } };
            Assert.That(Codes(Recipe(false, "armor.plate"), input), Does.Contain("assembly.clearance"));
            if (armorOnly) Assert.That(Codes(Recipe(false), input), Is.Empty);
            else Assert.That(Codes(Recipe(false), input), Does.Contain("assembly.clearance"));
        }

        [Test]
        public void UnknownPartAndExactRevision_AreRejected()
        {
            Assert.That(Codes(Recipe(false, head: "unknown"), Input()), Does.Contain("assembly.fit.part"));
            Assert.That(Codes(Recipe(false, revision: 2), Input()), Does.Contain("assembly.fit.revision"));
        }

        [Test]
        public void RegisteredButUnsupportedGunCombination_IsRejected()
        {
            var input = Input();
            input.Parts = input.Parts.Concat(new[] { new AssemblyFitPartInput { Id = "head.long", Revision = 1,
                AssetRevision = "art-v1", Category = ComponentCategory.Head, AttachType = "head", ExpectedSocketType = "head" } }).ToArray();
            Assert.That(Codes(Recipe(false, head: "head.long"), input), Does.Contain("assembly.fit.coverage"));
        }

        [Test]
        public void MissingMountAndWrongSocketType_AreRejected()
        {
            var input = Input();
            input.Frames[0].Mounts = input.Frames[0].Mounts.Take(1).ToArray();
            Assert.That(Codes(Recipe(), input), Does.Contain("assembly.fit.socket"));
            input.Frames[0].Mounts[0].SocketType = "gun.other";
            Assert.That(Codes(Recipe(false), input), Does.Contain("assembly.fit.socket_type"));
        }

        [Test]
        public void PartAttachMismatch_IsRejected()
        {
            var input = Input(); input.Parts[7].AttachType = "incompatible";
            Assert.That(Codes(Recipe(false), input), Does.Contain("assembly.fit.socket_type"));
        }

        [TestCase("schema")]
        [TestCase("part_schema")]
        [TestCase("socket")]
        [TestCase("number")]
        [TestCase("motion")]
        public void MalformedCatalog_IsRejectedWithStableDiagnostic(string mutation)
        {
            var input = Input(); string code;
            switch (mutation)
            {
                case "schema": input.SchemaVersion = 2; code = "assembly.fit.schema"; break;
                case "part_schema": input.Parts[0].SocketSchemaVersion = 2; code = "assembly.fit.schema"; break;
                case "socket": input.Frames[0].Mounts[0].SocketId = "mount.unknown"; code = "assembly.fit.socket"; break;
                case "number": input.Weapons[0].Regions[0].Points[0].Y = float.NaN; code = "assembly.fit.number"; break;
                default: input.Weapons[0].Regions[0].Motion = "teleporting"; code = "assembly.fit.motion"; break;
            }
            var error = Assert.Throws<AssemblyCatalogException>(() => new AssemblyFitCatalog(input));
            Assert.That(error.Diagnostics.Select(x => x.Code), Does.Contain(code));
        }

        [Test]
        public void CatalogSnapshot_DefensivelyCopiesAllNestedAuthorData()
        {
            var input = Input(); var fits = new AssemblyFitCatalog(input);
            input.Revision = "changed"; input.Parts[0].AssetRevision = "changed";
            input.Frames[0].Mounts[0].X = 99;
            input.Weapons[0].Regions[0].Points[0].Y = 99;
            input.Weapons[0].RequiredParts[1].Id = "changed";
            input.Weapons = Array.Empty<AssemblyFitWeaponInput>();
            Assert.That(fits.Revision, Is.EqualTo("fit-fixture-v1"));
            Assert.That(fits.TryGetPart("frame.standard", 1, out var part), Is.True);
            Assert.That(part.AssetRevision, Is.EqualTo("art-v1"));
            Assert.That(fits.TryGetFrame("frame.standard", 1, out var frame), Is.True);
            Assert.That(frame.Mounts[0].Position.X, Is.Zero);
            Assert.That(frame.Mounts, Is.Not.InstanceOf<AssemblyFitMount[]>());
            Assert.That(fits.TryGetWeapon("core.gun", 1, out var weapon), Is.True);
            Assert.That(AssemblyFitRules.SweptRadius(weapon), Is.EqualTo(.23f).Within(.000001));
            Assert.That(weapon.RequiredParts[1].Id, Is.EqualTo("head.short"));
            Assert.That(AssemblyFitRules.Validate(Recipe(), fits), Is.Empty);
        }

        [Test]
        public void FiniteInputsThatOverflowDerivedGeometry_AreRejected()
        {
            var input = Input();
            input.Weapons[0].Regions[0].Points = new[] { Point(float.MaxValue, float.MaxValue) };
            var error = Assert.Throws<AssemblyCatalogException>(() => new AssemblyFitCatalog(input));
            Assert.That(error.Diagnostics.Select(x => x.Code), Does.Contain("assembly.fit.number"));
            input = Input(); input.Frames[0].Mounts[0].X = float.MaxValue;
            input.Weapons[0].PivotX = float.MaxValue;
            Assert.That(Codes(Recipe(false), input), Does.Contain("assembly.fit.number"));
        }

        [Test]
        public void CombinedResolution_RequiresNumericalAndPhysicalValidityBeforePublishingValue()
        {
            ComponentModifier Set(AssemblyStat stat, float value) => new ComponentModifier(stat, ModifierOperation.Set, value);
            var input = Input();
            var definitions = input.Parts.Select(p => new ComponentDefinition(p.Id, p.Revision, p.Id, p.Category, 1, 0,
                AssemblyWeaponFamily.Gun, AssemblyStructureFamily.None,
                p.Category == ComponentCategory.Frame ? new[] { Set(AssemblyStat.Capacity, 100), Set(AssemblyStat.MountCount, 2),
                    Set(AssemblyStat.Health, 55), Set(AssemblyStat.Feed, 1.6f) }
                : p.Category == ComponentCategory.Mechanism ? new[] { Set(AssemblyStat.MagazineSize, 6), Set(AssemblyStat.Interval, .42f),
                    Set(AssemblyStat.ReloadSeconds, .42f), Set(AssemblyStat.Damage, 12), Set(AssemblyStat.Range, 8), Set(AssemblyStat.TargetLimit, 1) }
                : Array.Empty<ComponentModifier>()));
            var catalog = new ComponentCatalog("fit-fixture-v1", definitions);
            var recipe = Recipe(); var balance = GameBalance.CreateDefault();
            var numerical = AssemblyResolver.Resolve(recipe, catalog, balance, MaterialTier.Pencil);
            Assert.That(numerical.Valid, Is.True);
            var intersectingFits = new AssemblyFitCatalog(Input(.2237952f, .29f));
            var rejected = AssemblyResolver.Resolve(recipe, catalog, balance, MaterialTier.Pencil, intersectingFits);
            Assert.That(rejected.Valid, Is.False); Assert.That(rejected.Value, Is.Null);
            Assert.That(rejected.Diagnostics.Select(x => x.Code), Does.Contain("assembly.clearance"));
            var accepted = AssemblyResolver.Resolve(recipe, catalog, balance, MaterialTier.Pencil, new AssemblyFitCatalog(input));
            Assert.That(accepted.Valid, Is.True); Assert.That(accepted.Diagnostics, Is.Empty);
            Assert.That(accepted.Value.Weapons[0].Range, Is.EqualTo(numerical.Value.Weapons[0].Range));
            balance.Material(MaterialTier.Pencil).CostMultiplier = float.NaN;
            var bothInvalid = AssemblyResolver.Resolve(recipe, catalog, balance, MaterialTier.Pencil, intersectingFits);
            Assert.That(bothInvalid.Value, Is.Null);
            Assert.That(bothInvalid.Diagnostics[0].Code, Is.EqualTo("assembly.number.nonfinite"));
            Assert.That(bothInvalid.Diagnostics.Select(x => x.Code), Does.Contain("assembly.clearance"));
        }
    }
}
