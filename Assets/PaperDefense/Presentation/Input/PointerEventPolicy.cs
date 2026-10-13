using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace PaperDefense.Presentation
{
    /// <summary>Keep exact mouse-down positions while drawing input is enabled; restore shared settings on release.</summary>
    internal static class PointerEventPolicy
    {
        sealed class Ownership
        {
            public int Users;
            public bool PreviousValue;
        }

        static readonly Dictionary<InputSettings, Ownership> owners = new Dictionary<InputSettings, Ownership>();

        public static InputSettings Acquire()
        {
            var settings = InputSystem.settings;
            if (settings == null) return null;
            if (!owners.TryGetValue(settings, out var ownership))
            {
                ownership = new Ownership { PreviousValue = settings.disableRedundantEventsMerging };
                owners.Add(settings, ownership);
            }
            ownership.Users++;
            settings.disableRedundantEventsMerging = true;
            return settings;
        }

        public static void Release(InputSettings settings)
        {
            if (settings == null || !owners.TryGetValue(settings, out var ownership)) return;
            if (--ownership.Users != 0) return;
            settings.disableRedundantEventsMerging = ownership.PreviousValue;
            owners.Remove(settings);
        }
    }
}
