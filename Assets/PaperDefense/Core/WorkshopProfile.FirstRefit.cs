namespace PaperDefense.Core
{
    public sealed partial class WorkshopProfile
    {
        // Existing v8/v9 readers retain valid saved rights, including non-catalog lifecycle markers.
        const string FirstRefitTutorialMarker = "tutorial.first-refit.v1";

        public bool FirstRefitTutorialHandled => unlocked.Contains(FirstRefitTutorialMarker);

        public bool MarkFirstRefitTutorialHandled() => unlocked.Add(FirstRefitTutorialMarker);
    }
}
