using System;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    internal static class HistoricalProfileTestData
    {
        internal static WorkshopProfileData Create(WorkshopProfile profile, int profileVersion)
        {
            WorkshopProfileData data = profile.ToData(); data.Version = profileVersion;
            Array.Resize(ref data.Loadout, ProfileLimits.HistoricalLoadoutSlotCount);
            data.TotalXp = 0;
            data.Entries = null; data.Library = null; data.Rewards = null; data.Operations = null;
            if (profileVersion >= 4) return data;
            data.Unlocked = Array.FindAll(data.Unlocked, id => id != "module.rail");
            foreach (BlueprintData page in data.Album)
            {
                page.Modules &= ~(TowerModules)8;
                page.Drawing.FormatVersion = 1; page.Drawing.BindingVersion = 1;
                page.Drawing.Parts = Array.FindAll(page.Drawing.Parts, part => (int)part.Key >= 0 && (int)part.Key <= 6);
            }
            return data;
        }
    }
}
