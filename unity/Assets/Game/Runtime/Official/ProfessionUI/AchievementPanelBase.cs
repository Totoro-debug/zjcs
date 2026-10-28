using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Official.ProfessionUI
{
    // Original AchievementPanelBase list/filter/sort/five-row binding, restricted to
    // the profession group. Ranking and other achievement types do not use this port.
    public sealed class AchievementPanelBase : MonoBehaviour
    {
        public WorldStateSwitcher<bool> EmptyStateSwitcher;
        [SerializeField] private bool mIsShowReceivedItem, mIsShowProcessingItem;
        [SerializeField] private PanelTitle mPanelTitle;
        [SerializeField] private List<AchievementPanelItem> mPanelItemList;
        private IProfessionAchievementData data;
        private bool subscribed;
        public void InitDataKey(IProfessionAchievementData context)
        {
            if (subscribed && data != null) data.Changed -= InitData;
            data = context ?? throw new ArgumentNullException(nameof(context));
            subscribed = false;
            if (isActiveAndEnabled) Activate();
        }
        private void OnEnable() { if (data != null) Activate(); }
        private void Activate()
        {
            if (!subscribed) { data.Changed += InitData; subscribed = true; }
            if (mPanelTitle != null) mPanelTitle.SetKey("ui_achievement_title_TotalProfessionLevel");
            InitData();
        }
        private void OnDisable()
        {
            if (subscribed && data != null) data.Changed -= InitData;
            subscribed = false;
        }
        private void InitData()
        {
            IReadOnlyList<OfficialProfessionAchievementView> visible = data.Rules.VisibleWithStates(data.TotalLevel,
                data.Recorded, data.AchievementPanelShowCount, mIsShowReceivedItem, mIsShowProcessingItem);
            EmptyStateSwitcher.SetState(visible.Count == 0);
            for (int i = 0; i < mPanelItemList.Count; i++)
            {
                AchievementPanelItem item = mPanelItemList[i];
                item.gameObject.SetActive(i < visible.Count);
                if (i < visible.Count) item.Init(visible[i], data, GetRewardCallBack);
            }
        }
        private void GetRewardCallBack(int group, int count) => InitData();
    }
}
