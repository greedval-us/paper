using System;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    internal static class HistoricalProfileTestData
    {
        internal static WorkshopProfileData Create(WorkshopProfile profile, int profileVersion)
        {
            WorkshopProfileData data = profile.ToData(); data.Version = profileVersion;
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
