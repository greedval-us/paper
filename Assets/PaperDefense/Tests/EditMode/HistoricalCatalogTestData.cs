using System;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    internal static class HistoricalCatalogTestData
    {
        internal static WorkshopProfileData VersionTwo()
        {
            WorkshopProfileData data = new WorkshopProfile().ToData(); data.Version = 2;
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
