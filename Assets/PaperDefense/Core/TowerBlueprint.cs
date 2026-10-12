using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum ChassisKind { Standard = 0, Heavy = 1 }
    // Zero is deliberately invalid: missing JSON origin metadata cannot become a legacy page.
    public enum DrawingOrigin { LegacyStandard = 1, PlayerDrawing = 2 }
    [Flags] public enum TowerModules { None = 0, Armor = 1, Feed = 2, Tracking = 4, Rail = 8 }

    /// <summary>A saved assembly is a value snapshot. Editing the album never refits deployed towers.</summary>
    public sealed class TowerBlueprint
    {
        public string Id { get; }
        public string Name { get; }
        public ChassisKind Chassis { get; }
        public StructureKind FirstWeapon { get; }
        public StructureKind? SecondWeapon { get; }
        public TowerModules Modules { get; }
        public DrawingSnapshot Drawing { get; }
        public DrawingOrigin Origin { get; }
        public DrawingAssessment Assessment { get; }
        public MortarFootprint FirstFootprint { get; }
        public MortarFootprint SecondFootprint { get; }
        public MortarFootprint GetMortarFootprint(int mount)
        {
            if (mount == 0 && FirstWeapon == StructureKind.Mortar) return FirstFootprint;
            if (mount == 1 && SecondWeapon == StructureKind.Mortar) return SecondFootprint;
            throw new ArgumentOutOfRangeException(nameof(mount), "Mount must contain a mortar.");
        }
        public TowerBlueprint WithMortarFootprint(int mount, MortarFootprint footprint)
        {
            GetMortarFootprint(mount);
            if (footprint == null) throw new ArgumentNullException(nameof(footprint));
            return new TowerBlueprint(Id, Name, Chassis, FirstWeapon, SecondWeapon, Modules, Drawing, Origin,
                mount == 0 ? footprint : FirstFootprint, mount == 1 ? footprint : SecondFootprint);
        }
        public TowerBlueprint AsVariant(string id, string name) => new TowerBlueprint(id, name, Chassis, FirstWeapon,
            SecondWeapon, Modules, Drawing.DeepCopy(), Origin, FirstFootprint, SecondFootprint);
        public TowerBlueprint(string id, string name, ChassisKind chassis, StructureKind firstWeapon,
            StructureKind? secondWeapon = null, TowerModules modules = TowerModules.None,
            DrawingSnapshot drawing = null, DrawingOrigin origin = DrawingOrigin.LegacyStandard,
            MortarFootprint firstFootprint = null, MortarFootprint secondFootprint = null)
        {
            if (!Enum.IsDefined(typeof(DrawingOrigin), origin)) throw new ArgumentOutOfRangeException(nameof(origin));
            Id = id?.Trim() ?? ""; Name = name?.Trim() ?? ""; Chassis = chassis; FirstWeapon = firstWeapon; SecondWeapon = secondWeapon; Modules = modules;
            Drawing = drawing ?? DrawingSnapshot.Empty; Origin = origin;
            FirstFootprint = firstFootprint ?? MortarFootprint.Circle; SecondFootprint = secondFootprint ?? MortarFootprint.Circle;
            if ((firstWeapon != StructureKind.Mortar && FirstFootprint.AspectRatio != 1) ||
                (secondWeapon != StructureKind.Mortar && SecondFootprint.AspectRatio != 1))
                throw new ArgumentException("Only mortar mounts may have a noncircular footprint.");
            if (origin == DrawingOrigin.LegacyStandard && !Drawing.IsEmpty)
                throw new ArgumentException("Авторские линии требуют пометки PlayerDrawing.", nameof(origin));
            Assessment = DrawingEvaluator.Evaluate(this);
        }
        public static TowerBlueprint NewDraft(string id, string name, ChassisKind chassis, StructureKind firstWeapon,
            StructureKind? secondWeapon = null, TowerModules modules = TowerModules.None)
            => new TowerBlueprint(id, name, chassis, firstWeapon, secondWeapon, modules, DrawingSnapshot.Empty, DrawingOrigin.PlayerDrawing);
        public TowerBlueprint WithName(string name) => new TowerBlueprint(Id, name, Chassis, FirstWeapon, SecondWeapon, Modules, Drawing, Origin, FirstFootprint, SecondFootprint);
        public TowerBlueprint WithDrawing(DrawingSnapshot drawing) => new TowerBlueprint(Id, Name, Chassis, FirstWeapon, SecondWeapon, Modules,
            drawing ?? throw new ArgumentNullException(nameof(drawing)), DrawingOrigin.PlayerDrawing, FirstFootprint, SecondFootprint);
        public TowerBlueprint WithAssembly(ChassisKind chassis, StructureKind firstWeapon, StructureKind? secondWeapon, TowerModules modules)
        {
            if (chassis == Chassis && firstWeapon == FirstWeapon && secondWeapon == SecondWeapon && modules == Modules) return this;
            MortarFootprint first = firstWeapon == FirstWeapon ? FirstFootprint : MortarFootprint.Circle;
            MortarFootprint second = secondWeapon == SecondWeapon ? SecondFootprint : MortarFootprint.Circle;
            if (Origin == DrawingOrigin.LegacyStandard)
                return new TowerBlueprint(Id, Name, chassis, firstWeapon, secondWeapon, modules, DrawingSnapshot.Empty,
                    DrawingOrigin.PlayerDrawing, first, second);
            var retained = new List<DrawingPart>();
            foreach (DrawingPart part in Drawing.Parts)
                if (DrawingPartPolicy.ShouldRetain(part.Key, this, chassis, firstWeapon, secondWeapon, modules)) retained.Add(part);
            return new TowerBlueprint(Id, Name, chassis, firstWeapon, secondWeapon, modules,
                new DrawingSnapshot(retained), DrawingOrigin.PlayerDrawing, first, second);
        }
        public TowerBlueprint AsNew(string id, string name) => new TowerBlueprint(id, name, Chassis, FirstWeapon, SecondWeapon, Modules,
            Origin == DrawingOrigin.PlayerDrawing ? Drawing.DeepCopy() : DrawingSnapshot.Empty, DrawingOrigin.PlayerDrawing,
            FirstFootprint, SecondFootprint);
    }

    public static class TowerCatalog
    {
        static readonly TowerConstructionRules historicalRules = new TowerConstructionRules();
        static TowerConstructionRules Rules(GameBalance balance) => balance == null ? historicalRules : balance.Construction;
        /// <summary>Historical direct-tool price; retain its int/float operation order separately from assembly Cost.</summary>
        public static int ToolCost(GameBalance balance, StructureKind kind, MaterialTier tier) =>
            (int)Math.Ceiling(Math.Max(0, balance.Structure(kind).Cost) * Math.Max(0, balance.Material(tier).CostMultiplier));
        public static float QualityMultiplier(TowerBlueprint blueprint, GameBalance balance)
            => blueprint == null || blueprint.Origin == DrawingOrigin.LegacyStandard ? 1 : QualityMultiplier(blueprint.Assessment.Quality, balance);
        internal static float QualityMultiplier(float quality, GameBalance balance)
            => DrawingEvaluator.FireMultiplier(quality, balance.DrawingMinimumRate, balance.DrawingMaximumRate);
        public static float ActionInterval(StructureDefinition definition, float productionMultiplier, float qualityMultiplier)
        {
            if (qualityMultiplier <= 0 || qualityMultiplier > 1 || float.IsNaN(qualityMultiplier) || float.IsInfinity(qualityMultiplier))
                throw new ArgumentOutOfRangeException(nameof(qualityMultiplier));
            // Construction/feed and the historical minimum interval limit production before drawing quality.
            return Math.Max(.05f, definition.ShotInterval / Math.Max(.01f, productionMultiplier)) / qualityMultiplier;
        }
        public static float ActionInterval(TowerBlueprint blueprint, GameBalance balance, StructureKind kind, float productionMultiplier, float qualityMultiplier)
            => ActionInterval(balance.Structure(kind), productionMultiplier, qualityMultiplier) *
                (blueprint != null && (blueprint.Modules & TowerModules.Rail) != 0 ? Rules(balance).RailIntervalMultiplier : 1);
        public static int Capacity(ChassisKind chassis) => Capacity(chassis, null);
        public static int Capacity(TowerBlueprint blueprint) => Capacity(blueprint.Chassis);
        public static int Capacity(ChassisKind chassis, GameBalance balance)
            => chassis == ChassisKind.Heavy ? Rules(balance).HeavyCapacity : Rules(balance).StandardCapacity;
        public static int Capacity(TowerBlueprint blueprint, GameBalance balance) => Capacity(blueprint.Chassis, balance);
        public static int Load(TowerBlueprint blueprint) => Load(blueprint, null);
        public static int Load(TowerBlueprint blueprint, GameBalance balance)
        {
            var rules = Rules(balance);
            long load = (long)rules.WeaponLoad(blueprint.FirstWeapon) +
                (blueprint.SecondWeapon.HasValue ? rules.WeaponLoad(blueprint.SecondWeapon.Value) : 0) + rules.ModuleLoad(blueprint.Modules);
            return (int)Math.Min(int.MaxValue, load);
        }
        public static bool IsMountPart(StructureKind kind) => PartCatalog.TryGetMount(kind, out _);
        static bool IsWeapon(StructureKind kind) => IsMountPart(kind);
        public static float WeaponRange(TowerBlueprint blueprint, GameBalance balance, StructureKind kind) => balance.Structure(kind).Range *
            (blueprint != null && (blueprint.Modules & TowerModules.Rail) != 0 ? Rules(balance).RailRangeMultiplier : 1);
        // Saved pages retain their construction when balance changes. Capacity belongs to commands, not the file format.
        public static bool ValidateParts(TowerBlueprint blueprint, out string reason)
        {
            reason = "";
            if (blueprint == null) reason = "Выберите чертёж.";
            else if (blueprint.Id.Length == 0 || blueprint.Id.Length > ProfileLimits.MaximumBlueprintIdLength ||
                blueprint.Name.Length == 0 || blueprint.Name.Length > ProfileLimits.MaximumBlueprintNameLength)
                reason = "Нужны идентификатор и название до " + ProfileLimits.MaximumBlueprintNameLength + " знаков.";
            else if (!Enum.IsDefined(typeof(ChassisKind), blueprint.Chassis)) reason = "Неизвестное основание.";
            else if ((blueprint.Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking | TowerModules.Rail)) != 0) reason = "Неизвестный модуль.";
            else if (!IsWeapon(blueprint.FirstWeapon) || (blueprint.SecondWeapon.HasValue && !IsWeapon(blueprint.SecondWeapon.Value))) reason = "Крепления принимают только оружейные детали каталога.";
            return reason.Length == 0;
        }
        public static bool Validate(TowerBlueprint blueprint, out string reason) => Validate(blueprint, null, out reason);
        public static bool Validate(TowerBlueprint blueprint, GameBalance balance, out string reason)
        {
            if (!ValidateParts(blueprint, out reason)) return false;
            int excess = Load(blueprint, balance) - Capacity(blueprint, balance);
            if (excess > 0) reason = "Не хватает " + excess + " ед. нагрузки. Снимите оружие или выберите другое основание.";
            return reason.Length == 0;
        }
        public static int Cost(TowerBlueprint blueprint, GameBalance balance, MaterialTier tier)
        {
            var rules = Rules(balance);
            long cost = blueprint.Chassis == ChassisKind.Heavy ? rules.HeavyBaseCost : rules.StandardBaseCost;
            cost += Math.Max(0, balance.Structure(blueprint.FirstWeapon).Cost - rules.WeaponAssemblyCredit);
            if (blueprint.SecondWeapon.HasValue) cost += Math.Max(0, balance.Structure(blueprint.SecondWeapon.Value).Cost - rules.WeaponAssemblyCredit);
            if ((blueprint.Modules & TowerModules.Armor) != 0) cost += rules.ArmorCost;
            if ((blueprint.Modules & TowerModules.Feed) != 0) cost += rules.FeedCost;
            if ((blueprint.Modules & TowerModules.Tracking) != 0) cost += rules.TrackingCost;
            if ((blueprint.Modules & TowerModules.Rail) != 0) cost += rules.RailCost;
            return (int)Math.Min(int.MaxValue, Math.Ceiling(cost * (double)Math.Max(0, balance.Material(tier).CostMultiplier)));
        }
        public static float Health(TowerBlueprint blueprint, GameBalance balance, MaterialTier tier)
        {
            var rules = Rules(balance);
            float health = rules.FoundationHealth;
            if (blueprint.Chassis == ChassisKind.Heavy) health *= rules.HeavyHealthMultiplier;
            if ((blueprint.Modules & TowerModules.Armor) != 0) health *= rules.ArmorHealthMultiplier;
            return Math.Max(1, health * balance.Material(tier).HealthMultiplier);
        }
        public static float Defense(TowerBlueprint blueprint, GameBalance balance)
            => blueprint != null && (blueprint.Modules & TowerModules.Armor) != 0 ? Rules(balance).ArmorDefense : 0;
        public static int ColorCost(TowerBlueprint blueprint,GameBalance balance)
            => (int)Math.Max(Rules(balance).ColorMinimum, ((long)Cost(blueprint,balance,MaterialTier.Pencil)+Rules(balance).ColorCostDivisor-1)/Rules(balance).ColorCostDivisor);
        public static float BuildSeconds(TowerBlueprint blueprint, GameBalance balance, MaterialTier tier)
        {
            var rules = Rules(balance);
            float seconds = balance.Structure(blueprint.FirstWeapon).BuildSeconds;
            if (blueprint.SecondWeapon.HasValue) seconds = Math.Max(seconds, balance.Structure(blueprint.SecondWeapon.Value).BuildSeconds) + rules.SecondaryBuildSeconds;
            if (blueprint.Chassis == ChassisKind.Heavy) seconds += rules.HeavyBuildSeconds;
            if ((blueprint.Modules & TowerModules.Armor) != 0) seconds += rules.ArmorBuildSeconds;
            if ((blueprint.Modules & TowerModules.Feed) != 0) seconds += rules.FeedBuildSeconds;
            if ((blueprint.Modules & TowerModules.Tracking) != 0) seconds += rules.TrackingBuildSeconds;
            if ((blueprint.Modules & TowerModules.Rail) != 0) seconds += rules.RailBuildSeconds;
            return Math.Max(0, seconds * balance.Material(tier).BuildTimeMultiplier);
        }
        public static float FeedMultiplier(TowerBlueprint blueprint) => FeedMultiplier(blueprint, null);
        public static float AcquisitionSeconds(TowerBlueprint blueprint) => AcquisitionSeconds(blueprint, null);
        public static float FeedMultiplier(TowerBlueprint blueprint, GameBalance balance, int activeMounts)
        {
            var rules = Rules(balance);
            if (rules.DynamicFeed)
            {
                if (activeMounts <= 0) return 0;
                float module = (blueprint.Modules & TowerModules.Feed) != 0 ? rules.FeedModuleMultiplier : 1;
                return Math.Min(rules.MaxFeedMultiplier, Math.Min(module, rules.DynamicFeedCapacity * module / activeMounts));
            }
            return Math.Min(rules.MaxFeedMultiplier, (blueprint.SecondWeapon.HasValue ? rules.DualFeedMultiplier : 1) *
                ((blueprint.Modules & TowerModules.Feed) != 0 ? rules.FeedModuleMultiplier : 1));
        }
        public static float FeedMultiplier(TowerBlueprint blueprint, GameBalance balance)
            => FeedMultiplier(blueprint, balance, blueprint.SecondWeapon.HasValue ? 2 : 1);
        public static float AcquisitionSeconds(TowerBlueprint blueprint, GameBalance balance)
            => (blueprint.Modules & TowerModules.Tracking) != 0 ? Rules(balance).TrackingAcquisitionSeconds : Rules(balance).AcquisitionSeconds;
    }

    [Serializable] public sealed class BlueprintData
    {
        public string Id, Name;
        public ChassisKind Chassis;
        public StructureKind FirstWeapon, SecondWeapon;
        public bool HasSecondWeapon;
        public TowerModules Modules;
        public int DrawingMetadataVersion;
        public int FootprintMetadataVersion;
        public float FirstFootprintAspect, SecondFootprintAspect;
        public DrawingOrigin Origin = (DrawingOrigin)(-1);
        public DrawingData Drawing;
        public BlueprintData() { }
        public BlueprintData(TowerBlueprint blueprint)
        { Id = blueprint.Id; Name = blueprint.Name; Chassis = blueprint.Chassis; FirstWeapon = blueprint.FirstWeapon;
            HasSecondWeapon = blueprint.SecondWeapon.HasValue; SecondWeapon = blueprint.SecondWeapon ?? StructureKind.Gun; Modules = blueprint.Modules;
            DrawingMetadataVersion = 1; Origin = blueprint.Origin; Drawing = new DrawingData(blueprint.Drawing);
            FootprintMetadataVersion = 1; FirstFootprintAspect = blueprint.FirstFootprint.AspectRatio; SecondFootprintAspect = blueprint.SecondFootprint.AspectRatio; }
        public TowerBlueprint ToBlueprint(int profileVersion = WorkshopProfile.CurrentVersion)
        {
            if (profileVersion > WorkshopProfile.CurrentVersion) throw new NotSupportedException("Newer profile version.");
            if (profileVersion < 1) throw new ArgumentException("Unknown profile version.");
            if (profileVersion == 1)
                return new TowerBlueprint(Id, Name, Chassis, FirstWeapon, HasSecondWeapon ? SecondWeapon : (StructureKind?)null, Modules);
            if (profileVersion >= 3 &&
                (!Enum.IsDefined(typeof(ChassisKind), Chassis) || !Enum.IsDefined(typeof(StructureKind), FirstWeapon) ||
                 (HasSecondWeapon && !Enum.IsDefined(typeof(StructureKind), SecondWeapon)) ||
                 (Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking | TowerModules.Rail)) != 0))
                throw new NotSupportedException("Чертёж содержит неизвестную этой версии игры деталь.");
            if (profileVersion == 2 &&
                (!HistoricalWeapon(FirstWeapon) || (HasSecondWeapon && !HistoricalWeapon(SecondWeapon)) ||
                 (Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking)) != 0))
                throw new ArgumentException("Неизвестная деталь профиля версии 2.");
            if (DrawingMetadataVersion > 1) throw new NotSupportedException("Более новая версия данных страницы.");
            if (DrawingMetadataVersion != 1 || !Enum.IsDefined(typeof(DrawingOrigin), Origin) || Drawing == null)
                throw new ArgumentException("У страницы отсутствуют данные происхождения рисунка.");
            if (profileVersion == 2 && Drawing.FormatVersion > 1)
                throw new NotSupportedException("Профиль версии 2 содержит более новый формат рисунка.");
            MortarFootprint first = MortarFootprint.Circle, second = MortarFootprint.Circle;
            if (profileVersion == 4)
            {
                if (FootprintMetadataVersion > 1) throw new NotSupportedException("Newer footprint metadata.");
                if (FootprintMetadataVersion != 1) throw new ArgumentException("Missing footprint metadata.");
                first = new MortarFootprint(FirstFootprintAspect); second = new MortarFootprint(SecondFootprintAspect);
            }
            return new TowerBlueprint(Id, Name, Chassis, FirstWeapon, HasSecondWeapon ? SecondWeapon : (StructureKind?)null,
                Modules, Drawing.ToSnapshot(), Origin, first, second);
        }
        static bool HistoricalWeapon(StructureKind kind) => kind == StructureKind.Gun || kind == StructureKind.Piercer || kind == StructureKind.Mortar;
    }
    [Serializable] public sealed class DrawingData
    {
        public int FormatVersion, BindingVersion;
        public DrawingPartData[] Parts;
        public DrawingData() { }
        public DrawingData(DrawingSnapshot snapshot)
        {
            FormatVersion = snapshot.FormatVersion; BindingVersion = snapshot.BindingVersion;
            Parts = new DrawingPartData[snapshot.Parts.Count];
            for (int i = 0; i < Parts.Length; i++) Parts[i] = new DrawingPartData(snapshot.Parts[i]);
        }
        public DrawingSnapshot ToSnapshot()
        {
            if (FormatVersion > DrawingSnapshot.CurrentFormatVersion || BindingVersion > DrawingSnapshot.CurrentBindingVersion)
                throw new NotSupportedException("Рисунок создан более новой версией игры.");
            int expectedParts = FormatVersion == 1 ? 7 : 8;
            if (FormatVersion < 1 || BindingVersion != DrawingSnapshot.CurrentBindingVersion ||
                Parts == null || Parts.Length != expectedParts)
                throw new ArgumentException("Неверная версия или состав рисунка.");
            var parts = new DrawingPart[Parts.Length];
            var seen = new bool[expectedParts];
            int strokeCount = 0, pointCount = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                DrawingPartData part = Parts[i];
                if (part == null || part.Strokes == null) throw new ArgumentException("Повреждённая часть рисунка.");
                int key = (int)part.Key;
                if (key < 0 || key >= expectedParts || seen[key]) throw new ArgumentException("Неверный или повторяющийся ключ части рисунка.");
                seen[key] = true;
                strokeCount += part.Strokes.Length;
                if (strokeCount > DrawingSnapshot.MaxStrokes) throw new ArgumentException("Слишком много штрихов.");
                var strokes = new DrawingStroke[part.Strokes.Length];
                for (int s = 0; s < strokes.Length; s++)
                {
                    DrawingStrokeData source = part.Strokes[s];
                    if (source == null || source.Points == null) throw new ArgumentException("Повреждённый штрих.");
                    pointCount += source.Points.Length;
                    if (pointCount > DrawingSnapshot.MaxPoints) throw new ArgumentException("Слишком много точек.");
                    var points = new DrawingPoint[source.Points.Length];
                    for (int p = 0; p < points.Length; p++)
                    {
                        DrawingPointData point = source.Points[p];
                        if (point == null) throw new ArgumentException("Повреждённая точка.");
                        points[p] = new DrawingPoint(point.X, point.Y);
                    }
                    strokes[s] = new DrawingStroke(points);
                }
                parts[i] = new DrawingPart(part.Key, strokes);
            }
            return new DrawingSnapshot(parts);
        }
    }
    [Serializable] public sealed class DrawingPartData
    {
        public DrawingPartKey Key;
        public DrawingStrokeData[] Strokes;
        public DrawingPartData() { }
        public DrawingPartData(DrawingPart part)
        {
            Key = part.Key; Strokes = new DrawingStrokeData[part.Strokes.Count];
            for (int i = 0; i < Strokes.Length; i++) Strokes[i] = new DrawingStrokeData(part.Strokes[i]);
        }
    }
    [Serializable] public sealed class DrawingStrokeData
    {
        public DrawingPointData[] Points;
        public DrawingStrokeData() { }
        public DrawingStrokeData(DrawingStroke stroke)
        {
            Points = new DrawingPointData[stroke.Points.Count];
            for (int i = 0; i < Points.Length; i++) Points[i] = new DrawingPointData { X = stroke.Points[i].X, Y = stroke.Points[i].Y };
        }
    }
    [Serializable] public sealed class DrawingPointData { public float X, Y; }
}
