using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        public int ColorCost(TowerBlueprint blueprint,MaterialTier material)
            => blueprint==null?Math.Max(2,(BuildCost(StructureKind.Gun,MaterialTier.Pencil)+7)/8):TowerCatalog.ColorCost(blueprint,Balance);

        public BuildQuote QuoteBlueprint(IReadOnlyList<Cell> cells,TowerBlueprint blueprint,MaterialTier material,InkColor color)
        {
            if (!TowerCatalog.Validate(blueprint,Balance,out string invalid))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason=invalid};
            if (!Enum.IsDefined(typeof(InkColor),color))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason="Неизвестный цвет."};
            if (!Enum.IsDefined(typeof(MaterialTier),material))
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason="Неизвестный инструмент."};
            if (color != InkColor.None && !Mechanics.ColorCartridges)
                return new BuildQuote{Cells=Array.Empty<Cell>(),Reason="Цветные картриджи недоступны на этом листе."};
            var quote=QuoteBuild(cells,blueprint.FirstWeapon,material,TowerCatalog.Cost(blueprint,Balance,material));
            // Occupancy and resource failures stay atomic, using the assembly's complete price.
            if (!quote.Valid) return quote;
            long cost=(long)TowerCatalog.Cost(blueprint,Balance,material)*quote.Cells.Count;
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
            int cost=TowerCatalog.Cost(blueprint,Balance,material);
            foreach(var cell in quote.Cells)
            {
                AddStructure(cell,blueprint.FirstWeapon,material,cost,false);
                var structure=StructureAt(cell);structure.Blueprint=blueprint;structure.Color=color;
                structure.MaxHealth=TowerCatalog.Health(blueprint,Balance,material)*healthMultiplier;
                structure.Health=structure.MaxHealth;
                structure.BuildRemaining=TowerCatalog.BuildSeconds(blueprint,Balance,material);
                structure.IsComplete=structure.BuildRemaining<=0;
                MarkStructureChanged(structure,structure.IsComplete);
            }
            Statistics.RecordBuild(quote.Cost,quote.Cells.Count,blueprint.FirstWeapon,material);
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
            => structure.Blueprint==null?BuildCost(structure.Kind,material):TowerCatalog.Cost(structure.Blueprint,Balance,material);
        private float StructureHealth(StructureState structure,MaterialTier material)
            => (structure.Blueprint==null?Balance.Structure(structure.Kind).MaxHealth*Balance.Material(material).HealthMultiplier:
                TowerCatalog.Health(structure.Blueprint,Balance,material))*healthMultiplier;
        private float StructureBuildSeconds(StructureState structure,MaterialTier material)
            => structure.Blueprint==null?Balance.Structure(structure.Kind).BuildSeconds*Balance.Material(material).BuildTimeMultiplier:
                TowerCatalog.BuildSeconds(structure.Blueprint,Balance,material);
    }
}
