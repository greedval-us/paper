using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Palette indices shared with saved and caller-owned notebook themes.</summary>
    public static class NotebookThemeDefaults
    {
        public static readonly Color Surface = new Color32(245, 246, 248, 255);
        public static readonly Color Ink = new Color32(53, 65, 80, 255);

        internal static NotebookVisualThemeAsset CreateTheme()
        {
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            theme.name = "Notebook native theme defaults (runtime)";
            theme.hideFlags = HideFlags.HideAndDontSave;
            var data = new NotebookArtSource { Revision = "notebook-native-defaults-v1", Palette = new[] {
                Ink, new Color(.36f, .40f, .46f), Surface, new Color(.8f, .24f, .25f),
                new Color(.24f, .48f, .76f), new Color(.25f, .61f, .42f),
                new Color(.65f, .69f, .73f, .18f), Color.white } };
            foreach (string family in new[] { "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "blotter", "rail" })
                data.Recoils.Add(new NotebookWeaponRecoil { WeaponId = "weapon." + family, EffectId = "fx.muzzle",
                    Duration = .2f, Distance = .085f, Muzzle = new Vector2(0, .35f), Keys = {
                        new NotebookRecoilKey { Time = 0, Value = 1 }, new NotebookRecoilKey { Time = .2f, Value = 0 } } });
            theme.ReplaceData(data);
            return theme;
        }

        internal static NotebookArtShape ForPalette(NotebookArtShape shape, int channels)
        {
            if (shape == null) return null;
            var copy = new NotebookArtShape { Id = shape.Id, TemplateId = shape.TemplateId, ReferenceRevision = shape.ReferenceRevision };
            foreach (var mark in shape.Marks)
                copy.Marks.Add(new NotebookArtMark { Name = mark.Name, Primitive = mark.Primitive,
                    Channel = LegacyChannel(mark.Channel, channels), Width = mark.Width, Alpha = mark.Alpha,
                    Reference = mark.Reference, Points = (Vector2[])mark.Points.Clone(), Center = mark.Center,
                    Radius = mark.Radius, Aspect = mark.Aspect, Segments = mark.Segments });
            return copy;
        }

        static NotebookInkChannel LegacyChannel(NotebookInkChannel channel, int channels)
        {
            if ((int)channel < channels) return channel;
            switch (channel)
            {
                case NotebookInkChannel.Ivory: return NotebookInkChannel.Navy;
                case NotebookInkChannel.Sketch: return NotebookInkChannel.Pencil;
                case NotebookInkChannel.Body: return channels > (int)NotebookInkChannel.Sketch
                    ? NotebookInkChannel.Sketch : NotebookInkChannel.Pencil;
                case NotebookInkChannel.Surface: return NotebookInkChannel.Paper;
                case NotebookInkChannel.Amber: return NotebookInkChannel.Blue;
                case NotebookInkChannel.Danger: return NotebookInkChannel.Red;
                case NotebookInkChannel.GlassInk: return NotebookInkChannel.Blue;
                case NotebookInkChannel.Copper: return channels > (int)NotebookInkChannel.Amber ? NotebookInkChannel.Amber : NotebookInkChannel.Red;
                case NotebookInkChannel.BladeSteel: return NotebookInkChannel.Pencil;
                case NotebookInkChannel.Violet: return NotebookInkChannel.Blue;
                case NotebookInkChannel.Clean: return NotebookInkChannel.White;
                case NotebookInkChannel.NotebookPaper: return channels > (int)NotebookInkChannel.Surface
                    ? NotebookInkChannel.Surface : NotebookInkChannel.Paper;
                default: return NotebookInkChannel.Navy;
            }
        }
    }
}
