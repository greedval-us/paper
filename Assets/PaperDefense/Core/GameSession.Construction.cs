using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        public BuildQuote QuoteEntry(IReadOnlyList<Cell> cells, ResolvedEntry entry, MaterialTier material, InkColor color)
        {
            if (entry == null || entry.Kind == AlbumEntryKind.Legacy)
                return RejectedEntryQuote(UserText.Message("copy.48635af5e97f1a11"));
            if (!Enum.IsDefined(typeof(MaterialTier), material) || entry.Material != material)
                return RejectedEntryQuote(UserText.Message("assembly.material.unsupported"));
            if (!Enum.IsDefined(typeof(InkColor), color))
                return RejectedEntryQuote(UserText.Message("copy.63ae8dc3a0c35f89"));
            if (color != InkColor.None && !Mechanics.ColorCartridges)
                return RejectedEntryQuote(UserText.Message("copy.0a21e662d398deaa"));
            if (color != InkColor.None && entry.Kind != AlbumEntryKind.Tower)
                return RejectedEntryQuote(UserText.Message("copy.a8ce8499831aa26c"));
            var quote = QuoteBuild(cells, StructureState.EntryKind(entry), material, entry.Cost, compositeEntry: true);
            if (!quote.Valid) return quote;
            if (entry.Support?.Kind == AssemblyStructureFamily.Generator)
            {
                int existing = 0;
                foreach (var structure in structures)
                    if (structure.Health > 0 && structure.ResolvedEntry?.Support?.Kind == AssemblyStructureFamily.Generator) existing++;
                if (existing + quote.Cells.Count > entry.Support.MaximumInstances)
                { quote.Valid = false; quote.Reason = UserText.Message("td.generator.limit"); return quote; }
            }
            if (color != InkColor.None && (long)EntryColorCost(entry) * quote.Cells.Count > ColoredInk(color))
            { quote.Valid = false; quote.Reason = UserText.Message("copy.cc5678934a13d506"); }
            return quote;
        }

        public bool TryBuildEntry(IReadOnlyList<Cell> cells, ResolvedEntry entry, MaterialTier material, InkColor color, out string reason, string sourceAlbumEntryId = null)
        {
            var quote = QuoteEntry(cells, entry, material, color); reason = quote.Reason;
            if (!quote.Valid) return false;
            // Prepare every entity before publishing the resource payment or changing occupancy.
            var prepared = new List<StructureState>(quote.Cells.Count);
            float health = TowerCatalog.FiniteStat(entry.Health * healthMultiplier, "health");
            foreach (var cell in quote.Cells)
                prepared.Add(new StructureState(entry)
                {
                    Id = nextStructureId + prepared.Count, Cell = cell, PaidInk = entry.Cost, Color = color, SourceAlbumEntryId = sourceAlbumEntryId,
                    Health = health, MaxHealth = health, IsComplete = entry.BuildSeconds <= 0,
                    Aim = new Point2(cell.X + 1.5f, cell.Y + .5f), Priority = TargetPriority.Nearest
                });
            Ink -= quote.Cost;
            if (color != InkColor.None) coloredInk[(int)color] -= EntryColorCost(entry) * prepared.Count;
            nextStructureId += prepared.Count;
            foreach (var structure in prepared)
            {
                structures.Add(structure); grid[structure.Cell.Y * rules.Width + structure.Cell.X] = structure;
                MarkStructureChanged(structure, structure.IsComplete);
            }
            string metricsId = entry.Kind == AlbumEntryKind.Tower ? LegacyContentIds.Weapon(prepared[0].Kind) : prepared[0].ContentId;
            Statistics.RecordBuild(quote.Cost, prepared.Count, metricsId, material);
            return true;
        }

        static BuildQuote RejectedEntryQuote(string reason)
            => new BuildQuote { Cells = Array.Empty<Cell>(), Reason = reason };

        int EntryColorCost(ResolvedEntry entry)
        {
            int pencilCost = entry.Material == MaterialTier.Pencil ? entry.Cost :
                AssemblyResolver.Resolve(entry.Snapshot, rules, MaterialTier.Pencil).Value.Cost;
            return (int)Math.Max(rules.Construction.ColorMinimum,
                ((long)pencilCost + rules.Construction.ColorCostDivisor - 1) / rules.Construction.ColorCostDivisor);
        }

        bool TryUpgradeEntry(StructureState structure, out string reason)
        {
            var material = (MaterialTier)((int)structure.Tier + 1);
            var resolution = AssemblyResolver.Resolve(structure.ResolvedEntry.Snapshot, rules, material);
            reason = UserText.Message("assembly.material.unsupported");
            if (!resolution.Valid) return false;
            var entry = resolution.Value;
            int difference = Math.Max(0, entry.Cost - structure.ResolvedEntry.Cost);
            if (difference > Ink) { reason = UserText.Message("copy.d952db5ec0c442f9", difference); return false; }
            float maximum = TowerCatalog.FiniteStat(entry.Health * healthMultiplier, "health");
            float fraction = Clamp(structure.Health / Math.Max(1, structure.MaxHealth), 0, 1);
            Ink -= difference; structure.PaidInk += difference; Statistics.RecordUpgrade(difference);
            structure.ResolvedEntry = entry; structure.Tier = material;
            structure.MaxHealth = maximum; structure.Health = maximum * fraction;
            structure.BuildRemaining = entry.BuildSeconds; structure.IsComplete = entry.BuildSeconds <= 0;
            structure.WaitingForClearance = false;
            // Material does not replace magazines or their normalized progress.
            MarkStructureChanged(structure, true);
            reason = ""; return true;
        }

        public int ColorCost(TowerBlueprint blueprint,MaterialTier material)
            => blueprint==null?(int)Math.Max(rules.Construction.ColorMinimum,((long)BuildCost(StructureKind.Gun,MaterialTier.Pencil)+rules.Construction.ColorCostDivisor-1)/rules.Construction.ColorCostDivisor):TowerCatalog.ColorCost(blueprint,rules);

        public BuildQuote QuoteBlueprint(IReadOnlyList<Cell> cells,TowerBlueprint blueprint,MaterialTier material,InkColor color)
        {
            if (!TowerCatalog.Validate(blueprint,rules,out string invalid))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason=invalid};
            if (!Enum.IsDefined(typeof(InkColor),color))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason=UserText.Message("copy.63ae8dc3a0c35f89")};
            if (!Enum.IsDefined(typeof(MaterialTier),material))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason=UserText.Message("copy.cef60cd86a8db8c7")};
            if (color != InkColor.None && !Mechanics.ColorCartridges)
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason=UserText.Message("copy.0a21e662d398deaa")};
            var quote=QuoteBuild(cells,blueprint.FirstWeapon,material,TowerCatalog.Cost(blueprint,rules,material));
            // Occupancy and resource failures stay atomic, using the assembly's complete price.
            if (!quote.Valid) return quote;
            long cost=(long)TowerCatalog.Cost(blueprint,rules,material)*quote.Cells.Count;
            quote.Cost=(int)Math.Min(int.MaxValue,cost);
            if (cost>Ink){quote.Valid=false;quote.Reason=UserText.Message("copy.d952db5ec0c442f9", cost);return quote;}
            if (color!=InkColor.None && (long)ColorCost(blueprint,material)*quote.Cells.Count>ColoredInk(color))
            {quote.Valid=false;quote.Reason=UserText.Message("copy.cc5678934a13d506");}
            return quote;
        }

        public bool TryBuildBlueprint(IReadOnlyList<Cell> cells,TowerBlueprint blueprint,MaterialTier material,InkColor color,out string reason)
        {
            var quote=QuoteBlueprint(cells,blueprint,material,color);reason=quote.Reason;
            if(!quote.Valid)return false;
            Ink-=quote.Cost;
            if(color!=InkColor.None)coloredInk[(int)color]-=ColorCost(blueprint,material)*quote.Cells.Count;
            int cost=TowerCatalog.Cost(blueprint,rules,material);
            blueprint=blueprint.WithContent(rules.Content);
            var assembly=TowerCatalog.Resolve(blueprint,rules.Content);
            foreach(var cell in quote.Cells)
            {
                AddStructure(cell,blueprint.FirstWeapon,material,cost,false,assembly.First.Numeric,assembly.First.Id);
                var structure=StructureAt(cell);structure.Blueprint=blueprint;structure.Assembly=assembly;structure.Color=color;
                structure.MaxHealth=TowerCatalog.Health(blueprint,rules,material)*healthMultiplier;
                structure.Health=structure.MaxHealth;
                structure.BuildRemaining=TowerCatalog.BuildSeconds(blueprint,rules,material);
                structure.IsComplete=structure.BuildRemaining<=0;
                MarkStructureChanged(structure,structure.IsComplete);
            }
            Statistics.RecordBuild(quote.Cost,quote.Cells.Count,blueprint.FirstWeaponId,material);
            return true;
        }

        public bool TryPaint(Cell cell,InkColor color,out string reason)
        {
            reason="";
            if(Phase==RunPhase.Victory||Phase==RunPhase.Defeat){reason=UserText.Message("copy.850e91bbbd5c94d8");return false;}
            if(!Enum.IsDefined(typeof(InkColor),color)){reason=UserText.Message("copy.63ae8dc3a0c35f89");return false;}
            if(color!=InkColor.None&&!Mechanics.ColorCartridges){reason=UserText.Message("copy.0a21e662d398deaa");return false;}
            var tower=StructureAt(cell);
            if(tower==null||ConstructionToolRules.IsTool(tower.Kind)||tower.ResolvedEntry?.Kind==AlbumEntryKind.Structure)
            {reason=UserText.Message("copy.a8ce8499831aa26c");return false;}
            if(tower.Color==color)return true;
            int cost=tower.ResolvedEntry==null?ColorCost(tower.Blueprint,tower.Tier):EntryColorCost(tower.ResolvedEntry);
            if(color!=InkColor.None&&ColoredInk(color)<cost){reason=UserText.Message("copy.e9d0f96d16c02def", cost);return false;}
            if(color!=InkColor.None)coloredInk[(int)color]-=cost;
            tower.Color=color;StructureVisualVersion++;
            return true;
        }

        private int StructureBuildCost(StructureState structure,MaterialTier material)
            => structure.Blueprint==null?BuildCost(structure.Kind,material):TowerCatalog.Cost(structure.Blueprint,rules,material);
        private float StructureHealth(StructureState structure,MaterialTier material)
            => (structure.Blueprint==null?rules.Structure(structure.Kind).MaxHealth*rules.Material(material).HealthMultiplier:
                TowerCatalog.Health(structure.Blueprint,rules,material))*healthMultiplier;
        private float StructureBuildSeconds(StructureState structure,MaterialTier material)
            => structure.Blueprint==null?rules.Structure(structure.Kind).BuildSeconds*rules.Material(material).BuildTimeMultiplier:
                TowerCatalog.BuildSeconds(structure.Blueprint,rules,material);
    }
}
