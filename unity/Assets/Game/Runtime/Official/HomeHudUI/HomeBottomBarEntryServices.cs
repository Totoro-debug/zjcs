using System;
using System.Collections.Generic;

namespace Zjcs.Official.HomeHudUI
{
    public interface IHomePlayerUiServices
    {
        bool HasPlayer { get; }
        bool ManagerAvailable { get; }
        UnityEngine.Events.UnityEvent OnPlayerUpdate { get; }
        Zjcs.Official.ProfessionUI.IProfessionTipDotSystem TipDotSystem { get; }
    }

    public interface IHomePetEvolutionServices
    {
        IEnumerable<long> GetAllPetIndices();
        bool PetCanEvolution(long index);
    }

    public interface IHomeHudCondition { }

    public interface IHomeHudConditionHandler
    {
        void Register(IHomeHudCondition condition, Action refresh);
        void Deregister(IHomeHudCondition condition, Action refresh);
    }

    public interface IHomeMapActiveableEntity
    {
        MapWorldActiveableEntityType ActiveEntType { get; }
    }

    public interface IHomeHudGameData
    {
        long GameId { get; }
        int StageId { get; }
        StageType StageType { get; }
        bool IsBackground { get; }
    }

    public interface IHomeBottomBarPlayer
    {
        long LegionId { get; }
        event Action<int, int, int> OnStageCountUpdate;
        event Action<IHomeMapActiveableEntity> OnActiveableEntUpdate;
        event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
        event Action<int> OnLevelUpdate;
        event Action<IHomeHudGameData> OnGameDataAdd;
        event Action<long, bool> OnGameDataRemove;
        event Action<long, long> OnLegionUpdate;
        IReadOnlyDictionary<long, IHomeHudGameData> CurrentGames { get; }
        bool IsUnlockDemonBattle();
        bool IsFunctionLimit(FunctionType type);
        bool IsMopUpUnlocked();
        bool IsUnlockDailyDungeon();
        bool IsMapGamePlayFruitUnlock();
        bool IsMapGamePlayEnergyCollectUnlock();
    }

    public interface IHomeLegionMapDataManager
    {
        void QueryAndSubscribeHangUpAffairMsg();
        void QueryAndSubscribeBuildingData();
        void QueryAndSubscribeMemberHangUpMsg();
        void QueryAndSubscribeLifeHangUpData();
        void QueryAndSubscribeItemBagData();
        void UnsubscribeHangUpAffairMsg();
        void UnsubscribeBuildingData();
        void UnsubscribeMemberHangUpMsg();
        void UnsubscribeLifeHangUpData();
        void UnsubscribeItemBagData();
    }

    public interface IHomeBottomBarEntryServices
    {
        IHomeBottomBarPlayer MainPlayer { get; }
        bool ManagerAvailable { get; }
        event Action OnGameUpdate;
        IHomeHudGameData MainGame { get; }
        IHomeHudConditionHandler ConditionHandler { get; }
        IHomeHudCondition GetFunctionCondition(FunctionType type);
        IHomeLegionMapDataManager LegionMapDataManager { get; }
        StageType GetStageType(int stageId);
        void SystemMessage(string key);
        void SwitchInGame(long gameId);
        bool IsLegionBossPanelPopup();
        void CloseLegionBossPanel();
    }
}
