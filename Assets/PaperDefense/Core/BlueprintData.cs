using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    [Serializable] public sealed class BlueprintData
    {
        public string Id, Name;
        public int ContentMetadataVersion;
        public string ChassisId, FirstWeaponId, SecondWeaponId;
        public ModuleSelectionData[] ModuleSelections;
        public int EvaluationMetadataVersion;
        public DrawingEvaluationData Evaluation;
        // Historical numeric fields remain migration-only. Their values are never reassigned.
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
            ContentMetadataVersion = 1; ChassisId = blueprint.ChassisId; FirstWeaponId = blueprint.FirstWeaponId; SecondWeaponId = blueprint.SecondWeaponId;
            ModuleSelections = new ModuleSelectionData[blueprint.ModuleSelections.Count];
            for (int i = 0; i < ModuleSelections.Length; i++) ModuleSelections[i] = new ModuleSelectionData(blueprint.ModuleSelections[i]);
            EvaluationMetadataVersion = 1; Evaluation = new DrawingEvaluationData(blueprint.Assessment.ToStamp());
            DrawingMetadataVersion = 1; Origin = blueprint.Origin; Drawing = new DrawingData(blueprint.Drawing);
            FootprintMetadataVersion = 1; FirstFootprintAspect = blueprint.FirstFootprint.AspectRatio; SecondFootprintAspect = blueprint.SecondFootprint.AspectRatio; }
        public TowerBlueprint ToBlueprint(int profileVersion = WorkshopProfile.CurrentVersion)
        {
            if (profileVersion > WorkshopProfile.CurrentVersion) throw new NotSupportedException("Newer profile version.");
            if (profileVersion < 1) throw new ArgumentException("Unknown profile version.");
            if (profileVersion == 5) return ToContentBlueprint();
            if (!Enum.IsDefined(typeof(ChassisKind), Chassis) || !Enum.IsDefined(typeof(StructureKind), FirstWeapon) ||
                (HasSecondWeapon && !Enum.IsDefined(typeof(StructureKind), SecondWeapon)) ||
                (Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking | TowerModules.Rail)) != 0)
                throw new NotSupportedException("Чертёж содержит неизвестную этой версии игры деталь.");
            if (profileVersion == 1)
                return new TowerBlueprint(Id, Name, Chassis, FirstWeapon, HasSecondWeapon ? SecondWeapon : (StructureKind?)null, Modules);
            if (profileVersion == 2 &&
                (!HistoricalWeapon(FirstWeapon) || (HasSecondWeapon && !HistoricalWeapon(SecondWeapon)) ||
                 (Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking)) != 0))
                throw new ArgumentException("Неизвестная деталь профиля версии 2.");
            if (DrawingMetadataVersion > 1) throw new NotSupportedException("Более новая версия данных страницы.");
            if (!Enum.IsDefined(typeof(DrawingOrigin), Origin) && (int)Origin > (int)DrawingOrigin.PlayerDrawing)
                throw new NotSupportedException("Более новый вид происхождения рисунка.");
            if (DrawingMetadataVersion != 1 || !Enum.IsDefined(typeof(DrawingOrigin), Origin) || Drawing == null)
                throw new ArgumentException("У страницы отсутствуют данные происхождения рисунка.");
            if (profileVersion == 2 && Drawing.FormatVersion > 1)
                throw new NotSupportedException("Профиль версии 2 содержит более новый формат рисунка.");
            if (Drawing.FormatVersion > 2 || Drawing.BindingVersion > 1)
                throw new NotSupportedException("Исторический профиль содержит более новую схему привязок рисунка.");
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
        TowerBlueprint ToContentBlueprint()
        {
            if (ContentMetadataVersion > 1 || EvaluationMetadataVersion > 1 || DrawingMetadataVersion > 1 || FootprintMetadataVersion > 1)
                throw new NotSupportedException("Более новая версия данных страницы.");
            if (!Enum.IsDefined(typeof(DrawingOrigin), Origin) && (int)Origin > (int)DrawingOrigin.PlayerDrawing)
                throw new NotSupportedException("Более новый вид происхождения рисунка.");
            if (ContentMetadataVersion != 1 || EvaluationMetadataVersion != 1 || Evaluation == null ||
                DrawingMetadataVersion != 1 || FootprintMetadataVersion != 1 || Drawing == null ||
                !Enum.IsDefined(typeof(DrawingOrigin), Origin) || ModuleSelections == null || ModuleSelections.Length > DrawingSnapshot.MaximumParts)
                throw new ArgumentException("Отсутствуют данные состава или рисунка страницы.");
            if (string.IsNullOrWhiteSpace(ChassisId) || string.IsNullOrWhiteSpace(FirstWeaponId) ||
                (HasSecondWeapon && string.IsNullOrWhiteSpace(SecondWeaponId)) ||
                (!HasSecondWeapon && !string.IsNullOrEmpty(SecondWeaponId))) throw new ArgumentException("Отсутствует или противоречит флагу ID детали страницы.");
            var modules = new ModuleSelection[ModuleSelections.Length];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < modules.Length; i++)
            {
                ModuleSelectionData source = ModuleSelections[i];
                if (source == null || !seen.Add(source.InstanceId ?? "")) throw new ArgumentException("Повреждённое или повторяющееся оборудование страницы.");
                modules[i] = new ModuleSelection(source.InstanceId, source.DefinitionId);
            }
            return new TowerBlueprint(Id, Name, ChassisId, FirstWeaponId, HasSecondWeapon ? SecondWeaponId : null, modules,
                Drawing.ToSnapshot(), Origin, MortarFootprint.CreateStored(FirstFootprintAspect), MortarFootprint.CreateStored(SecondFootprintAspect),
                evaluationStamp: Evaluation.ToStamp());
        }
        static bool HistoricalWeapon(StructureKind kind) => kind == StructureKind.Gun || kind == StructureKind.Piercer || kind == StructureKind.Mortar;
    }
    [Serializable] public sealed class ModuleSelectionData
    {
        public string InstanceId, DefinitionId;
        public ModuleSelectionData() { }
        public ModuleSelectionData(ModuleSelection selection) { InstanceId = selection.InstanceId; DefinitionId = selection.DefinitionId; }
    }
    [Serializable] public sealed class DrawingEvaluationData
    {
        public int RuleVersion;
        public string ReferenceSignature, DrawingHash, ConstructionHash;
        public float Quality;
        public DrawingFeedbackData[] Feedback;
        public DrawingEvaluationData() { }
        public DrawingEvaluationData(DrawingEvaluationStamp stamp)
        {
            RuleVersion = stamp.RuleVersion; ReferenceSignature = stamp.ReferenceSignature; DrawingHash = stamp.DrawingHash;
            ConstructionHash = stamp.ConstructionHash; Quality = stamp.Quality;
            Feedback = new DrawingFeedbackData[stamp.PartFeedback.Count];
            for (int i = 0; i < Feedback.Length; i++) Feedback[i] = new DrawingFeedbackData(stamp.PartFeedback[i]);
        }
        public DrawingEvaluationStamp ToStamp()
        {
            if (RuleVersion < 1 || Feedback == null || Feedback.Length > DrawingSnapshot.MaximumParts)
                throw new ArgumentException("Повреждённая оценка рисунка.");
            var feedback = new DrawingPartFeedback[Feedback.Length];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < feedback.Length; i++)
            {
                var source = Feedback[i];
                if (source == null || !seen.Add(source.BindingId ?? "")) throw new ArgumentException("Повреждённая оценка части рисунка.");
                feedback[i] = new DrawingPartFeedback(source.BindingId, source.Quality, source.Coverage, source.Precision, source.IncompleteContour, source.Message);
            }
            return new DrawingEvaluationStamp(RuleVersion, ReferenceSignature, DrawingHash, ConstructionHash, Quality, feedback);
        }
    }
    [Serializable] public sealed class DrawingFeedbackData
    {
        public string BindingId, Message;
        public float Quality, Coverage, Precision;
        public int IncompleteContour;
        public DrawingFeedbackData() { }
        public DrawingFeedbackData(DrawingPartFeedback value)
        { BindingId = value.BindingId; Message = value.Message; Quality = value.Quality; Coverage = value.Coverage; Precision = value.Precision; IncompleteContour = value.IncompleteContour; }
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
            bool legacy = FormatVersion == 1 || FormatVersion == 2;
            int expectedParts = FormatVersion == 1 ? 7 : 8;
            if (FormatVersion < 1 || (legacy ? BindingVersion != 1 : BindingVersion != 2) ||
                Parts == null || (legacy ? Parts.Length != expectedParts : Parts.Length <= 8 || Parts.Length > DrawingSnapshot.MaximumParts))
                throw new ArgumentException("Неверная версия или состав рисунка.");
            var parts = new DrawingPart[Parts.Length];
            var seen = new bool[expectedParts];
            var bindings = new HashSet<string>(StringComparer.Ordinal);
            int strokeCount = 0, pointCount = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                DrawingPartData part = Parts[i];
                if (part == null || part.Strokes == null) throw new ArgumentException("Повреждённая часть рисунка.");
                string binding;
                if (legacy)
                {
                    int key = (int)part.Key;
                    if (key < 0 || key >= expectedParts || seen[key]) throw new ArgumentException("Неверный или повторяющийся ключ части рисунка.");
                    seen[key] = true; binding = DrawingBindings.For(part.Key);
                }
                else
                {
                    binding = part.BindingId; DrawingBindings.Validate(binding, "Drawing.Parts.BindingId");
                    if (!bindings.Add(binding)) throw new ArgumentException("Повторяющаяся привязка части рисунка.");
                    if (DrawingBindings.TryLegacy(binding, out var legacyKey)) seen[(int)legacyKey] = true;
                }
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
                parts[i] = new DrawingPart(binding, strokes);
            }
            foreach (bool present in seen) if (!present) throw new ArgumentException("Отсутствует обязательная привязка рисунка.");
            return new DrawingSnapshot(parts);
        }
    }
    [Serializable] public sealed class DrawingPartData
    {
        public DrawingPartKey Key;
        public string BindingId;
        public DrawingStrokeData[] Strokes;
        public DrawingPartData() { }
        public DrawingPartData(DrawingPart part)
        {
            Key = part.Key; BindingId = part.BindingId; Strokes = new DrawingStrokeData[part.Strokes.Count];
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
