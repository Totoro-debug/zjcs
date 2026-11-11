using System;
using System.Collections;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeHudUI
{
    // Operations consumed by the original PlayerUIBase and PlayerExpSlider.
    // The selected profile supplies player data, parsers and reward sequencing.
    public interface IHomeExperienceServices
    {
        IHomeExperiencePlayer MainPlayer { get; }
        bool ManagerAvailable { get; }
        SafeAction OnPlayerUpdate { get; }
        IHomeLevelExpParser LevelExpParser { get; }
        IHomeLevelBlessParser LevelBlessInfoParser { get; }
        IHomeExperienceRewards Rewards { get; }
        int PlayerExpItemId { get; }
        int PlanesRank { get; }
        void ResolveSubRank(int subRank);
        object Compact(long value);
        string GetSeasonNameByRank(int rank);
    }

    public interface IHomeExperiencePlayer
    {
        int LevelLimit { get; }
        int Rank { get; }
        int BaseLevel { get; }
        int Level { get; }
        int SubRank { get; }
        event Action<HomeExperienceItem, long, object> OnItemCountUpdate;
        event Action<int> OnSubRankUpdate;
        event Action OnBlessLevelDataUpdate;
        event Action<object> OnSeasonRecordDataAdd;
        event Action<object> OnSeasonRecordDataEndUpdate;
        bool IsBlessLevelOpen();
        bool ShowPlayerStoreExpLimit();
        int GetMaxStoreExpLevel();
        bool IsMaxBlessLevel();
        long GetCurBlessPlayerExp();
        int GetCurBlessPlayerLevel();
        long GetItemAmount(int itemId);
    }

    public sealed class HomeExperienceItem { public int Id; }
    public sealed class HomeLevelBlessInfo { public long Exp; }

    public interface IHomeLevelExpParser
    {
        long GetExp(int id, int level);
        int CalcLevelByExp(int id, long exp);
        long GetLevelUpExp(int id, int level);
        long GetLevelUpExp(int id, int fromLevel, int toLevel);
    }

    public interface IHomeLevelBlessParser
    {
        int GetBlessLevel(int rank, long exp);
        HomeLevelBlessInfo Get(int rank, int blessLevel);
        long GetExpRequestByBlessLevel(int blessLevel);
    }

    public interface IHomeExperienceRewards
    {
        long HandledExp { get; }
        long PlayerRealExp { get; }
        void RegisterRewardExpDisplay(Func<long, long, IEnumerator> display);
        void UnregisterRewardExpDisplay(Func<long, long, IEnumerator> display);
    }
}
