using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public static partial class NightNotebookArt
    {
        // Equipment occupies its own local envelope, not a replacement recipe part.
        // Head attachments inherit recoil; receiver attachments inherit the mount rotation.
        static Rect PowerEquipment(int rank) => new Rect(-.045f - rank * .012f, .075f, .09f + rank * .024f, .18f);
        static Rect DriveEquipment(int rank) => new Rect(.065f, -.105f, .065f + rank * .016f, .15f);
        static Rect OpticsEquipment(int rank) => new Rect(-.185f, .015f, .095f, .12f + rank * .035f);
        static Rect ArmorEquipment(Rect hull, int rank) => new Rect(hull.xMin - .012f * rank, hull.yMin,
            hull.width + .024f * rank, hull.height + .018f * rank);
        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        internal static Rect DevelopmentBounds(AssemblyVisualFragment fragment, AssemblyDevelopmentPose development)
        {
            if (fragment.SlotIndex >= 0) development = development.ForWeapon(fragment.WeaponFamily);
            Rect bounds = fragment.Region;
            var category = Category(fragment);
            if (fragment.SlotIndex >= 0)
            {
                if (category == ComponentCategory.Head && development.Power > 0)
                    bounds = Union(bounds, PowerEquipment(development.Power));
                if (category == ComponentCategory.Mechanism)
                {
                    if (development.Mechanics > 0) bounds = Union(bounds, DriveEquipment(development.Mechanics));
                    if (development.Range > 0) bounds = Union(bounds, OpticsEquipment(development.Range));
                }
            }
            else if (category == ComponentCategory.Hull && development.Resilience > 0)
                bounds = Union(bounds, ArmorEquipment(bounds, development.Resilience));
            return bounds;
        }

        static Drawing Equipment(AssemblyVisualFragment fragment, Rect area, MeshPen pen, Vector2 at,
            float opacity, Color tint, float scale)
        {
            var equipment = new AssemblyVisualFragment(fragment.PartId, fragment.SpriteId, fragment.SlotIndex, fragment.Layer,
                area, area, fragment.Origin, fragment.Scale, fragment.Radians);
            return new Drawing(equipment, pen, at, opacity, tint, scale, default);
        }

        internal static void DrawDevelopment(AssemblyVisualFragment fragment, AssemblyDevelopmentPose development,
            MeshPen pen, Vector2 at, float opacity, Color tint, float scale)
        {
            if (pen == null) return;
            if (fragment.SlotIndex >= 0) development = development.ForWeapon(fragment.WeaponFamily);
            var category = Category(fragment);
            if (fragment.SlotIndex < 0)
            {
                if (category != ComponentCategory.Hull || development.Resilience == 0) return;
                var armor = Equipment(fragment, ArmorEquipment(fragment.Region, development.Resilience), pen, at, opacity, tint, scale);
                float height = .46f + development.Resilience * .16f;
                // Thick outer cheeks and structural cross-braces, leaving the feed visible.
                armor.SolidBox(.015f, .04f, .19f, height);
                armor.SolidBox(.795f, .04f, .19f, height);
                for (int i = 0; i < development.Resilience; i++)
                {
                    float y = .12f + i * .24f;
                    armor.SolidBox(.015f, y, .25f, .10f);
                    armor.SolidBox(.735f, y, .25f, .10f);
                }
                if (development.Resilience >= 2) armor.SolidBox(.20f, .06f, .60f, .15f);
                if (development.Resilience == 3)
                {
                    armor.Beam(.18f, .30f, .39f, .83f, .10f);
                    armor.Beam(.82f, .30f, .61f, .83f, .10f);
                }
                return;
            }
            if (category == ComponentCategory.Head)
            {
                if (development.Power > 0)
                {
                    var power = Equipment(fragment, PowerEquipment(development.Power), pen, at, opacity, tint, scale);
                    // Open jacket: broad side rails change the silhouette without moving the bore.
                    power.SolidBox(.02f, .04f, .24f, .91f);
                    power.SolidBox(.74f, .04f, .24f, .91f);
                    for (int i = 0; i < development.Power; i++)
                        power.SolidBox(.02f, .12f + i * .29f, .96f, .13f);
                }
                if (development.ColorRank > 0)
                {
                    var barrel = Equipment(fragment, fragment.Region, pen, at, opacity, tint, scale);
                    for (int i = 0; i < development.ColorRank; i++)
                        barrel.ColorBox(.24f, .19f + i * .20f, .52f, .11f, development.WeaponInk);
                }
                return;
            }
            if (category != ComponentCategory.Mechanism) return;
            if (development.Mechanics > 0)
            {
                var drive = Equipment(fragment, DriveEquipment(development.Mechanics), pen, at, opacity, tint, scale);
                drive.SolidBox(.03f, .07f, .94f, .40f);
                drive.Beam(.23f, .36f, .23f, .79f, .19f);
                drive.Disc(.48f, .69f, .42f, .28f);
                drive.Disc(.48f, .69f, .18f, .12f, true);
                for (int i = 0; i < development.Mechanics; i++)
                    drive.SolidBox(.08f + i * .29f, .08f, .19f, .32f);
                if (development.Mechanics == 3) drive.Beam(.18f, .76f, .82f, .43f, .12f);
            }
            if (development.Range > 0)
            {
                var optics = Equipment(fragment, OpticsEquipment(development.Range), pen, at, opacity, tint, scale);
                optics.Beam(.48f, .12f, .97f, .12f, .14f);
                optics.SolidBox(.24f, .08f, .44f, .86f);
                optics.SolidBox(.10f, .78f, .73f, .17f);
                optics.SolidBox(.31f, .84f, .31f, .08f, true);
                for (int i = 0; i < development.Range; i++)
                    optics.SolidBox(.16f, .19f + i * .19f, .61f, .10f);
            }
            if (development.ColorRank > 0)
            {
                var chamber = Equipment(fragment, fragment.Region, pen, at, opacity, tint, scale);
                // A substantial enamel chamber insert, not a floating colour dot or a whole-body tint.
                chamber.SolidBox(.23f, .25f, .54f, .30f, true);
                chamber.ColorBox(.27f, .29f, .46f, .22f, development.WeaponInk);
            }
        }
    }
}
