using PaperDefense.Application;
using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    /// <summary>Default display boundary; live HUDs own and use their injected snapshot.</summary>
    public static class GameText
    {
        public static string Resolve(string value) => TextResourcesAsset.Default.Resolve(value);
        public static string Resolve(UserTextResolver resolver, string value) => (resolver ?? TextResourcesAsset.Default).Resolve(value);

        internal static string TrapOrBeaconEffect(ResolvedSupport support)
        {
            switch (support?.Kind)
            {
                case AssemblyStructureFamily.Spikes:
                    return UserText.Message("assembly.effect.spikes", support.SpikeDamage, support.Radius, ResolvedSupport.SpikeIntervalSeconds);
                case AssemblyStructureFamily.Beacon:
                    return UserText.Message("assembly.effect.beacon", support.ArmorReduction * 100, support.Radius, ResolvedSupport.BeaconLingerSeconds);
                case AssemblyStructureFamily.Prism:
                    return UserText.Message("assembly.effect.prism", ResolvedSupport.PrismIntervalSeconds, support.Radius,
                        support.BurnDamagePerSecond, (1 - support.SlowSpeedMultiplier) * 100, support.ArmorReduction * 100);
                default:
                    return null;
            }
        }
    }
}
