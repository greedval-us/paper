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
            if(!Mechanics.InkWells)return;
            var cells=new Cell[rules.WellPlacements.Count];
            for(int i=0;i<cells.Length;i++) cells[i]=rules.WellPlacements[i].Resolve(rules.Width,rules.Height);
            wellFlows=new WeightedFlowField[cells.Length];
            for(int i=0;i<cells.Length;i++)
            {
                wells.Add(new InkWellState{Id=i,Cell=cells[i],Color=rules.WellPlacements[i].Color,Health=rules.WellHealth,
                    MaxHealth=rules.WellHealth});
                wellFlows[i]=new WeightedFlowField(rules.Width,rules.Height);
            }
        }

        public bool TryActivateWell(int index,out string reason)
        {
            reason="";
            if(Phase==RunPhase.Victory||Phase==RunPhase.Defeat){reason=UserText.Message("copy.850e91bbbd5c94d8");return false;}
            if(index<0||index>=wells.Count){reason=UserText.Message("copy.e59e78c138c420a2");return false;}
            var well=wells[index];
            if(well.Active){reason=UserText.Message("copy.b0b658758b517b85");return false;}
            int price=Math.Max(0,rules.WellActivationCost);
            if(Ink<price){reason=UserText.Message("copy.d952db5ec0c442f9", price);return false;}
            Ink-=price;well.Active=true;well.Health=Math.Max(1,well.MaxHealth);
            // An assault already in progress cannot be counted as a fully protected wave.
            well.EligibleWave=WaveIndex+1;well.Threatened=false;flowDirty=true;StructureVisualVersion++;
            EventBuffer.Publish(TickIndex,SimulationEventKind.InkWellActivated,Center(well.Cell),Center(well.Cell),default,
                entityId:well.Id,simulationSeconds:Elapsed);
            return true;
        }

        internal void DamageObjective(int index,float damage)
        {
            if(damage<=0||float.IsNaN(damage)||float.IsInfinity(damage))return;
            if(index < -1)return;
            if(index==-1){BaseHealth=Math.Max(0,BaseHealth-damage);
                EventBuffer.Publish(TickIndex,SimulationEventKind.BaseHit,Center(BaseCell),Center(BaseCell),default,simulationSeconds:Elapsed);return;}
            if(index>=wells.Count||!wells[index].Active)return;
            var well=wells[index];well.Health=Math.Max(0,well.Health-damage);
            EventBuffer.Publish(TickIndex,SimulationEventKind.InkWellHit,Center(well.Cell),Center(well.Cell),default,entityId:well.Id,simulationSeconds:Elapsed);
            if(well.Health>0)return;
            well.Active=false;well.Threatened=false;well.EligibleWave=int.MaxValue;
            flowDirty=true;StructureVisualVersion++;
            foreach(var enemy in enemies)
                if(enemy.ObjectiveIndex==index){enemy.ObjectiveIndex=-1;enemy.HasDestination=false;}
        }

        private bool ReservedObjective(Cell cell)
        {
            foreach(var well in wells)
            {
                int radius=rules.WellPlacements[well.Id].ReservedRadius;
                if(Math.Abs(cell.X-well.Cell.X)<=radius&&Math.Abs(cell.Y-well.Cell.Y)<=radius)return true;
            }
            return false;
        }
        private Point2 TargetPoint(EnemyState enemy)
            => Center(enemy.ObjectiveIndex>=0&&enemy.ObjectiveIndex<wells.Count?wells[enemy.ObjectiveIndex].Cell:BaseCell);
        private Cell TargetObjectiveCell(int index)=>index>=0&&index<wells.Count?wells[index].Cell:BaseCell;
        private WeightedFlowField EnemyFlow(EnemyState enemy)
            => enemy.ObjectiveIndex>=0&&enemy.ObjectiveIndex<wells.Count&&wells[enemy.ObjectiveIndex].Active?wellFlows[enemy.ObjectiveIndex]:flow;

        private int ChooseObjective()
        {
            if(wells.Count==0||random.NextDouble()>=Clamp(rules.Progression.WellThreatFraction,0,1))return -1;
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
                    Math.Max(rules.Combat.MinimumPathDamage,profile.Damage)/Math.Max(rules.Combat.MinimumActionInterval,profile.AttackInterval));
        }
        private void AwardWellResources()
        {
            foreach(var well in wells)
            {
                if(well.Active&&well.EligibleWave<=WaveIndex)
                    coloredInk[(int)well.Color]=(int)Math.Min(int.MaxValue,(long)coloredInk[(int)well.Color]+Math.Max(0,rules.WellInkPerWave));
                well.Threatened=false;
            }
        }
    }
}
