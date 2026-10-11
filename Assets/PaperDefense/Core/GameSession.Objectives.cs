using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed class InkWellState
    {
        public int Id {get;internal set;}
        public Cell Cell {get;internal set;}
        public InkColor Color {get;internal set;}
        public float Health {get;internal set;}
        public float MaxHealth {get;internal set;}
        public bool Active {get;internal set;}
        public bool Threatened {get;internal set;}
        internal int EligibleWave;
    }

    public sealed partial class GameSession
    {
        readonly List<InkWellState> wells=new List<InkWellState>(3);
        readonly int[] coloredInk=new int[4];
        WeightedFlowField[] wellFlows;
        public IReadOnlyList<InkWellState> Wells=>wells;
        public int GoldEarned {get;private set;}
        public int ColoredInk(InkColor color)=>Enum.IsDefined(typeof(InkColor),color)?coloredInk[(int)color]:0;

        void InitializeObjectives()
        {
            if(!Balance.EnableInkWells)return;
            var cells=new[]{new Cell(Balance.Width/5,Balance.Height/4),new Cell(Balance.Width*4/5,Balance.Height/4),
                new Cell(Balance.Width/2,Balance.Height*5/6)};
            wellFlows=new WeightedFlowField[cells.Length];
            for(int i=0;i<cells.Length;i++)
            {
                wells.Add(new InkWellState{Id=i,Cell=cells[i],Color=(InkColor)(i+1),Health=Balance.WellHealth,
                    MaxHealth=Balance.WellHealth});
                wellFlows[i]=new WeightedFlowField(Balance.Width,Balance.Height);
            }
        }

        public bool TryActivateWell(int index,out string reason)
        {
            reason="";
            if(Phase==RunPhase.Victory||Phase==RunPhase.Defeat){reason="Эта страница уже закончена.";return false;}
            if(index<0||index>=wells.Count){reason="Чернильница не найдена.";return false;}
            var well=wells[index];
            if(well.Active){reason="Эта точка уже защищается.";return false;}
            int price=Math.Max(0,Balance.WellActivationCost);
            if(Ink<price){reason="Не хватает штрихов: нужно "+price+".";return false;}
            Ink-=price;well.Active=true;well.Health=Math.Max(1,well.MaxHealth);
            // An assault already in progress cannot be counted as a fully protected wave.
            well.EligibleWave=WaveIndex+1;well.Threatened=false;flowDirty=true;StructureVisualVersion++;
            return true;
        }

        internal void DamageObjective(int index,float damage)
        {
            if(damage<=0||float.IsNaN(damage)||float.IsInfinity(damage))return;
            if(index < -1)return;
            if(index==-1){BaseHealth=Math.Max(0,BaseHealth-damage);return;}
            if(index>=wells.Count||!wells[index].Active)return;
            var well=wells[index];well.Health=Math.Max(0,well.Health-damage);
            if(well.Health>0)return;
            well.Active=false;well.Threatened=false;well.EligibleWave=int.MaxValue;
            flowDirty=true;StructureVisualVersion++;
            foreach(var enemy in enemies)
                if(enemy.ObjectiveIndex==index){enemy.ObjectiveIndex=-1;enemy.HasDestination=false;}
        }

        private bool ReservedObjective(Cell cell)
        {
            foreach(var well in wells)if(Math.Abs(cell.X-well.Cell.X)<=1&&Math.Abs(cell.Y-well.Cell.Y)<=1)return true;
            return false;
        }
        private Point2 TargetPoint(EnemyState enemy)
            => Center(enemy.ObjectiveIndex>=0&&enemy.ObjectiveIndex<wells.Count?wells[enemy.ObjectiveIndex].Cell:BaseCell);
        private Cell TargetObjectiveCell(int index)=>index>=0&&index<wells.Count?wells[index].Cell:BaseCell;
        private WeightedFlowField EnemyFlow(EnemyState enemy)
            => enemy.ObjectiveIndex>=0&&enemy.ObjectiveIndex<wells.Count&&wells[enemy.ObjectiveIndex].Active?wellFlows[enemy.ObjectiveIndex]:flow;

        private int ChooseObjective()
        {
            if(wells.Count==0||random.NextDouble()>=.3)return -1;
            int active=0;foreach(var well in wells)if(well.Active)active++;
            if(active==0)return -1;
            int selection=random.Next(active);
            foreach(var well in wells)if(well.Active&&selection--==0){well.Threatened=true;return well.Id;}
            return -1;
        }
        private void RebuildWellFlows(EnemyDefinition profile)
        {
            foreach(var well in wells)
                if(well.Active)wellFlows[well.Id].Rebuild(well.Cell,grid,profile.Speed,
                    Math.Max(.01f,profile.Damage)/Math.Max(.05f,profile.AttackInterval));
        }
        private void AwardWaveResources()
        {
            foreach(var well in wells)
            {
                if(well.Active&&well.EligibleWave<=WaveIndex)
                    coloredInk[(int)well.Color]=(int)Math.Min(int.MaxValue,(long)coloredInk[(int)well.Color]+Math.Max(0,Balance.WellInkPerWave));
                well.Threatened=false;
            }
            GoldEarned=(int)Math.Min(int.MaxValue,(long)GoldEarned+Math.Max(0,Balance.WaveGold));
            if(WaveIndex>=Balance.Waves.Count)
                GoldEarned=(int)Math.Min(int.MaxValue,(long)GoldEarned+Math.Max(0,Balance.VictoryGold));
        }
    }
}
