using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PaperDefense.Core
{
    public enum DrawingPartRole { Base, Mount, Module, Decoration }
    public enum DrawingVisualStyle { Template = 0, Legacy = 1 }
    public sealed class DrawingPartTemplate
    {
        public DrawingPartKey Key { get; }
        public string BindingId { get; }
        public DrawingPartRole Role { get; }
        public string DisplayName { get; }
        public float Weight { get; }
        public string ReferenceSignature { get; }
        public Point2 Pivot { get; }
        public IReadOnlyList<DrawingStroke> Contours { get; }
        internal DrawingPartTemplate(DrawingPartKey key, Point2 pivot, IReadOnlyList<DrawingStroke> contours)
            : this(DrawingBindings.For(key), LegacyRole(key), LegacyName(key), pivot, contours,
                key == DrawingPartKey.Decoration ? 0 : (int)key <= 2 ? 25 : 5, "legacy.v2." + key) { }
        internal DrawingPartTemplate(string bindingId, DrawingPartRole role, string displayName, Point2 pivot,
            IReadOnlyList<DrawingStroke> contours, float weight, string referenceSignature)
        {
            BindingId = bindingId; DrawingBindings.TryLegacy(bindingId, out DrawingPartKey key); Key = key;
            Role = role; DisplayName = displayName; Pivot = pivot;
            Contours = new DrawingPart(bindingId, contours).Strokes; Weight = weight; ReferenceSignature = referenceSignature;
        }
        static DrawingPartRole LegacyRole(DrawingPartKey key) => key == DrawingPartKey.Base ? DrawingPartRole.Base :
            key == DrawingPartKey.Mount0 || key == DrawingPartKey.Mount1 ? DrawingPartRole.Mount :
            key == DrawingPartKey.Decoration ? DrawingPartRole.Decoration : DrawingPartRole.Module;
        static string LegacyName(DrawingPartKey key)
        {
            switch (key)
            {
                case DrawingPartKey.Base: return UserText.Message("copy.0369683b41e6de45");
                case DrawingPartKey.Mount0: return UserText.Message("copy.9e98735021c6b210");
                case DrawingPartKey.Mount1: return UserText.Message("copy.375f6faa7a651fae");
                case DrawingPartKey.Armor: return UserText.Message("copy.e41e849ff6126cff");
                case DrawingPartKey.Feed: return UserText.Message("copy.d4a738612fc5e701");
                case DrawingPartKey.Tracking: return UserText.Message("copy.41e4c92b11a67cdf");
                case DrawingPartKey.Rail: return UserText.Message("copy.215d555ed53c9640");
                default: return UserText.Message("copy.da593f54cbd9f76c");
            }
        }
    }

    /// <summary>Reference outlines and assembly bindings. Player strokes never determine mounts or physical geometry.</summary>
    public static class DrawingTemplateCatalog
    {
        static readonly ConditionalWeakTable<TowerBlueprint, IReadOnlyList<DrawingPartTemplate>> cache = new ConditionalWeakTable<TowerBlueprint, IReadOnlyList<DrawingPartTemplate>>();
        public static bool IsEnabled(TowerBlueprint blueprint, DrawingPartKey key) => DrawingPartPolicy.IsEnabled(blueprint, key);
        public static bool IsEnabled(TowerBlueprint blueprint, string bindingId) => DrawingPartPolicy.IsEnabled(blueprint, bindingId);
        public static IReadOnlyList<DrawingPartKey> EnabledKeys(TowerBlueprint blueprint)
        {
            var keys = new List<DrawingPartKey>();
            foreach (var template in Create(blueprint)) if ((int)template.Key >= 0) keys.Add(template.Key);
            return keys.AsReadOnly();
        }
        public static IReadOnlyList<string> EnabledBindings(TowerBlueprint blueprint)
        {
            var keys = new List<string>(); foreach (var part in Create(blueprint)) keys.Add(part.BindingId); return keys.AsReadOnly();
        }
        public static Point2 Pivot(TowerBlueprint blueprint, DrawingPartKey key) => Pivot(blueprint, DrawingBindings.For(key));
        public static Point2 Pivot(TowerBlueprint blueprint, string bindingId)
        {
            foreach (var template in Create(blueprint)) if (template.BindingId == bindingId) return template.Pivot;
            // Disabled legacy parts are retained in the drawing but never rendered.
            if ((blueprint.Content == null || blueprint.Content.IsLegacyAdapter) && DrawingBindings.TryLegacy(bindingId, out var key)) return LegacyDrawingTemplates.Pivot(blueprint, key);
            throw new ArgumentException(UserText.Message("copy.7f4de9f87d912bac", bindingId), nameof(bindingId));
        }
        public static IReadOnlyList<DrawingPartTemplate> Create(TowerBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            return cache.GetValue(blueprint, Resolve);
        }
        static IReadOnlyList<DrawingPartTemplate> Resolve(TowerBlueprint blueprint)
        {
            if (blueprint.Content == null || blueprint.Content.IsLegacyAdapter)
            {
                if (!Enum.IsDefined(typeof(ChassisKind), blueprint.Chassis) || !Enum.IsDefined(typeof(StructureKind), blueprint.FirstWeapon) ||
                    (blueprint.SecondWeaponId != null && (!blueprint.SecondWeapon.HasValue || !Enum.IsDefined(typeof(StructureKind), blueprint.SecondWeapon.Value)))) return Array.Empty<DrawingPartTemplate>();
                foreach (var selected in blueprint.ModuleSelections) if (LegacyContentIds.Module(selected.DefinitionId) == TowerModules.None) return Array.Empty<DrawingPartTemplate>();
                return LegacyDrawingTemplates.Create(blueprint);
            }
            var content = blueprint.Content;
            var result = new List<DrawingPartTemplate>();
            if (!content.TryGetChassis(blueprint.ChassisId, out _)) return Array.Empty<DrawingPartTemplate>();
            foreach(var mount in blueprint.WeaponMounts)if(!content.TryGetWeapon(mount.WeaponId,out _))return Array.Empty<DrawingPartTemplate>();
            foreach (var selected in blueprint.ModuleSelections) if (!content.TryGetModule(selected.DefinitionId, out _)) return Array.Empty<DrawingPartTemplate>();
            var chassis = content.GetChassis(blueprint.ChassisId);
            Add(result, content.GetTemplate(chassis.TemplateId), DrawingBindings.Base, DrawingPartRole.Base, chassis.Name, null);
            foreach(var mount in blueprint.WeaponMounts)
            {
                var weapon=content.GetWeapon(mount.WeaponId);
                Add(result,content.GetTemplate(weapon.TemplateId),DrawingBindings.Mount(mount.SlotIndex),DrawingPartRole.Mount,
                    UserText.Message("copy.ad1cfa5b44f2fff4", (mount.SlotIndex+1), weapon.Name),chassis.GetMountPivot(mount.SlotIndex,blueprint.WeaponMounts.Count));
            }
            foreach (var selected in blueprint.ModuleSelections)
            {
                var module = content.GetModule(selected.DefinitionId);
                Add(result, content.GetTemplate(module.TemplateId), DrawingBindings.Module(selected.InstanceId), DrawingPartRole.Module, module.Name, null);
            }
            result.Add(new DrawingPartTemplate(DrawingBindings.Decoration, DrawingPartRole.Decoration, UserText.Message("copy.da593f54cbd9f76c"), new Point2(0, 0),
                Array.Empty<DrawingStroke>(), 0, "decoration.v1"));
            return result.AsReadOnly();
        }
        static void Add(List<DrawingPartTemplate> result, DrawingTemplateDefinition template, string binding, DrawingPartRole role, string name, Point2? pivot)
            => result.Add(new DrawingPartTemplate(binding, role, name, pivot ?? template.Pivot, template.Contours, template.Weight, template.Signature));
    }

    /// <summary>Archived v1/v2 reference geometry used exclusively by legacy pages and migration.</summary>
    public static class LegacyDrawingTemplates
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
            var result = new DrawingPartTemplate[keys.Count+(blueprint.GetWeaponId(2)!=null?1:0)];
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
            if(blueprint.GetWeaponId(2)!=null)
                result[result.Length-1]=new DrawingPartTemplate(DrawingBindings.Mount2,DrawingPartRole.Mount,UserText.Message("copy.3265cc01bf3a6da1"),new Point2(.24f,.04f),Weapon(LegacyContentIds.Weapon(blueprint.GetWeaponId(2))),25,"legacy.mount.2");
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
                default: throw new ArgumentOutOfRangeException(nameof(kind), UserText.Message("copy.78527a4b8e411f70"));
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
