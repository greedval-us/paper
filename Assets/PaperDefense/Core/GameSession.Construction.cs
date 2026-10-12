using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        public int ColorCost(TowerBlueprint blueprint,MaterialTier material)
            => blueprint==null?(int)Math.Max(rules.Construction.ColorMinimum,((long)BuildCost(StructureKind.Gun,MaterialTier.Pencil)+rules.Construction.ColorCostDivisor-1)/rules.Construction.ColorCostDivisor):TowerCatalog.ColorCost(blueprint,rules);

        public BuildQuote QuoteBlueprint(IReadOnlyList<Cell> cells,TowerBlueprint blueprint,MaterialTier material,InkColor color)
        {
            if (!TowerCatalog.Validate(blueprint,rules,out string invalid))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason=invalid};
            if (!Enum.IsDefined(typeof(InkColor),color))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason="Неизвестный цвет."};
            if (!Enum.IsDefined(typeof(MaterialTier),material))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason="Неизвестный инструмент."};
            if (color != InkColor.None && !Mechanics.ColorCartridges)
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason="Цветные картриджи недоступны на этом листе."};
            var quote=QuoteBuild(cells,blueprint.FirstWeapon,material,TowerCatalog.Cost(blueprint,rules,material));
            // Occupancy and resource failures stay atomic, using the assembly's complete price.
            if (!quote.Valid) return quote;
            long cost=(long)TowerCatalog.Cost(blueprint,rules,material)*quote.Cells.Count;
            quote.Cost=(int)Math.Min(int.MaxValue,cost);
            if (cost>Ink){quote.Valid=false;quote.Reason="Не хватает штрихов: нужно "+cost+".";return quote;}
            if (color!=InkColor.None && (long)ColorCost(blueprint,material)*quote.Cells.Count>ColoredInk(color))
            {quote.Valid=false;quote.Reason="Не хватает цветных чернил для всей сборки.";}
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
            if(Phase==RunPhase.Victory||Phase==RunPhase.Defeat){reason="Эта страница уже закончена.";return false;}
            if(!Enum.IsDefined(typeof(InkColor),color)){reason="Неизвестный цвет.";return false;}
            if(color!=InkColor.None&&!Mechanics.ColorCartridges){reason="Цветные картриджи недоступны на этом листе.";return false;}
            var tower=StructureAt(cell);
            if(tower==null||tower.Kind==StructureKind.Wall||tower.Kind==StructureKind.Spikes)
            {reason="Выберите башню для картриджа.";return false;}
            if(tower.Color==color)return true;
            int cost=ColorCost(tower.Blueprint,tower.Tier);
            if(color!=InkColor.None&&ColoredInk(color)<cost){reason="Не хватает цветных чернил: нужно "+cost+".";return false;}
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
