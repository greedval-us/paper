using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public static class ConstructionToolRules
    {
        public static bool IsTool(StructureKind kind)=>kind==StructureKind.Wall||kind==StructureKind.Spikes||kind==StructureKind.TarPit||kind==StructureKind.RepairPost;
        public static bool IsTrap(StructureKind kind)=>kind==StructureKind.Spikes||kind==StructureKind.TarPit;
    }
    public sealed partial class GameSession
    {
        bool TryMapSpawn(int side,out Cell cell)
        {
            // Rotate through matching entrances; a full entrance delays its wave backlog.
            int start=random.Next(Battlefield.Entrances.Count);
            for(int i=0;i<Battlefield.Entrances.Count;i++)
            {
                var candidate=Battlefield.Entrances[(start+i)%Battlefield.Entrances.Count];
                if(Battlefield.Side(candidate)==side&&crowd.CanEnter(candidate)){cell=candidate;return true;}
            }
            cell=default;return false;
        }
        readonly List<StructureState> activeRepairPosts=new List<StructureState>();
        readonly List<StructureState> activeTarPits=new List<StructureState>();
        void ApplySupport(float delta)
        {
            activeRepairPosts.Clear();activeTarPits.Clear();
            foreach(var structure in structures)
            {
                if(!structure.IsComplete||structure.Health<=0)continue;
                if(structure.Kind==StructureKind.RepairPost)activeRepairPosts.Add(structure);
                else if(structure.Kind==StructureKind.TarPit)activeTarPits.Add(structure);
            }
            if(activeRepairPosts.Count>0)foreach(var target in structures)
            {
                if(!target.IsComplete||target.Health<=0||target.Kind==StructureKind.RepairPost||target.Health>=target.MaxHealth)continue;
                float rate=0;
                foreach(var post in activeRepairPosts)
                {
                    var definition=rules.Structure(post.Kind);
                    if(DistanceSquared(Center(post.Cell),Center(target.Cell))<=definition.Range*definition.Range)
                        rate=Math.Max(rate,definition.RepairPerSecond);
                }
                if(rate<=0)continue;
                target.Health=Math.Min(target.MaxHealth,target.Health+rate*delta);
                RefreshHealthBand(target);
            }
            foreach(var trap in activeTarPits)
            {
                var definition=rules.Structure(trap.Kind);
                float radius=Math.Max(.5f,definition.Radius);
                foreach(var enemy in enemies)
                    if(enemy.Health>0&&DistanceSquared(Center(trap.Cell),enemy.Position)<=radius*radius)
                        EnemyEffects.ApplySlow(enemy,definition.SlowSpeedMultiplier,Math.Max(delta,definition.SlowDuration));
            }
        }
    }
}
