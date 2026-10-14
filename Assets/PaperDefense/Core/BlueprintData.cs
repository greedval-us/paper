using System;
using System.Collections.Generic;
using System.Globalization;

namespace PaperDefense.Core
{
    [Serializable] public sealed class BlueprintData
    {
        public string Id, Name;
        public ArmorDistributionData Reinforcement;
        public int ContentMetadataVersion;
        public string ChassisId, FirstWeaponId, SecondWeaponId;
        public ModuleSelectionData[] ModuleSelections;
        public int WeaponMountMetadataVersion;
        public WeaponMountSelectionData[] WeaponMounts;
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
        public int FreeformRuleVersion;
        public BlueprintData() { }
        public BlueprintData(TowerBlueprint blueprint)
        { Reinforcement=blueprint.Reinforcement==null?null:new ArmorDistributionData(blueprint.Reinforcement);
            Id = blueprint.Id; Name = blueprint.Name; Chassis = blueprint.Chassis; FirstWeapon = blueprint.FirstWeapon;
            HasSecondWeapon = blueprint.SecondWeapon.HasValue; SecondWeapon = blueprint.SecondWeapon ?? StructureKind.Gun; Modules = blueprint.Modules;
            ContentMetadataVersion = 1; ChassisId = blueprint.ChassisId; FirstWeaponId = blueprint.FirstWeaponId; SecondWeaponId = blueprint.SecondWeaponId;
            ModuleSelections = new ModuleSelectionData[blueprint.ModuleSelections.Count];
            WeaponMountMetadataVersion=1;WeaponMounts=new WeaponMountSelectionData[blueprint.WeaponMounts.Count];
            for(int i=0;i<WeaponMounts.Length;i++){WeaponMounts[i]=new WeaponMountSelectionData(blueprint.WeaponMounts[i]);if(blueprint.WeaponMounts[i].Volley!=null)WeaponMountMetadataVersion=2;}
            for (int i = 0; i < ModuleSelections.Length; i++) ModuleSelections[i] = new ModuleSelectionData(blueprint.ModuleSelections[i]);
            EvaluationMetadataVersion = 1; Evaluation = new DrawingEvaluationData(blueprint.Assessment.ToStamp());
            DrawingMetadataVersion = 1; Origin = blueprint.Origin; Drawing = new DrawingData(blueprint.Drawing);
            FreeformRuleVersion = blueprint.FreeformRuleVersion;
            FootprintMetadataVersion = 1;
            foreach(var mount in blueprint.WeaponMounts)if(mount.Footprint.IsContour)FootprintMetadataVersion=2;
            FirstFootprintAspect = blueprint.FirstFootprint.AspectRatio; SecondFootprintAspect = blueprint.SecondFootprint.AspectRatio; }
        public TowerBlueprint ToBlueprint(int profileVersion = WorkshopProfile.CurrentVersion)
        {
            if (profileVersion > WorkshopProfile.CurrentVersion) throw new NotSupportedException("Newer profile version.");
            if (profileVersion < 1) throw new ArgumentException("Unknown profile version.");
            if(Reinforcement!=null&&(Reinforcement.Version>1||(profileVersion<7&&Reinforcement.Version!=0)))throw new NotSupportedException("Newer reinforcement rules.");
            if(WeaponMountMetadataVersion>2||(profileVersion<7&&WeaponMountMetadataVersion>1))throw new NotSupportedException("Newer weapon mount metadata.");
            if(profileVersion<6&&FootprintMetadataVersion>1)throw new NotSupportedException("Contour footprints require mount metadata.");
            if (Origin == DrawingOrigin.Freeform)
            {
                if (profileVersion < 7) throw new ArgumentException("Freeform drawings require profile version 7.");
                if (FreeformRuleVersion > FreeformTowerDesign.RuleVersion) throw new NotSupportedException("Newer freeform drawing rules.");
                if (FreeformRuleVersion < 1) throw new ArgumentException("Missing freeform drawing rules.");
            }
            if (profileVersion >= 5) return ToContentBlueprint(profileVersion);
            if (!Enum.IsDefined(typeof(ChassisKind), Chassis) || !Enum.IsDefined(typeof(StructureKind), FirstWeapon) ||
                (HasSecondWeapon && !Enum.IsDefined(typeof(StructureKind), SecondWeapon)) ||
                (Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking | TowerModules.Rail)) != 0)
                throw new NotSupportedException(UserText.Message("copy.428a6194712aa364"));
            if (profileVersion == 1)
                return new TowerBlueprint(Id, Name, Chassis, FirstWeapon, HasSecondWeapon ? SecondWeapon : (StructureKind?)null, Modules);
            if (profileVersion == 2 &&
                (!HistoricalWeapon(FirstWeapon) || (HasSecondWeapon && !HistoricalWeapon(SecondWeapon)) ||
                 (Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking)) != 0))
                throw new ArgumentException(UserText.Message("copy.5f58a5e3abdd5032"));
            if (DrawingMetadataVersion > 1) throw new NotSupportedException(UserText.Message("copy.6c744cc68ecf81e5"));
            if (!Enum.IsDefined(typeof(DrawingOrigin), Origin) && (int)Origin > (int)DrawingOrigin.PlayerDrawing)
                throw new NotSupportedException(UserText.Message("copy.05eba950435c3eca"));
            if (DrawingMetadataVersion != 1 || !Enum.IsDefined(typeof(DrawingOrigin), Origin) || Drawing == null)
                throw new ArgumentException(UserText.Message("copy.d4ece270e1268843"));
            if (profileVersion == 2 && Drawing.FormatVersion > 1)
                throw new NotSupportedException(UserText.Message("copy.f1a3783cdcbc8f2e"));
            if (Drawing.FormatVersion > 2 || Drawing.BindingVersion > 1)
                throw new NotSupportedException(UserText.Message("copy.fda72652b2c8354d"));
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
        TowerBlueprint ToContentBlueprint(int profileVersion)
        {
            if (ContentMetadataVersion > 1 || EvaluationMetadataVersion > 1 || DrawingMetadataVersion > 1 || FootprintMetadataVersion > 2)
                throw new NotSupportedException(UserText.Message("copy.6c744cc68ecf81e5"));
            if (!Enum.IsDefined(typeof(DrawingOrigin), Origin) && (int)Origin > (int)DrawingOrigin.PlayerDrawing)
                throw new NotSupportedException(UserText.Message("copy.05eba950435c3eca"));
            if (ContentMetadataVersion != 1 || EvaluationMetadataVersion != 1 || Evaluation == null ||
                DrawingMetadataVersion != 1 || (FootprintMetadataVersion != 1 && FootprintMetadataVersion != 2) || Drawing == null ||
                !Enum.IsDefined(typeof(DrawingOrigin), Origin) || ModuleSelections == null || ModuleSelections.Length > DrawingSnapshot.MaximumParts)
                throw new ArgumentException(UserText.Message("copy.1f79fe0daa674686"));
            if (string.IsNullOrWhiteSpace(ChassisId) || (profileVersion==5&&(string.IsNullOrWhiteSpace(FirstWeaponId) ||
                (HasSecondWeapon && string.IsNullOrWhiteSpace(SecondWeaponId)) ||
                (!HasSecondWeapon && !string.IsNullOrEmpty(SecondWeaponId))))) throw new ArgumentException(UserText.Message("copy.5781d5bb49432494"));
            var modules = new ModuleSelection[ModuleSelections.Length];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < modules.Length; i++)
            {
                ModuleSelectionData source = ModuleSelections[i];
                if (source == null || !seen.Add(source.InstanceId ?? "")) throw new ArgumentException(UserText.Message("copy.6e051f29cafa2af9"));
                modules[i] = new ModuleSelection(source.InstanceId, source.DefinitionId);
            }
            if(profileVersion>=6)
            {
                if(WeaponMountMetadataVersion>2)throw new NotSupportedException("Newer weapon mount metadata.");
                if((WeaponMountMetadataVersion!=1&&WeaponMountMetadataVersion!=2)||WeaponMounts==null||WeaponMounts.Length<1||WeaponMounts.Length>TowerBlueprint.MaximumWeaponMounts)throw new ArgumentException("Missing or invalid weapon mount metadata.");
                var mounts=new List<WeaponMountSelection>();var slots=new HashSet<int>();
                foreach(var mount in WeaponMounts)
                {
                    if(mount==null||!slots.Add(mount.SlotIndex))throw new ArgumentException("Duplicate or empty weapon mount.");
                    var footprint=mount.ToFootprint();
                    if(footprint.IsContour&&FootprintMetadataVersion!=2)throw new ArgumentException("Missing contour footprint metadata.");
                    var volley=mount.ToVolley();
                    if(volley!=null&&WeaponMountMetadataVersion!=2)throw new ArgumentException("Missing volley metadata.");
                    mounts.Add(new WeaponMountSelection(mount.SlotIndex,mount.WeaponId,footprint,volley));
                }
                return new TowerBlueprint(Id,Name,ChassisId,mounts,modules,Drawing.ToSnapshot(),Origin,evaluationStamp:Evaluation.ToStamp(),freeformRuleVersion:FreeformRuleVersion,reinforcement:Reinforcement?.ToDistribution());
            }
            return new TowerBlueprint(Id, Name, ChassisId, FirstWeaponId, HasSecondWeapon ? SecondWeaponId : null, modules,
                Drawing.ToSnapshot(), Origin, MortarFootprint.CreateStored(FirstFootprintAspect), MortarFootprint.CreateStored(SecondFootprintAspect),
                evaluationStamp: Evaluation.ToStamp());
        }
        static bool HistoricalWeapon(StructureKind kind) => kind == StructureKind.Gun || kind == StructureKind.Piercer || kind == StructureKind.Mortar;
    }
    [Serializable] public sealed class WeaponMountSelectionData
    {
        public int SlotIndex;
        public string WeaponId;
        public float FootprintAspect=1;
        public int FootprintContourVersion;
        public DrawingPointData[] FootprintPoints;
        public int VolleyVersion;
        public DrawingStrokeData[] VolleyGuides;
        public WeaponMountSelectionData() { }
        public WeaponMountSelectionData(WeaponMountSelection selection)
        {
            SlotIndex=selection.SlotIndex;WeaponId=selection.WeaponId;FootprintAspect=selection.Footprint.AspectRatio;
            if(selection.Volley!=null)
            {
                VolleyVersion=1;VolleyGuides=new DrawingStrokeData[selection.Volley.Guides.Count];
                for(int i=0;i<VolleyGuides.Length;i++)
                {
                    var guide=selection.Volley.Guides[i];var points=new DrawingPointData[guide.Points.Count];
                    for(int j=0;j<points.Length;j++)points[j]=new DrawingPointData{X=guide.Points[j].X,Y=guide.Points[j].Y};
                    VolleyGuides[i]=new DrawingStrokeData{Points=points};
                }
            }
            if(selection.Footprint.IsContour)
            {
                FootprintContourVersion=1;FootprintPoints=new DrawingPointData[selection.Footprint.Authored.Count];
                for(int i=0;i<FootprintPoints.Length;i++)FootprintPoints[i]=new DrawingPointData{X=selection.Footprint.Authored[i].X,Y=selection.Footprint.Authored[i].Y};
            }
        }
        public FanVolley ToVolley()
        {
            if(VolleyVersion>1)throw new NotSupportedException("Newer volley rules.");
            if(VolleyVersion==0&&(VolleyGuides==null||VolleyGuides.Length==0))return null;
            if(VolleyVersion!=1||VolleyGuides==null||VolleyGuides.Length<1||VolleyGuides.Length>FanVolley.MaximumGuides)throw new ArgumentException("Invalid volley guides.");
            var guides=new List<FanGuide>();
            foreach(var guide in VolleyGuides)
            {
                if(guide?.Points==null||guide.Points.Length>FanGuide.MaximumPoints)throw new ArgumentException("Invalid volley guide.");
                var points=new List<Point2>();foreach(var point in guide.Points){if(point==null)throw new ArgumentException("Missing volley point.");points.Add(new Point2(point.X,point.Y));}
                guides.Add(new FanGuide(points));
            }
            return new FanVolley(guides);
        }
        public MortarFootprint ToFootprint()
        {
            if(FootprintContourVersion>1)throw new NotSupportedException("Newer footprint contour rules.");
            if(FootprintContourVersion==0&&(FootprintPoints==null||FootprintPoints.Length==0))return MortarFootprint.CreateStored(FootprintAspect);
            if(FootprintContourVersion!=1||FootprintPoints==null||FootprintPoints.Length<3||FootprintPoints.Length>FootprintContour.MaximumPoints)throw new ArgumentException("Invalid footprint contour data.");
            var points=new Point2[FootprintPoints.Length];
            for(int i=0;i<points.Length;i++){var p=FootprintPoints[i]??throw new ArgumentException("Missing footprint point.");points[i]=new Point2(p.X,p.Y);}
            return MortarFootprint.FromContour(points,MortarFootprint.StructuralMaximumAspectRatio);
        }
        internal WeaponMountSelectionData CreateCopy()
        {
            var copy=new WeaponMountSelectionData{SlotIndex=SlotIndex,WeaponId=WeaponId,FootprintAspect=FootprintAspect,FootprintContourVersion=FootprintContourVersion,VolleyVersion=VolleyVersion};
            if(VolleyGuides!=null){copy.VolleyGuides=new DrawingStrokeData[VolleyGuides.Length];for(int i=0;i<VolleyGuides.Length;i++){var guide=VolleyGuides[i];if(guide==null)continue;copy.VolleyGuides[i]=new DrawingStrokeData();if(guide.Points!=null){copy.VolleyGuides[i].Points=new DrawingPointData[guide.Points.Length];for(int j=0;j<guide.Points.Length;j++)if(guide.Points[j]!=null)copy.VolleyGuides[i].Points[j]=new DrawingPointData{X=guide.Points[j].X,Y=guide.Points[j].Y};}}}
            if(FootprintPoints!=null){copy.FootprintPoints=new DrawingPointData[FootprintPoints.Length];for(int i=0;i<FootprintPoints.Length;i++)if(FootprintPoints[i]!=null)copy.FootprintPoints[i]=new DrawingPointData{X=FootprintPoints[i].X,Y=FootprintPoints[i].Y};}
            return copy;
        }
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
                throw new ArgumentException(UserText.Message("copy.b1a2f2e3c1560a45"));
            var feedback = new DrawingPartFeedback[Feedback.Length];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < feedback.Length; i++)
            {
                var source = Feedback[i];
                if (source == null || !seen.Add(source.BindingId ?? "")) throw new ArgumentException(UserText.Message("copy.154530d633298c9b"));
                string message = RestoreMessage(source, out string archivedMessage);
                feedback[i] = new DrawingPartFeedback(source.BindingId, source.Quality, source.Coverage, source.Precision, source.IncompleteContour, message, archivedMessage);
            }
            return new DrawingEvaluationStamp(RuleVersion, ReferenceSignature, DrawingHash, ConstructionHash, Quality, feedback);
        }

        static string RestoreMessage(DrawingFeedbackData source, out string archivedMessage)
        {
            string original = source.Message ?? "";
            archivedMessage = source.ArchivedMessage ?? "";
            if (original.StartsWith(UserText.Prefix, StringComparison.Ordinal) &&
                original.IndexOf(UserText.End, UserText.Prefix.Length) == original.Length - 1) return original;
            if (archivedMessage.Length == 0) archivedMessage = original;

            // These suffixes identify historical serialized messages, never current display copy.
            const string low = ": \u043c\u043d\u043e\u0433\u043e \u043b\u0438\u043d\u0438\u0439 \u0432 \u0441\u0442\u043e\u0440\u043e\u043d\u0435 \u043e\u0442 \u043e\u0431\u0440\u0430\u0437\u0446\u0430.";
            const string readable = ": \u043a\u043e\u043d\u0442\u0443\u0440\u044b \u0447\u0438\u0442\u0430\u044e\u0442\u0441\u044f \u0443\u0432\u0435\u0440\u0435\u043d\u043d\u043e.";
            const string contour = ": \u043a\u043e\u043d\u0442\u0443\u0440 ";
            const string unfinished = " \u0435\u0449\u0451 \u043d\u0435 \u0437\u0430\u0432\u0435\u0440\u0448\u0451\u043d.";
            if (original.EndsWith(low, StringComparison.Ordinal) && original.Length > low.Length)
                return UserText.Message("copy.5c24ada686c606b4", original.Substring(0, original.Length - low.Length));
            if (original.EndsWith(readable, StringComparison.Ordinal) && original.Length > readable.Length)
                return UserText.Message("copy.e19aba16f8c4e47b", original.Substring(0, original.Length - readable.Length));
            int start = original.LastIndexOf(contour, StringComparison.Ordinal);
            int numberStart = start + contour.Length;
            if (start > 0 && original.EndsWith(unfinished, StringComparison.Ordinal) &&
                original.Length > numberStart + unfinished.Length &&
                int.TryParse(original.Substring(numberStart, original.Length - numberStart - unfinished.Length),
                    NumberStyles.None, CultureInfo.InvariantCulture, out int contourNumber) && contourNumber > 0)
                return UserText.Message("copy.3cc592b5b09bbd9f", original.Substring(0, start), contourNumber);
            return UserText.Message("drawing.feedback.archived", ArchivedPartName(source.BindingId), source.Quality, source.Coverage, source.Precision);
        }

        static string ArchivedPartName(string bindingId)
        {
            switch (bindingId)
            {
                case DrawingBindings.Base: return UserText.Message("copy.0369683b41e6de45");
                case DrawingBindings.Mount0: return UserText.Message("copy.9e98735021c6b210");
                case DrawingBindings.Mount1: return UserText.Message("copy.375f6faa7a651fae");
                case DrawingBindings.Mount2: return UserText.Message("copy.3265cc01bf3a6da1");
                case "module.armor": return UserText.Message("copy.e41e849ff6126cff");
                case "module.feed": return UserText.Message("copy.d4a738612fc5e701");
                case "module.tracking": return UserText.Message("copy.41e4c92b11a67cdf");
                case "module.rail": return UserText.Message("copy.215d555ed53c9640");
                case DrawingBindings.Decoration: return UserText.Message("copy.da593f54cbd9f76c");
                default: return UserText.Message("drawing.feedback.part");
            }
        }
    }
    [Serializable] public sealed class DrawingFeedbackData
    {
        public string BindingId, Message, ArchivedMessage;
        public float Quality, Coverage, Precision;
        public int IncompleteContour;
        public DrawingFeedbackData() { }
        public DrawingFeedbackData(DrawingPartFeedback value)
        { BindingId = value.BindingId; Message = value.Message; ArchivedMessage = value.ArchivedMessage; Quality = value.Quality; Coverage = value.Coverage; Precision = value.Precision; IncompleteContour = value.IncompleteContour; }
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
                throw new NotSupportedException(UserText.Message("copy.683525593119661e"));
            bool legacy = FormatVersion == 1 || FormatVersion == 2;
            int expectedParts = FormatVersion == 1 ? 7 : 8;
            if (FormatVersion < 1 || (legacy ? BindingVersion != 1 : BindingVersion != 2) ||
                Parts == null || (legacy ? Parts.Length != expectedParts : Parts.Length <= 8 || Parts.Length > DrawingSnapshot.MaximumParts))
                throw new ArgumentException(UserText.Message("copy.af20eff34a1f5fb0"));
            var parts = new DrawingPart[Parts.Length];
            var seen = new bool[expectedParts];
            var bindings = new HashSet<string>(StringComparer.Ordinal);
            int strokeCount = 0, pointCount = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                DrawingPartData part = Parts[i];
                if (part == null || part.Strokes == null) throw new ArgumentException(UserText.Message("copy.7b936180ac3100a3"));
                string binding;
                if (legacy)
                {
                    int key = (int)part.Key;
                    if (key < 0 || key >= expectedParts || seen[key]) throw new ArgumentException(UserText.Message("copy.3bee70fc89e49680"));
                    seen[key] = true; binding = DrawingBindings.For(part.Key);
                }
                else
                {
                    binding = part.BindingId; DrawingBindings.Validate(binding, "Drawing.Parts.BindingId");
                    if (!bindings.Add(binding)) throw new ArgumentException(UserText.Message("copy.8a27c32f741e57d1"));
                    if (DrawingBindings.TryLegacy(binding, out var legacyKey)) seen[(int)legacyKey] = true;
                }
                strokeCount += part.Strokes.Length;
                if (strokeCount > DrawingSnapshot.MaxStrokes) throw new ArgumentException(UserText.Message("copy.aa637cbed76c6320"));
                var strokes = new DrawingStroke[part.Strokes.Length];
                for (int s = 0; s < strokes.Length; s++)
                {
                    DrawingStrokeData source = part.Strokes[s];
                    if (source == null || source.Points == null) throw new ArgumentException(UserText.Message("copy.4f93089e27dbbb46"));
                    pointCount += source.Points.Length;
                    if (pointCount > DrawingSnapshot.MaxPoints) throw new ArgumentException(UserText.Message("copy.9f5f5f21b84b9bff"));
                    var points = new DrawingPoint[source.Points.Length];
                    for (int p = 0; p < points.Length; p++)
                    {
                        DrawingPointData point = source.Points[p];
                        if (point == null) throw new ArgumentException(UserText.Message("copy.efd01ce3f7542ca2"));
                        points[p] = new DrawingPoint(point.X, point.Y);
                    }
                    strokes[s] = new DrawingStroke(points);
                }
                parts[i] = new DrawingPart(binding, strokes);
            }
            foreach (bool present in seen) if (!present) throw new ArgumentException(UserText.Message("copy.28baba461bb688b4"));
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
