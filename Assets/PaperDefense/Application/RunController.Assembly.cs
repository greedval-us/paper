using System;
using System.Linq;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed partial class RunController
    {
        AssemblyCommands assemblyCommands;
        public AssemblyCommands AssemblyCommands => assemblyCommands ?? (assemblyCommands = new AssemblyCommands(this));
        public RunLoadoutSnapshot RunLoadout { get; private set; }
        bool runRequiresSlots;
        internal WorkshopProfile AssemblyProfile => profile;
        internal ComponentCatalog ConfiguredAssemblyCatalog => ConfiguredAssemblies();
        public ComponentCatalog AssemblyCatalog => ConfiguredAssemblies();
        internal GameBalance AssemblyBalance => session?.Balance;
        internal bool CreateAssemblyCandidate(out WorkshopProfile candidate, out string reason) => CreateCandidate(out candidate, out reason);
        internal bool CommitAssemblyCandidate(WorkshopProfile candidate, out string reason) => CommitCandidate(candidate, out reason);

        public AssemblyResolution PreviewAlbumEntry(AlbumEntry entry, MaterialTier material = MaterialTier.Pencil)
            => TowerCatalog.ResolveEntry(entry, AssemblyBalance, material);

        /// <summary>Uses the actual start gate without publishing a loadout or touching the profile store.</summary>
        public bool CanStartAlbumRun(out string reason)
        {
            if (!Editing(out reason)) return false;
            try
            {
                var balance = CreateWorkshopBalance(); balance.Content = balance.ResolveContent();
                return PrepareAlbumLoadout(balance, out _, out _, out _, out reason);
            }
            catch (ArgumentException error)
            { LastConfigurationDiagnostic = error.ToString(); reason = UserText.Message("run.error.configuration"); return false; }
        }

        bool StartAssemblyRun(out string reason)
        {
            if (!Editing(out reason)) return false;
            GameBalance balance;
            GameSession preparedSession;
            try
            {
                balance = CreateWorkshopBalance();
                balance.Content = balance.ResolveContent();
                preparedSession = new GameSession(balance, starterDefenses: false);
            }
            catch (ArgumentException error)
            { LastConfigurationDiagnostic = error.ToString(); reason = UserText.Message("run.error.configuration"); return false; }
            if (!PrepareAlbumLoadout(balance, out var snapshot, out var prepared, out var requireSlots, out reason)) return false;
            runLoadout = Array.AsReadOnly(prepared); RunLoadout = snapshot; runRequiresSlots = requireSlots; InWorkshop = false;
            Initialize(preparedSession);
            reason = ""; WorkshopChanged?.Invoke(); return true;
        }

        bool PrepareAlbumLoadout(GameBalance balance, out RunLoadoutSnapshot snapshot, out TowerBlueprint[] prepared,
            out bool requireSlots, out string reason)
        {
            snapshot = null; prepared = null; requireSlots = false; reason = "";
            try
            {
                var candidate = profile.CreateWorkingCopy(); candidate.ConfigureContent(balance.Content);
                var entries = candidate.GetLoadoutEntries();
                requireSlots = balanceAsset?.dataPreset?.database?.assemblyCatalog != null ||
                    entries.Any(entry => entry != null && entry.Kind != AlbumEntryKind.Legacy);
                foreach (var entry in entries.Where(e => e != null))
                {
                    if (entry.Kind == AlbumEntryKind.Legacy)
                    { if (!candidate.ValidateOwned(entry.LegacyBlueprint, out reason, balance)) return false; }
                    else foreach (var part in entry.Snapshot.Recipe.Parts)
                        if (!candidate.IsUnlocked(part.Id)) { reason = UserText.Message("profile.part.locked"); return false; }
                }
                snapshot = new RunLoadoutSnapshot(entries, balance);
                prepared = candidate.GetLoadoutBlueprints().Select(page => page?.WithContent(balance.Content)).ToArray();
            }
            catch (ArgumentException error)
            {
                LastConfigurationDiagnostic = error.ToString();
                reason = UserText.Message(error.ParamName == "entries" ? "run.loadout.attack_required" : "run.error.configuration");
                return false;
            }
            return true;
        }
    }
}
