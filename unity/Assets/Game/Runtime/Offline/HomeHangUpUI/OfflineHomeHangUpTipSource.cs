using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zjcs.Offline.Home;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneUiControls;

namespace Zjcs.Offline.HomeHangUpUI
{
    // Owns no speculative rate, rank, preference, facility, or visitor defaults.
    // The transaction owner forwards these notifications after durable commits.
    public sealed class OfflineHomeHangUpTipSource : IHomeHangUpTipServices, IHomeHangUpTipPlayer
    {
        readonly OfflineHomeIncomeSession session;
        readonly Func<long> now;
        readonly Func<bool> ownHome, canGetExp;
        readonly Func<int> level;
        readonly Func<int, long> levelUpExp;
        readonly Func<EFurniture, bool> quickReceive;
        readonly Action<EFurniture, int> switchState;
        readonly Action<HangUpTipLevelChangeData> publish;

        public OfflineHomeHangUpTipSource(OfflineHomeIncomeSession session,
            OfflineHomeHangUpTipConfiguration configuration, Func<long> now, Func<bool> ownHome,
            Func<int> level, Func<bool> canGetExp, Func<int, long> levelUpExp,
            Func<EFurniture, bool> quickReceive, Action<EFurniture, int> switchState,
            Action<HangUpTipLevelChangeData> publish)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            this.ownHome = ownHome ?? throw new ArgumentNullException(nameof(ownHome));
            this.level = level ?? throw new ArgumentNullException(nameof(level));
            this.canGetExp = canGetExp ?? throw new ArgumentNullException(nameof(canGetExp));
            this.levelUpExp = levelUpExp ?? throw new ArgumentNullException(nameof(levelUpExp));
            this.quickReceive = quickReceive ?? throw new ArgumentNullException(nameof(quickReceive));
            this.switchState = switchState ?? throw new ArgumentNullException(nameof(switchState));
            this.publish = publish ?? throw new ArgumentNullException(nameof(publish));
            Settings = configuration.CreateSettings(); RecordHangUpIdDict = configuration.CreateRecordIds();
        }

        public IHomeHangUpTipPlayer MainPlayer => this;
        public IHomeHangUpTipPlayer PlayerData => this;
        public bool InMyHome => ownHome();
        public int Level => level();
        public bool CanGetExp() => canGetExp();
        public long GetLevelUpExp(int value) => levelUpExp(value);
        public HomeHangUpTipSettings Settings { get; }
        public IReadOnlyDictionary<EFurniture, List<int>> RecordHangUpIdDict { get; }
        public bool HasFurniture(EFurniture furniture) => session.OwnsFurniture((int)furniture);
        public bool GetHangUpQuickReceiveState(EFurniture furniture) => quickReceive(furniture);
        public IReadOnlyDictionary<int, HomeHangUpTipRecord> HangUpDataDict
        {
            get
            {
                var result = new Dictionary<int, HomeHangUpTipRecord>();
                foreach (var entry in session.Definitions)
                    result.Add(entry.HangUpId, new HomeHangUpTipRecord { IsUnlock = entry.Unlocked });
                return result;
            }
        }
        HangUpIncomePreview Preview(int id) { session.AdvanceTo(now()); return session.Preview(id); }
        public long GetHangUpAwardCount(int id) => Preview(id).Count;
        public long GetHangUpMaxCount(int id) => Preview(id).Capacity;
        public double GetHangUpHourOutput(int id) => Preview(id).HourlyRate;
        public void SetTipState(EFurniture furniture, int tipLevel) => switchState(furniture, tipLevel);
        public void Publish(HangUpTipLevelChangeData data) => publish(data);

        public event Action<int> OnHangUpUnlock;
        public event Action<int> OnHangUpDataChanged;
        public event Action<EFurniture> OnRepairFurniture;
        public event Action<long> OnDebugTimeOffset;
        public event Action<int> OnLevelUpdate;
        public void NotifyUnlocked(int id) => OnHangUpUnlock?.Invoke(id);
        public void NotifyDataChanged(int id) => OnHangUpDataChanged?.Invoke(id);
        public void NotifyRepaired(EFurniture furniture) => OnRepairFurniture?.Invoke(furniture);
        public void NotifyDebugTimeOffset(long offset) => OnDebugTimeOffset?.Invoke(offset);
        public void NotifyLevelChanged(int value) => OnLevelUpdate?.Invoke(value);

        public IDisposable Wait(float seconds, Action completed)
        {
            var cancellation = new CancellationTokenSource();
            Wait(seconds, cancellation.Token, completed).Forget();
            return new ScheduledWait(cancellation);
        }
        static async UniTaskVoid Wait(float seconds, CancellationToken token, Action completed)
        {
            await UniTask.WaitForSeconds(seconds, false, PlayerLoopTiming.Update, token, false);
            completed();
        }
        sealed class ScheduledWait : IDisposable
        {
            CancellationTokenSource source;
            public ScheduledWait(CancellationTokenSource source) { this.source = source; }
            public void Dispose()
            {
                var value = source; source = null;
                if (value == null) return;
                value.Cancel(); value.Dispose();
            }
        }
    }
}
