// Original activity gacha availability condition; player and activity services are supplied by the host.
using System;
using System.Collections.Generic;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeActivityTipDots
{
    public interface IHomeActivityGachaCostTipServices : IHomeActivityGachaTipServices
    {
        int GetFreeTimes(int activityId, int gachaId);
        bool IsGetAllBigAward(int activityId, int gachaId);
        bool HasMatchAward(int activityId, int gachaId);
        bool HasItems(IDictionary<int, long> items);
        event Action<int, int> OnActivityGachaFreeTimesUpdated;
        event Action OnSexUpdate;
        event Action OnProfessionChange;
    }

    [Serializable]
    public class TipDotActivityGachaHasCostConditionNode : TipDotActivityConditionNodeBase
    {
        public int Times;
        private IDisposable mMessageSubscription;
        private HomeActivityNoRedDotGacha mWithGachaIdData;
        private int mActivityId, mActivitySpecialGachaId;
        private readonly Dictionary<int, long> mCostItems = new Dictionary<int, long>();
        private HashSet<int> mAwardItems = new HashSet<int>();
        private IHomeActivityGachaCostTipServices GachaServices => (IHomeActivityGachaCostTipServices)Services;
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
            mMessageSubscription = GachaServices.SubscribeNoRedDotWithMessage(OnNoRedDotWithMessageUpdate);
            GachaServices.OnItemCountUpdate += OnItemCountUpdate;
            GachaServices.OnActivityGachaFreeTimesUpdated += OnActivityGachaFreeTimesUpdated;
            GachaServices.OnSexUpdate += OnSexUpdate;
            GachaServices.OnProfessionChange += OnProfessionChange;
            GachaServices.OnSpecialGachaEnd += OnSpecialGachaEnd;
            UpdateCostItem();
            mAwardItems = GachaServices.GetCanAwardItems(mActivitySpecialGachaId);
        }

        private void OnProfessionChange() => SetDirty();
        private void OnSexUpdate() => SetDirty();
        private void OnSpecialGachaEnd(int gachaId) => SetDirty();
        private void OnActivityGachaFreeTimesUpdated(int activityId, int gachaId) { UpdateCostItem(); SetDirty(); }
        private void OnItemCountUpdate(int itemId) { if (mCostItems.ContainsKey(itemId) || mAwardItems.Contains(itemId)) SetDirty(); }
        private void UpdateCostItem()
        {
            mCostItems.Clear();
            IDictionary<int, long> cost = GachaServices.GetCosts(mActivitySpecialGachaId);
            int freeTimes = GachaServices.GetFreeTimes(mActivityId, mActivitySpecialGachaId);
            int count = Times - freeTimes;
            foreach (KeyValuePair<int, long> item in cost) mCostItems.Add(item.Key, item.Value * count);
        }

        public override void Release()
        {
            mMessageSubscription.Dispose();
            if (GachaServices.HasPlayer)
            {
                GachaServices.OnItemCountUpdate -= OnItemCountUpdate;
                GachaServices.OnActivityGachaFreeTimesUpdated -= OnActivityGachaFreeTimesUpdated;
                GachaServices.OnSpecialGachaEnd -= OnSpecialGachaEnd;
                GachaServices.OnSexUpdate -= OnSexUpdate;
                GachaServices.OnProfessionChange -= OnProfessionChange;
            }
            base.Release();
        }

        public override bool CheckShowTip()
        {
            if (!base.CheckShowTip() || GachaServices.IsNoRedDotWithGachaSet(mWithGachaIdData)
                || !GachaServices.HasActivity(ActivityType.ActivityGacha, mActivityId)) return false;
            if (GachaServices.GetFreeTimes(mActivityId, mActivitySpecialGachaId) >= Times) return true;
            if (GachaServices.IsGetAllBigAward(mActivityId, mActivitySpecialGachaId)) return false;
            if (!GachaServices.HasMatchAward(mActivityId, mActivitySpecialGachaId)) return false;
            return GachaServices.HasItems(mCostItems);
        }

        private void OnNoRedDotWithMessageUpdate(IHomeActivityNoRedDotMessage data)
        {
            if (data.ActivityType == ActivityType.ActivityGacha && data.ActivityId == mActivityId)
            {
                if (data is HomeActivityNoRedDotGacha gacha && gacha.GachaId == mActivitySpecialGachaId) mWithGachaIdData = gacha;
                SetDirty();
            }
        }
        public override TipDotNodeBase Clone()
        {
            var node = new TipDotActivityGachaHasCostConditionNode(); node.Fill(this); return node;
        }
        public virtual void Fill(TipDotNodeBase source)
        {
            var node = source as TipDotActivityGachaHasCostConditionNode;
            if (node == null) throw new ArgumentException(GetType().Name, nameof(source));
            Fill(node);
        }
        public void Fill(TipDotActivityGachaHasCostConditionNode source) { Times = source.Times; }
    }
}
