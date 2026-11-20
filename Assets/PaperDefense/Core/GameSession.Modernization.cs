using System;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        public ModernizationQuote QuoteModernization(int structureId,ModernizationDirection direction)=>QuoteModernizationInternal(structureId,direction,InkColor.None,false);
        public ModernizationQuote QuoteColorModernization(int structureId,InkColor color)=>QuoteModernizationInternal(structureId,default,color,true);
        ModernizationQuote QuoteModernizationInternal(int id,ModernizationDirection direction,InkColor color,bool isColor)
        {
            StructureState tower=null;foreach(var candidate in structures)if(candidate.Id==id){tower=candidate;break;}
            var state=tower?.ModernizationState;var ranks=state?.Ranks??default;int revision=state?.Revision??0;
            var before=tower?.ModernizationEntry;var after=before;var result=ranks;int current=0,next=0,cost=0;float seconds=0;string reason="";
            bool validDirection=Enum.IsDefined(typeof(ModernizationDirection),direction);
            bool validColor=Enum.IsDefined(typeof(InkColor),color)&&color!=InkColor.None;
            var colorDefinition=isColor&&validColor?ColorEffectPayload.Capture(color,rules.Content):default;
            bool hasTower=before!=null;
            bool allocationValid=true;
            if(hasTower&&(isColor?validColor:validDirection))
            {
                current=isColor?ranks.ColorRank:ranks.Rank(direction);next=current+1;
                if(next<=3)
                {
                    var baseline=state?.Baseline??before;
                    cost=isColor?ModernizationRules.ColorCost(baseline.Cost,next):ModernizationRules.OrdinaryCost(baseline.Cost,direction,next);
                    seconds=ModernizationRules.WorkSeconds(next);
                    try
                    {
                        result=isColor?ranks.WithColor(color,next):ranks.Next(direction);
                        after=isColor?before:ModernizationRules.Resolve(baseline,result);
                    }
                    catch(ArgumentException){allocationValid=false;}
                }
            }
            if(!Mechanics.FieldModernization)reason="Боевое развитие отключено.";
            else if(Phase==RunPhase.Victory||Phase==RunPhase.Defeat)reason="Бой завершён.";
            else if(tower==null||tower.Health<=0)reason="Башня больше не существует.";
            else if(!hasTower)reason="Выберите боевую башню для модернизации.";
            else if(!tower.IsComplete||tower.BuildRemaining>0)reason="Башня ещё строится.";
            else if(state?.Pending!=null)reason="Идут работы.";
            else if(isColor&&!validColor)reason="Выберите цвет.";
            else if(!isColor&&!validDirection)reason="Неизвестное направление.";
            else if(isColor&&!Mechanics.ColorCartridges)reason="Цветные чернила отключены.";
            else if(isColor&&!ModernizationRules.HasColorEffect(colorDefinition))reason="У выбранных чернил нет действующего эффекта.";
            else if((isColor||direction==ModernizationDirection.Power)&&!ModernizationRules.HasAttack(before))reason="У промокашки нет атакующих орудий.";
            else if(isColor&&ranks.Color!=InkColor.None&&ranks.Color!=color)reason="Цвет уже закреплён за башней.";
            else if(next>3)reason="Достигнут максимальный ранг.";
            else if(next>Mechanics.MaximumModernizationRank)reason="Требуется технология следующего ранга.";
            else if(!isColor&&ranks.StepsUsed>=6)reason="Использованы все шесть улучшений.";
            else if(!allocationValid)reason="Только одно направление может достичь III.";
            else if(cost>(isColor?ColoredInk(color):Ink))reason=isColor?"Недостаточно цветных чернил.":"Недостаточно штрихов.";
            var beforePayload=ModernizationRules.ResolveColor(ColorEffectPayload.Capture(ranks.Color,rules.Content),ranks.ColorRank);
            var afterPayload=isColor&&validColor&&next>0&&next<=3?ModernizationRules.ResolveColor(colorDefinition,next):beforePayload;
            return new ModernizationQuote(RunId,id,revision,direction,color,isColor,current,next,cost,seconds,before,after,beforePayload,afterPayload,result,reason);
        }
        public bool TryModernize(ModernizationQuote quote,out string reason)
        {
            reason="Предложение устарело.";
            if(quote==null||!quote.Valid||quote.RunId!=RunId)return false;
            var fresh=quote.IsColor?QuoteColorModernization(quote.StructureId,quote.Color):QuoteModernization(quote.StructureId,quote.Direction);
            if(!fresh.Valid){reason=fresh.Reason;return false;}
            if(fresh.Revision!=quote.Revision||fresh.CurrentRank!=quote.CurrentRank||fresh.NextRank!=quote.NextRank||fresh.Cost!=quote.Cost)return false;
            var tower=StructureAtId(quote.StructureId);
            if(tower.ModernizationState==null)tower.ModernizationState=new ModernizationState(tower.ModernizationEntry);
            var state=tower.ModernizationState;
            if(quote.IsColor)
            {coloredInk[(int)quote.Color]-=fresh.Cost;tower.Color=quote.Color;state.Ranks=state.Ranks.WithColor(quote.Color,state.Ranks.ColorRank);}
            else {Ink-=fresh.Cost;tower.PaidInk+=fresh.Cost;Statistics.RecordUpgrade(fresh.Cost);}
            state.Pending=fresh;state.Remaining=fresh.WorkSeconds;state.Revision++;
            StructureVisualVersion++;PublishModernization(tower,SimulationEventKind.ModernizationStarted);reason="";return true;
        }
        StructureState StructureAtId(int id){foreach(var tower in structures)if(tower.Id==id)return tower;return null;}
        void AdvanceModernization(StructureState tower,float delta)
        {
            var state=tower.ModernizationState;if(state?.Pending==null||tower.Health<=0)return;
            state.Remaining=Math.Max(0,state.Remaining-delta);if(state.Remaining>.000001f)return;
            var purchase=state.Pending;float fraction=Clamp(tower.Health/Math.Max(1,tower.MaxHealth),0,1);
            state.Ranks=purchase.Result;state.Pending=null;state.Remaining=0;state.Revision++;
            if(tower.LegacyFieldEntry!=null)tower.LegacyFieldEntry=purchase.After;
            else tower.ResolvedEntry=purchase.After;
            tower.MaxHealth=purchase.After.Health*healthMultiplier;tower.Health=tower.MaxHealth*fraction;
            if(tower.ColorBudget!=null)tower.ColorBudget.SetTargetLimit(3+Math.Max(0,state.Ranks.ColorRank-1));
            MarkStructureChanged(tower,true);PublishModernization(tower,SimulationEventKind.ModernizationCompleted);
        }
        void PublishModernization(StructureState tower,SimulationEventKind kind)=>EventBuffer.Publish(TickIndex,kind,Center(tower.Cell),Center(tower.Cell),tower.Kind,entityId:tower.Id,sourceStructureId:tower.Id,weaponId:tower.ContentId,simulationSeconds:Elapsed);
    }
}
