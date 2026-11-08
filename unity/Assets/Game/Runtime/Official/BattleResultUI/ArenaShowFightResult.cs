using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.BattleResultUI
{
    public sealed class ArenaFightResult
    {
        public int PreviousRank, CurrentRank;
        public long AwardCount;
        public bool HasOutputAddition;
    }

    public sealed class ArenaShowFightResult : UIBase
    {
        [SerializeField] private GameObject mRankChangeGo;
        [SerializeField] private WorldMultiLanguageText mPreRankTxt, mNowRankTxt;
        [SerializeField] private WorldMultiLanguageText[] mAwardCoinTxts;
        [SerializeField] private WorldStateSwitcher<bool> mHasOutputAddition;
        private ArenaFightResult result;
        public void Init(ArenaFightResult value) => InitWrapper(() =>
            result = value ?? throw new ArgumentNullException(nameof(value)));
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            int change = result.PreviousRank - result.CurrentRank;
            mRankChangeGo.SetActive(change > 0);
            if (change > 0)
            {
                mPreRankTxt.SetKey("{0}", result.PreviousRank);
                mNowRankTxt.SetKey("{0}", result.CurrentRank);
            }
            mHasOutputAddition.SetState(result.HasOutputAddition);
            foreach (var text in mAwardCoinTxts) text.Format(result.AwardCount);
        }
    }
}
