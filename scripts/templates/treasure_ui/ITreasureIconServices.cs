using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureCultivation;

namespace Zjcs.Official.TreasureUI
{
    public interface ITreasureIconServices
    {
        bool PlayerAvailable { get; }
        bool ManagerAvailable { get; }
        SafeAction PlayerUpdated { get; }
        IProfessionTipDotSystem TipDotSystem { get; }
        event Action<IProfessionItemData,bool> OnTreasureRefresh;
        event Action<int> OnTreasureSlotShareLevelUpdate;
        event Action OnAllTreasureBlessUpdate;
        event Action<IProfessionItemData,long,object> OnItemCountUpdate;
        IDisposable SubscribeSettings(Action changed);
        IProfessionItemData GetItemById(int id);
        TreasureState GetState(IProfessionItemData data);
        int GetStarMax(IProfessionItemData data);
        int GetElement(int id);
        int GetTreasureFinalLevel(int id);
        int GetMaxTreasureBaseLevel();
        bool HasItem(int id,long count);
        bool ShowTip(IProfessionItemData item,List<TreasureRedDotEnum> tips,bool autoMerge,bool awake);
        bool IsPromotionItem(IProfessionItemData item);
        bool AutoMergeWhenLevelUp { get; }
        bool AwakeAutoMergeEnable { get; }
        ITreasureReplaceEventLisener ReplaceListener { get; }
        bool CanSetupTreasureSlot(int id,int index);
        void SetupTreasureSlot(PlayerSetupTreasureSlotRequest request);
        void SetUIGray(GameObject target,bool gray);
    }
    public sealed class PlayerSetupTreasureSlotRequest { public int TreasureClassId,TargetIndex; }
}
