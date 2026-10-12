using PaperDefense.Application;
using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    /// <summary>Default display boundary; live HUDs own and use their injected snapshot.</summary>
    public static class GameText
    {
        public static string Resolve(string value) => TextResourcesAsset.Default.Resolve(value);
        public static string Resolve(UserTextResolver resolver, string value) => (resolver ?? TextResourcesAsset.Default).Resolve(value);
    }
}
