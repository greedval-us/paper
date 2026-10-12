using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed class DrawingPartTemplate
    {
        public DrawingPartKey Key { get; }
        public Point2 Pivot { get; }
        public IReadOnlyList<DrawingStroke> Contours { get; }
        internal DrawingPartTemplate(DrawingPartKey key, Point2 pivot, IReadOnlyList<DrawingStroke> contours)
        { Key = key; Pivot = pivot; Contours = new DrawingPart(key, contours).Strokes; }
    }

    /// <summary>Reference outlines and assembly bindings. Player strokes never determine mounts or physical geometry.</summary>
    public static class DrawingTemplateCatalog
    {
        static readonly DrawingPartKey[] partOrder = { DrawingPartKey.Base, DrawingPartKey.Mount0, DrawingPartKey.Mount1,
            DrawingPartKey.Armor, DrawingPartKey.Feed, DrawingPartKey.Tracking, DrawingPartKey.Rail, DrawingPartKey.Decoration };

        public static bool IsEnabled(TowerBlueprint blueprint, DrawingPartKey key) => DrawingPartPolicy.IsEnabled(blueprint, key);

        public static IReadOnlyList<DrawingPartKey> EnabledKeys(TowerBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            var keys = new List<DrawingPartKey>(partOrder.Length);
            foreach (DrawingPartKey key in partOrder) if (IsEnabled(blueprint, key)) keys.Add(key);
            return keys.AsReadOnly();
        }

        public static Point2 Pivot(TowerBlueprint blueprint, DrawingPartKey key)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            switch (key)
            {
                case DrawingPartKey.Base: case DrawingPartKey.Decoration: case DrawingPartKey.Rail: return new Point2(0, 0);
                case DrawingPartKey.Mount0: return new Point2(blueprint.SecondWeapon.HasValue ? -.17f : 0, .04f);
                case DrawingPartKey.Mount1: return new Point2(.17f, .04f);
                case DrawingPartKey.Armor: case DrawingPartKey.Feed: return new Point2(0, -.08f);
                case DrawingPartKey.Tracking: return new Point2(0, -.16f);
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        public static IReadOnlyList<DrawingPartTemplate> Create(TowerBlueprint blueprint)
        {
            IReadOnlyList<DrawingPartKey> keys = EnabledKeys(blueprint);
            var result = new DrawingPartTemplate[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                DrawingPartKey key = keys[i];
                IReadOnlyList<DrawingStroke> contours;
                switch (key)
                {
                    case DrawingPartKey.Base:
                        float width = blueprint.Chassis == ChassisKind.Heavy ? .44f : .38f;
                        float height = blueprint.Chassis == ChassisKind.Heavy ? .32f : .26f;
                        contours = new[] { Rectangle(-width, -height, width, height), Rectangle(-width + .07f, -height + .07f, width - .07f, height - .07f) };
                        break;
                    case DrawingPartKey.Mount0: contours = Weapon(blueprint.FirstWeapon); break;
                    case DrawingPartKey.Mount1: contours = Weapon(blueprint.SecondWeapon.Value); break;
                    case DrawingPartKey.Armor:
                        contours = new[] { Line(-.38f, -.12f, -.22f, -.2f, 0, -.15f, .22f, -.2f, .38f, -.12f),
                            Line(-.38f, .12f, -.22f, .2f, 0, .15f, .22f, .2f, .38f, .12f) };
                        break;
                    case DrawingPartKey.Feed:
                        contours = new[] { Rectangle(-.13f, -.12f, .13f, .12f), Line(-.2f, -.18f, -.2f, .02f, -.13f, .02f),
                            Line(.2f, -.18f, .2f, .02f, .13f, .02f) };
                        break;
                    case DrawingPartKey.Tracking:
                        contours = new[] { Circle(0, 0, .12f), Line(-.19f, 0, -.08f, 0), Line(.08f, 0, .19f, 0),
                            Line(0, -.19f, 0, -.08f), Line(0, .08f, 0, .19f) };
                        break;
                    case DrawingPartKey.Rail:
                        contours = new[] { Line(-.12f, -.3f, -.12f, .58f), Line(.12f, -.3f, .12f, .58f),
                            Line(-.16f, -.2f, .16f, -.2f), Line(-.16f, .14f, .16f, .14f) };
                        break;
                    default: contours = Array.Empty<DrawingStroke>(); break;
                }
                result[i] = new DrawingPartTemplate(key, Pivot(blueprint, key), contours);
            }
            return Array.AsReadOnly(result);
        }

        static IReadOnlyList<DrawingStroke> Weapon(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Gun:
                    return new[] { Circle(0, 0, .1f), Rectangle(-.055f, .02f, .055f, .45f), Line(-.055f, .34f, .055f, .34f) };
                case StructureKind.Piercer:
                    return new[] { Circle(0, 0, .1f), Line(-.065f, .01f, -.065f, .38f, 0, .5f, .065f, .38f, .065f, .01f),
                        Line(0, .08f, 0, .41f) };
                case StructureKind.Mortar:
                    return new[] { Circle(0, 0, .13f), Rectangle(-.115f, .02f, .115f, .31f), Circle(0, .31f, .115f) };
                case StructureKind.Fan:
                    return new[] { Circle(0, 0, .1f), Line(-.055f, .02f, -.2f, .35f, .2f, .35f, .055f, .02f),
                        Line(-.145f, .25f, .145f, .25f) };
                case StructureKind.Ricochet:
                    return new[] { Circle(0, 0, .1f), Line(-.055f, .02f, -.15f, .14f, -.045f, .25f, -.15f, .36f,
                        0, .5f, .15f, .36f, .045f, .25f, .15f, .14f, .055f, .02f),
                        Line(-.08f, .25f, .08f, .25f) };
                case StructureKind.Stapler:
                    return new[] { Circle(0, 0, .1f), Rectangle(-.07f, .02f, .07f, .3f),
                        Line(-.17f, .34f, -.17f, .49f, .17f, .49f, .17f, .34f),
                        Line(-.09f, .34f, -.09f, .42f, .09f, .42f, .09f, .34f) };
                case StructureKind.Blotter:
                    return new[] { Circle(0, 0, .1f), Rectangle(-.065f, .02f, .065f, .24f),
                        Line(-.24f, .25f, -.24f, .45f, .24f, .45f, .24f, .25f, .16f, .21f,
                            .08f, .25f, 0, .21f, -.08f, .25f, -.16f, .21f, -.24f, .25f),
                        Line(-.18f, .32f, .18f, .32f), Line(-.18f, .39f, .18f, .39f) };
                default: throw new ArgumentOutOfRangeException(nameof(kind), "Для этой детали ещё нет образца рисунка.");
            }
        }

        static DrawingStroke Rectangle(float left, float bottom, float right, float top)
            => Line(left, bottom, right, bottom, right, top, left, top, left, bottom);
        static DrawingStroke Circle(float x, float y, float radius)
        {
            const int segments = 16;
            var points = new DrawingPoint[segments + 1];
            for (int i = 0; i < points.Length; i++)
            {
                double angle = i * 2 * Math.PI / segments;
                points[i] = new DrawingPoint(x + (float)Math.Cos(angle) * radius, y + (float)Math.Sin(angle) * radius);
            }
            return new DrawingStroke(points);
        }
        static DrawingStroke Line(params float[] xy)
        {
            var points = new DrawingPoint[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new DrawingPoint(xy[i * 2], xy[i * 2 + 1]);
            return new DrawingStroke(points);
        }
    }
}
