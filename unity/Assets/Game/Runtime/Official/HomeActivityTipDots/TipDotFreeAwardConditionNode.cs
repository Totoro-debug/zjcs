// Original free-activity-award tip condition; managers are supplied by the host.
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeActivityTipDots
{
    public readonly struct HomeTipActivity
    {
        public readonly ActivityType Type;
        public readonly int Id;
        public HomeTipActivity(ActivityType type, int id) { Type = type; Id = id; }
    }

    public interface IHomeActivityFreeAwardServices
    {
        bool HasPlayer { get; }
        bool HasActivityManager { get; }
        bool TryResolveGiftId(ActivityType type, int activityId, out int giftId);
        bool TryGetFreeAwardEndTime(int giftId, out DateTime endTime);
        bool HasActivity(ActivityType type, int activityId);
        bool CanGetFreeReward(int giftId, DateTime now);
        DateTime GetDateTime();
        event Action<int> OnFreeAwardDataAdd;
        event Action<int> OnGetFreeReward;
        event Action<long> OnDebugTimeOffset;
        event Action<HomeTipActivity> OnActivityOpen;
        event Action<HomeTipActivity> OnActivityClose;
        event Action<ActivityType> OnActivitiesUpdate;
    }

    public abstract class TipDotFreeAwardConditionNodeBase : TipDotConditionNodeBase
    {
        private CancellationTokenSource mCancellationTokenSource;
        protected int mGiftId;
        public IHomeActivityFreeAwardServices Services { get; set; }
        protected abstract ActivityType TargetActivityType { get; }
        protected abstract int ActivityId { get; }
        protected abstract bool HasInstanceInfo { get; }
        protected abstract bool TryResolveGiftId(out int giftId);

        public override void Init()
        {
            base.Init();
            Services.OnFreeAwardDataAdd += OnActivityFreeAwardAdd;
            Services.OnGetFreeReward += OnActivityGetFreeAward;
            Services.OnDebugTimeOffset += OnDebugTimeOffset;
            Services.OnActivityOpen += OnActivityOpen;
            Services.OnActivityClose += OnActivityClose;
            Services.OnActivitiesUpdate += OnActivitiesUpdate;
            TryResolveGiftId(out mGiftId);
            mCancellationTokenSource = new CancellationTokenSource();
            if (Services.TryGetFreeAwardEndTime(mGiftId, out DateTime endTime)) StartTimer(endTime);
        }

        public override void Release()
        {
            if (Services.HasPlayer)
            {
                Services.OnFreeAwardDataAdd -= OnActivityFreeAwardAdd;
                Services.OnGetFreeReward -= OnActivityGetFreeAward;
                Services.OnDebugTimeOffset -= OnDebugTimeOffset;
            }
            if (Services.HasActivityManager)
            {
                Services.OnActivityOpen -= OnActivityOpen;
                Services.OnActivityClose -= OnActivityClose;
                Services.OnActivitiesUpdate -= OnActivitiesUpdate;
            }
            base.Release();
        }

        private void OnDebugTimeOffset(long tick)
        {
            StopTimer();
            mCancellationTokenSource = new CancellationTokenSource();
            if (Services.TryGetFreeAwardEndTime(mGiftId, out DateTime endTime)) StartTimer(endTime);
            SetDirty();
        }

        private void StartTimer(DateTime nextUpdateTime)
        {
            if (Services.GetDateTime() >= nextUpdateTime) SetDirty();
            else if (mCancellationTokenSource != null) StartTimer(nextUpdateTime, mCancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid StartTimer(DateTime endTime, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                DateTime now = Services.GetDateTime();
                if (now >= endTime) break;
                TimeSpan remaining = endTime - now;
                int delay = remaining.TotalDays > 1.0 ? 86400000 : (int)Math.Max(0.0, remaining.TotalMilliseconds);
                if (delay <= 0) break;
                await UniTask.Delay(delay, false, PlayerLoopTiming.Update, token);
            }
            SetDirty();
        }

        private void StopTimer()
        {
            if (mCancellationTokenSource != null)
            {
                mCancellationTokenSource.Cancel();
                mCancellationTokenSource.Dispose();
                mCancellationTokenSource = null;
            }
        }

        private void OnActivitiesUpdate(ActivityType type) { if (type == TargetActivityType) SetDirty(); }
        private void OnActivityOpen(HomeTipActivity data) { if (data.Type == TargetActivityType && data.Id == ActivityId) SetDirty(); }
        private void OnActivityClose(HomeTipActivity data) { if (data.Type == TargetActivityType && data.Id == ActivityId) SetDirty(); }
        private void OnActivityFreeAwardAdd(int giftId) { if (mGiftId == giftId) SetDirty(); }
        private void OnActivityGetFreeAward(int giftId) { if (mGiftId == giftId) SetDirty(); }

        public override bool CheckShowTip()
        {
            if (!HasInstanceInfo || !TryResolveGiftId(out int giftId)) return false;
            return Services.HasActivity(TargetActivityType, ActivityId)
                && Services.CanGetFreeReward(giftId, Services.GetDateTime());
        }
    }

    [Serializable]
    public class TipDotFreeAwardConditionNode : TipDotFreeAwardConditionNodeBase
    {
        public ActivityType ActivityType = ActivityType.PrePay;
        private ActivityNodeInstanceInfo InstanceInfo => NodeKey.InstanceInfo as ActivityNodeInstanceInfo;
        protected override ActivityType TargetActivityType => ActivityType;
        protected override int ActivityId => InstanceInfo.ActivityId;
        protected override bool HasInstanceInfo => InstanceInfo != null;
        protected override bool TryResolveGiftId(out int giftId)
        {
            if (!Services.TryResolveGiftId(ActivityType, InstanceInfo.ActivityId, out giftId)) giftId = 0;
            return giftId != 0;
        }
        public override TipDotNodeBase Clone()
        {
            var node = new TipDotFreeAwardConditionNode();
            node.Fill(this);
            return node;
        }
        public virtual void Fill(TipDotNodeBase source)
        {
            var node = source as TipDotFreeAwardConditionNode;
            if (node == null) throw new ArgumentException(GetType().Name, nameof(source));
            Fill(node);
        }
        public void Fill(TipDotFreeAwardConditionNode source) { ActivityType = source.ActivityType; }
    }
}
