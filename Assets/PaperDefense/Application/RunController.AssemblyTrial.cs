using System;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed partial class RunController
    {
        /// <summary>Validate the local draft without publishing a profile candidate or a reward intent.</summary>
        public AssemblyTrialReport RunAssemblyTrial(AlbumEntry entry, AssemblyTrialScenario scenario, int budget = 400, int seed = 1337)
        {
            if (!InWorkshop || entry == null || entry.Kind == AlbumEntryKind.Legacy) return null;
            try
            {
                var draft = new WorkshopDraftSession(this);
                draft.Begin(entry);
                if (!draft.CanTrial) return null;
                return AssemblyTrialRunner.Run(entry, session.Balance, scenario, budget, seed);
            }
            catch (ArgumentException error)
            { LastConfigurationDiagnostic = error.ToString(); return null; }
            catch (NotSupportedException error)
            { LastConfigurationDiagnostic = error.ToString(); return null; }
        }
    }
}
