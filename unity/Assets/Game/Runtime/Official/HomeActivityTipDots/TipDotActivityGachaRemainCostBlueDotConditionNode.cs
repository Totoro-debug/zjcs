// Original activity gacha tip conditions; platform managers are supplied by the host.
using System;
using System.Collections.Generic;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeActivityTipDots
{
    public class ActivityGachaNodeInstanceInfo : ActivityNodeInstanceInfo
    {
        public int SpecailGachaId;
        public ActivityGachaNodeInstanceInfo(int activityId, int specialGachaId) : base(activityId)
        { SpecailGachaId = specialGachaId; }
        public override string GetInstanceDesc() => $"ActivityId:{ActivityId};SpecialGachaId:{SpecailGachaId}";
        public override bool Equals(object value) => value is ActivityGachaNodeInstanceInfo other
            && ActivityId == other.ActivityId && SpecailGachaId == other.SpecailGachaId;
        public override int GetHashCode() => HashCode.Combine(ActivityId, SpecailGachaId);
    }

    public interface IHomeActivityNoRedDotMessage
    {
        ActivityType ActivityType { get; }
        int ActivityId { get; }
    }

    public struct HomeActivityNoRedDotGacha : IHomeActivityNoRedDotMessage
    {
        public ActivityType ActivityType { get; set; }
        public int ActivityId { get; set; }
        public int GachaId { get; set; }
    }

    public interface IHomeActivityTipServices
    {
        event Action<HomeTipActivity> OnActivityOpen;
        event Action<HomeTipActivity> OnActivityClose;
        event Action<ActivityType> OnActivitiesUpdate;
        bool HasActivity(ActivityType type, int activityId);
        IDisposable SubscribeNoRedDotSetting(Action<HomeTipActivity> callback);
        bool IsActivityNoRedDotSet(ActivityType type, int activityId);
    }

    public interface IHomeActivityGachaTipServices : IHomeActivityTipServices
    {
        bool HasPlayer { get; }
        IDictionary<int, long> GetCosts(int gachaId);
        HashSet<int> GetCanAwardItems(int gachaId);
        IDisposable SubscribeNoRedDotWithMessage(Action<IHomeActivityNoRedDotMessage> callback);
        bool IsNoRedDotWithGachaSet(HomeActivityNoRedDotGacha data);
        long GetItemAmount(int itemId);
        event Action<int> OnItemCountUpdate;
        event Action<int> OnSpecialGachaEnd;
    }

    public abstract class TipDotActivityConditionNodeBase : TipDotConditionNodeBase
    {
        private IDisposable mSettingSubscription;
        public IHomeActivityTipServices Services { get; set; }
        protected abstract ActivityType ActivityType { get; }
        protected abstract int ActivityId { get; }
        protected virtual bool RespectActivityNoRedDotSetting => true;
        public override void Init()
        {
            base.Init();
            mSettingSubscription = Services.SubscribeNoRedDotSetting(OnNoRedDotSettingUpdate);
            Services.OnActivityOpen += OnActivityOpen;
            Services.OnActivityClose += OnActivityClose;
            Services.OnActivitiesUpdate += OnActivitiesUpdate;
        }
        public override void Release()
        {
            Services.OnActivityOpen -= OnActivityOpen;
            Services.OnActivityClose -= OnActivityClose;
            Services.OnActivitiesUpdate -= OnActivitiesUpdate;
            mSettingSubscription.Dispose();
            base.Release();
        }
        private void OnActivityOpen(HomeTipActivity data) { if (data.Type == ActivityType && data.Id == ActivityId) SetDirty(); }
        private void OnActivityClose(HomeTipActivity data) { if (data.Type == ActivityType && data.Id == ActivityId) SetDirty(); }
        private void OnActivitiesUpdate(ActivityType type) { if (type == ActivityType) SetDirty(); }
        private void OnNoRedDotSettingUpdate(HomeTipActivity data) { if (data.Type == ActivityType && data.Id == ActivityId) SetDirty(); }
        public override bool CheckShowTip() => !(RespectActivityNoRedDotSetting && Services.IsActivityNoRedDotSet(ActivityType, ActivityId));
    }

    [Serializable]
    public class TipDotActivityGachaRemainCostBlueDotConditionNode : TipDotActivityConditionNodeBase
    {
        public int Times;
        private IDisposable mMessageSubscription;
        private HomeActivityNoRedDotGacha mWithGachaIdData;
        private int mActivityId, mActivitySpecialGachaId;
        private IDictionary<int, long> mCostItems = new Dictionary<int, long>();
        private HashSet<int> mAwardItems = new HashSet<int>();
        private IHomeActivityGachaTipServices GachaServices => (IHomeActivityGachaTipServices)Services;
        protected override ActivityType ActivityType => ActivityType.ActivityGacha;
        protected override int ActivityId => mActivityId;
        public override void Init()
        {
            base.Init();
            if (NodeKey.InstanceInfo is ActivityGachaNodeInstanceInfo info)
            {
                mActivityId = info.ActivityId;
                mActivitySpecialGachaId = info.SpecailGachaId;
                mWithGachaIdData = new HomeActivityNoRedDotGacha { ActivityId = mActivityId, ActivityType = ActivityType.ActivityGacha, GachaId = mActivitySpecialGachaId };
            }
            mCostItems = GachaServices.GetCosts(mActivitySpecialGachaId);
            mAwardItems = GachaServices.GetCanAwardItems(mActivitySpecialGachaId);
            mMessageSubscription = GachaServices.SubscribeNoRedDotWithMessage(OnNoRedDotWithMessageUpdate);
            GachaServices.OnItemCountUpdate += OnItemCountUpdate;
            GachaServices.OnSpecialGachaEnd += OnSpecialGachaEnd;
        }
        public override void Release()
        {
            mMessageSubscription.Dispose();
            if (GachaServices.HasPlayer)
            {
                GachaServices.OnItemCountUpdate -= OnItemCountUpdate;
                GachaServices.OnSpecialGachaEnd -= OnSpecialGachaEnd;
            }
            base.Release();
        }
        private void OnSpecialGachaEnd(int gachaId) => SetDirty();
        private void OnItemCountUpdate(int itemId) { if (mCostItems.ContainsKey(itemId) || mAwardItems.Contains(itemId)) SetDirty(); }
        private void OnNoRedDotWithMessageUpdate(IHomeActivityNoRedDotMessage data)
        {
            if (data.ActivityType == ActivityType.ActivityGacha && data.ActivityId == mActivityId)
            {
                if (data is HomeActivityNoRedDotGacha gacha && gacha.GachaId == mActivitySpecialGachaId) mWithGachaIdData = gacha;
                SetDirty();
            }
        }
        public override bool CheckShowTip()
        {
            if (!base.CheckShowTip() || GachaServices.IsNoRedDotWithGachaSet(mWithGachaIdData)
                || !GachaServices.HasActivity(ActivityType.ActivityGacha, mActivityId)) return false;
            long count = -1L;
            foreach (KeyValuePair<int, long> cost in mCostItems)
            {
                long available = GachaServices.GetItemAmount(cost.Key) / (Times * cost.Value);
                if (count == -1 || count > available) count = available;
            }
            TipDotInfo.Data = count.ToString();
            return count > 0;
        }
        public override TipDotNodeBase Clone()
        {
            var node = new TipDotActivityGachaRemainCostBlueDotConditionNode(); node.Fill(this); return node;
        }
        public virtual void Fill(TipDotNodeBase source)
        {
            var node = source as TipDotActivityGachaRemainCostBlueDotConditionNode;
            if (node == null) throw new ArgumentException(GetType().Name, nameof(source));
            Fill(node);
        }
        public void Fill(TipDotActivityGachaRemainCostBlueDotConditionNode source) { Times = source.Times; }
    }
}
