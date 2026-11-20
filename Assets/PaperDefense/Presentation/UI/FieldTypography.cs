using UnityEngine;

namespace PaperDefense.Presentation
{
    internal static class FieldTypography
    {
        static Font regular, semibold;

        internal static Font Regular => regular != null ? regular : regular =
            Resources.Load<Font>("FieldHeadquarters/Inter-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        internal static Font Semibold => semibold != null ? semibold : semibold =
            Resources.Load<Font>("FieldHeadquarters/Inter-SemiBold") ?? Regular;
    }
}
