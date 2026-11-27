using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using S = Zjcs.Official.TreasureSlots;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineTreasureSlotService
    {
        public readonly S.GAction<S.BattlePlanType,int> OnBattlePlanContentCleared = new S.GAction<S.BattlePlanType,int>();

        public int GetAutomaticSetupTargetIndex(int treasureId)
        {
            if (!catalog.TryGetElement(treasureId,out int element)) return -1;
            session.Reload();
            return Snapshot().GetCanSetupTreasureSlotIndex((S.ElementType)element);
        }

        public ProgressionResult ApplyTreasurePlan(int index,string receipt) => ChangeTreasurePlan(index,false,receipt);
        public ProgressionResult ClearTreasurePlan(int index,string receipt) => ChangeTreasurePlan(index,true,receipt);

        private ProgressionResult ChangeTreasurePlan(int index,bool clear,string receipt)
        {
            if(index<0 || index>=catalog.MaxPlanCount || string.IsNullOrWhiteSpace(receipt) || receipt.Length>160)
                return Result(ProgressionResultStatus.InvalidState);
            var notifications=new List<Action>();ProgressionResult result;
            try {
                session.Reload();
                if(session.Save.Player.AppliedInventoryEventIds.Contains(receipt)) return Result(ProgressionResultStatus.Unchanged);
                result=session.ApplyInventoryTransaction(receipt,next=>{
                    var player=CreatePlayer(next.Player,catalog);
                    // Original EnsurePlanEntries materializes every configured plan, including empty plans.
                    for(int i=0;i<catalog.MaxPlanCount;i++) player.GetOrCreatePlanContent(S.BattlePlanType.Treasure,i);
                    if(!clear) {
                        S.PlayerTreasurePlanExt.TryGetTreasurePlanSlots(player,index,out var target);
                        // A stale/edited plan must not manufacture equipment or silently discard an unavailable reference.
                        var references=target.SelectMany(e=>e.Value.Values).ToArray();
                        if(references.Distinct().Count()!=references.Length || references.Any(id=>
                            !next.Player.Inventory.Any(item=>item.ItemId==id && item.Count>0)
                            || !next.Player.CharacterProperties.Treasures.Any(item=>item.ClassId==id && item.Active)))
                            throw new InvalidOperationException("Treasure plan references unavailable equipment.");
                    }
                    player.OnSetupTreasureSlot+=()=>notifications.Add(()=>OnSetupTreasureSlot?.Invoke());
                    player.OnBattlePlanContentCleared.Subscribe((type,plan)=>notifications.Add(()=>OnBattlePlanContentCleared.SafeInvoke(type,plan)));
                    if(clear) S.TreasurePlanCallbacks.OperPlayerClearBattlePlanCallback(player,
                        new S.PlayerClearBattlePlanRequest{PlanType=S.BattlePlanType.Treasure,PlanIndex=index},0);
                    else S.TreasurePlanCallbacks.OperPlayerApplyBattlePlanCallback(player,
                        new S.PlayerApplyBattlePlanRequest{PlanType=S.BattlePlanType.Treasure,PlanIndex=index},0);
                    StorePlayer(next.Player,player);
                });
            }
            catch(IOException error) {return Result(ProgressionResultStatus.InvalidState,error.Message);}
            catch(UnauthorizedAccessException error) {return Result(ProgressionResultStatus.InvalidState,error.Message);}
            catch(FormatException error) {return Result(ProgressionResultStatus.InvalidState,error.Message);}
            if(result.IsApplied) foreach(var notification in notifications) notification();
            return result;
        }
    }
}
