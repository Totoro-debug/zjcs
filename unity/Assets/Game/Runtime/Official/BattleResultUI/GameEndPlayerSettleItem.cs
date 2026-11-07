using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Official.BattleResultUI
{
    // Selected GameEndPlayerSettleItem, with local target/lite-info providers.
    public sealed class GameEndPlayerSettleItem : UIBase
    {
        [SerializeField] private WorldMultiLanguageText mDamageValue, mDamageMyValue;
        [SerializeField] private Image mDamageSlider;
        [SerializeField] private WorldStateSwitcher<bool> mDamageCorpSwitcher, mDamageRecordSwitcher;
        [SerializeField] private TeamPlayerIcon mPlayerIcon;
        [SerializeField] private WorldStateSwitcher<bool> mPlayerNameStateSwitcher, mInfoDownStateSwitcher;
        [SerializeField] private Animation mAnimation;
        [SerializeField] private FightThumbsUpUI mFightThumbsUpUI;
        private IBattleBossResultHost host;
        private IProfessionInfoTarget target;
        private long damage, gameId;
        private float damagePercent;
        private int stageType;
        private bool needCross;
        public long PlayerId => target?.TargetId ?? 0;
        public bool IsNpc => target?.IsNpc ?? false;
        public void Bind(IBattleBossResultHost value)
        {
            host = value ?? throw new ArgumentNullException(nameof(value));
            mFightThumbsUpUI.Bind(value.ThumbsUp);
        }
        public void Init(int type, long id, IProfessionInfoTarget player, long value,
            float percent, bool cross) => InitWrapper(() =>
        {
            stageType = type; gameId = id; target = player;
            damage = value; damagePercent = percent; needCross = cross;
        });
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            bool self = target.TargetId == host.MainPlayerId;
            mPlayerIcon.Init(target);
            mPlayerNameStateSwitcher.SetState(self);
            var compact = CultureNumberArg.Compact(damage);
            mDamageValue.Format(compact);
            mDamageMyValue.Format(compact);
            mDamageSlider.fillAmount = damagePercent;
            mDamageCorpSwitcher.SetState(self);
            mInfoDownStateSwitcher.SetState(needCross);
            mFightThumbsUpUI.SetData(stageType, gameId, PlayerId, !IsNpc, host.PlayerName(target));
            if (mAnimation != null) mAnimation.Play();
        }
        public void OnThumbUpReportSwitch(bool showing) => mFightThumbsUpUI.RefreshCanReport(!showing);
    }
}
