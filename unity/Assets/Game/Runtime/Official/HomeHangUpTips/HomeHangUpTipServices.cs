using System;
using System.Collections.Generic;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneUiControls;

namespace Zjcs.Official.HomeHangUpTips
{
    public sealed class HomeHangUpTipRecord { public bool IsUnlock; }

    // Original Player/IHomePlayerData, parser, state switcher and message ports.
    public interface IHomeHangUpTipPlayer
    {
        bool HasFurniture(EFurniture furniture);
        IReadOnlyDictionary<int, HomeHangUpTipRecord> HangUpDataDict { get; }
        long GetHangUpAwardCount(int id);
        long GetHangUpMaxCount(int id);
        double GetHangUpHourOutput(int id);
        bool GetHangUpQuickReceiveState(EFurniture furniture);
        int Level { get; }
        bool CanGetExp();
        event Action<int> OnHangUpUnlock;
        event Action<int> OnHangUpDataChanged;
        event Action<EFurniture> OnRepairFurniture;
        event Action<long> OnDebugTimeOffset;
        event Action<int> OnLevelUpdate;
    }

    public interface IHomeHangUpTipServices
    {
        IHomeHangUpTipPlayer MainPlayer { get; }
        IHomeHangUpTipPlayer PlayerData { get; }
        bool InMyHome { get; }
        IReadOnlyDictionary<EFurniture, List<int>> RecordHangUpIdDict { get; }
        HomeHangUpTipSettings Settings { get; }
        long GetLevelUpExp(int level);
        void SetTipState(EFurniture furniture, int level);
        void Publish(HangUpTipLevelChangeData data);
        // Original scaled UniTask WaitForSeconds at Update, canceled on replace/disable.
        IDisposable Wait(float seconds, Action completed);
    }
}
