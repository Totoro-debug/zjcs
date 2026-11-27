// Original treasure plan state transitions with explicit player/request and logging ports.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using TreasureSlotItemData = Zjcs.Official.TreasureSlots.PlayerTreasureSlotItemDataWrap;
using TreasureSlotData = Zjcs.Official.TreasureSlots.PlayerTreasureSlotDataWrap;

namespace Zjcs.Official.TreasureSlots
{
    public static class TreasurePlanDiagnostics { public static Action<Exception> ReportObserverError; }
    /*SIGNAL*/
    public sealed class PlayerApplyBattlePlanRequest { public BattlePlanType PlanType; public int PlanIndex; }
    public sealed class PlayerClearBattlePlanRequest { public BattlePlanType PlanType; public int PlanIndex; }
    public sealed partial class Player
    {
        public GAction<BattlePlanType,int> OnBattlePlanContentCleared = new GAction<BattlePlanType,int>();
        public bool TryGetPlanContent(BattlePlanType type,int index,out PlayerBattlePlanContentBaseWrap content)
        {
            content=null;
            if(type!=BattlePlanType.Treasure || index<0 || index>=settings.TreasurePlanMaxCount || !Plans.TryGetValue(index,out var row)) return false;
            content=row;return row!=null;
        }
        // The local model owns the Treasure facet of BattlePlanAccessor's active-index dictionary.
        public bool SetActivePlanIndex(BattlePlanType type,int index)
        {
            if(type!=BattlePlanType.Treasure || index<0 || index>=settings.TreasurePlanMaxCount) return false;
            ActivePlanIndex=index;return true;
        }
        /*NOTIFY*/
    }
    public static partial class PlayerTreasurePlanExt { /*METHODS*/ }
    public static class TreasurePlanCallbacks { /*CALLBACKS*/ }
}
