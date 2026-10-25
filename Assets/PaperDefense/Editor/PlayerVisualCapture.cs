namespace PaperDefense.Editor
{
    // The same native capture path is available to both Editor validation and the hidden Player.
    public static class PlayerVisualCapture
    {
        public static string Capture(string name, int width = 1600, int height = 1000, string folder = "Captures/PlayerGame")
            => PaperDefense.Presentation.ReleaseVisualCapture.Capture(name, width, height, folder);
    }
}
