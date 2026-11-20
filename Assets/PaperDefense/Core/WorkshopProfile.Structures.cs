using System.Linq;

namespace PaperDefense.Core
{
    public readonly struct StructureFamilyOffer
    {
        public string RightId { get; }
        public int GoldCost { get; }
        public int RequiredLevel { get; }
        public bool Owned { get; }
        public bool CanPurchase { get; }

        internal StructureFamilyOffer(string rightId, int cost, int level, WorkshopProfile profile)
        {
            RightId = rightId;
            GoldCost = cost;
            RequiredLevel = level;
            Owned = cost == 0 || profile.IsUnlocked(rightId);
            CanPurchase = !Owned && profile.Gold >= cost && MetaProgression.LevelForXp(profile.TotalXp) >= level;
        }
    }

    public sealed partial class WorkshopProfile
    {
        const string StructureShopMarker = "shop.structures.v1";
        const string SpikesRight = "structure.family.spikes";
        const string BeaconRight = "structure.family.beacon";

        public StructureFamilyOffer GetStructureOffer(AssemblyStructureFamily family)
        {
            switch (family)
            {
                case AssemblyStructureFamily.Wall:
                case AssemblyStructureFamily.Slow:
                case AssemblyStructureFamily.Repair:
                case AssemblyStructureFamily.Generator: return new StructureFamilyOffer(null, 0, 1, this);
                case AssemblyStructureFamily.Spikes: return new StructureFamilyOffer(SpikesRight, 45, 1, this);
                case AssemblyStructureFamily.Beacon: return new StructureFamilyOffer(BeaconRight, 80, 2, this);
                case AssemblyStructureFamily.Prism: return new StructureFamilyOffer("structure.family.prism", 120, 3, this);
                default: return default;
            }
        }

        public bool TryPurchaseStructureFamily(AssemblyStructureFamily family, out string code)
        {
            var offer = GetStructureOffer(family);
            code = "";
            if (offer.Owned) return true;
            if (offer.RightId == null) { code = "profile.structure.unknown"; return false; }
            if (MetaProgression.LevelForXp(TotalXp) < offer.RequiredLevel) { code = "profile.structure.level"; return false; }
            if (Gold < offer.GoldCost) { code = "profile.structure.gold"; return false; }
            Gold -= offer.GoldCost;
            unlocked.Add(offer.RightId);
            return true;
        }

        public bool OwnsStructureRecipe(AssemblyRecipe recipe)
            => recipe?.Structure == null || GetStructureOffer(recipe.Structure.Family).Owned;

        // Older v9 profiles got the stock components for every workshop template for free.
        // Carry over families that actually have a saved design or a purchased specialist part.
        void MigrateStructureShop()
        {
            if (!unlocked.Add(StructureShopMarker)) return;
            if (entries.Any(e => e.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Spikes) ||
                unlocked.Contains("site.spikes.compact") || unlocked.Contains("function.spikes.barbed"))
                unlocked.Add(SpikesRight);
            if (entries.Any(e => e.Snapshot?.Recipe.Structure?.Family == AssemblyStructureFamily.Beacon) ||
                unlocked.Contains("site.beacon.compact") || unlocked.Contains("function.beacon.focused"))
                unlocked.Add(BeaconRight);
        }
    }
}
