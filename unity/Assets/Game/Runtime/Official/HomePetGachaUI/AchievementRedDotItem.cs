// Original legacy achievement indicator; native key identity remains in the host.
using System;
using System.Collections.Generic;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomePetGachaUI
{
    public interface IHomeAchievementRedDotServices
    {
        bool HasPlayer { get; }
        object CreateKey(int achievementType, string jsonParam);
        bool ShowAllForType(object key);
        IEnumerable<object> GetKeysOfSameType(object key);
        bool KeysEqual(object left, object right);
        int GroupId(object key);
        List<int> GetAllAchievementGroupIds();
        bool IsAchievementGroupLimited();
        bool HasAvailableReward(object key);
        bool HasAvailableReward(List<object> keys);
        IDisposable SubscribeAchievementGroupLimit(Action changed);
        event Action<int> OnRewardRecorded;
        event Action<object> OnAchievementUpdated;
        event Action OnRewardsRecorded;
    }

    public class AchievementRedDotItem : RedDotItemBase
    {
        public List<AchievementTypeAndParamItem> AchievementTypeAndParamList;
        protected readonly List<object> mDataKeys = new List<object>();
        protected List<int> mAchievementGroupIds = new List<int>();
        protected IDisposable mConditionHandlerRegistration;
        public IHomeAchievementRedDotServices AchievementServices { get; set; }

        protected virtual void Awake()
        {
            if (AchievementTypeAndParamList.Count == 0) return;
            mDataKeys.Clear();
            foreach (AchievementTypeAndParamItem item in AchievementTypeAndParamList)
                AddDataKey(GetAchievementDataKey(item));
        }

        protected void AddDataKey(object key)
        {
            if (AchievementServices.ShowAllForType(key))
                mDataKeys.AddRange(AchievementServices.GetKeysOfSameType(key));
            else
                mDataKeys.Add(key);
        }

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            AchievementServices.OnRewardRecorded += MainPlayer_OnRecordAlreadyGotAchievementReward;
            AchievementServices.OnAchievementUpdated += MainPlayer_OnUpdateAchievementData;
            AchievementServices.OnRewardsRecorded += MainPlayer_OnRecordAlreadyGotAchievementRewardBatch;
            mAchievementGroupIds = AchievementServices.GetAllAchievementGroupIds();
            mConditionHandlerRegistration = AchievementServices.SubscribeAchievementGroupLimit(SetDirty);
        }

        protected override void OnDisableAndInited()
        {
            if (AchievementServices.HasPlayer)
            {
                AchievementServices.OnRewardRecorded -= MainPlayer_OnRecordAlreadyGotAchievementReward;
                AchievementServices.OnAchievementUpdated -= MainPlayer_OnUpdateAchievementData;
                AchievementServices.OnRewardsRecorded -= MainPlayer_OnRecordAlreadyGotAchievementRewardBatch;
            }
            if (mConditionHandlerRegistration != null)
            {
                mConditionHandlerRegistration.Dispose();
                mConditionHandlerRegistration = null;
            }
            base.OnDisableAndInited();
        }

        private void MainPlayer_OnUpdateAchievementData(object achievementDataKey)
        {
            foreach (object key in mDataKeys)
                if (AchievementServices.KeysEqual(key, achievementDataKey))
                {
                    Refresh();
                    break;
                }
        }

        private void MainPlayer_OnRecordAlreadyGotAchievementReward(int groupId) => Refresh();
        private void MainPlayer_OnRecordAlreadyGotAchievementRewardBatch() => Refresh();
        protected object GetAchievementDataKey(AchievementTypeAndParamItem data)
            => AchievementServices.CreateKey(data.AchievementType, data.JsonParam);

        public void SetDataKeys(List<object> dataKeys)
        {
            mDataKeys.Clear();
            foreach (object dataKey in dataKeys) AddDataKey(dataKey);
            Refresh();
        }

        public override bool ShowRedDot()
        {
            if (!AchievementServices.IsAchievementGroupLimited())
            {
                foreach (object key in mDataKeys)
                    if (!mAchievementGroupIds.Contains(AchievementServices.GroupId(key))
                        && AchievementServices.HasAvailableReward(key)) return true;
                return false;
            }
            return AchievementServices.HasAvailableReward(mDataKeys);
        }
    }
}
