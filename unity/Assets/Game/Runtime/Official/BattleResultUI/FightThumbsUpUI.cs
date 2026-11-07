using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.BattleResultUI
{
    public enum BattleThumbsUpRelative { None, Each, MeToOther, OtherToMe }
    public interface IFightThumbsUpUIHost
    {
        long MainPlayerId { get; }
        bool IsLimited(int stageType);
        int MyCount(int stageType);
        BattleThumbsUpRelative OtherRelative(int stageType, long playerId);
        bool AlreadyReported(long gameId, long playerId);
        event Action<int> CountChanged;
        event Action<long> Reported;
        void Request(int stageType, long gameId, long target, Action success, Action failed);
        void OpenReport(long target, string name, long gameId);
        void SetThumbsUpToTeam(long target);
    }

    // Selected FightThumbsUpUI. R3/RPC subscriptions cross an explicit host;
    // the local host uses the confirmed unsupported-type/limit branch.
    public sealed class FightThumbsUpUI : UIBase
    {
        public enum EFightThumbsUpStateType { None, CanThumbsUp, EachThumbsUped, SelfThumbsUped, CanReport }
        [SerializeField] private WorldStateSwitcher<EFightThumbsUpStateType> mStateSwitcher;
        [SerializeField] private Button mThumbUpBtn, mThumbUpReportSwitchBtn;
        [SerializeField] private TMP_Text mSelfThumbUpedText;
        [SerializeField] private Animation mCanThumbUpAnim, mSelfThumbUpAnim;
        private IFightThumbsUpUIHost host;
        private int stageType;
        private long gameId, playerId;
        private bool isPlayer, hasThumbsUped, reportMode, hasReported;
        private string playerName;
        public void Bind(IFightThumbsUpUIHost value) => host = value ?? throw new ArgumentNullException(nameof(value));
        public void SetData(int type, long game, long player, bool validPlayer, string name = null)
            => InitWrapper(() =>
            {
                if (gameId != game || playerId != player) hasReported = false;
                stageType = type; gameId = game; playerId = player; isPlayer = validPlayer; playerName = name;
                if (host.AlreadyReported(gameId, playerId)) hasReported = true;
            });
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            mThumbUpBtn.onClick.AddListener(OnThumbsUpBtn);
            mThumbUpReportSwitchBtn.onClick.AddListener(OnReportBtn);
            host.CountChanged += OnMyCountChanged;
            host.Reported += OnReported;
            RefreshThumbsUpDisplay();
        }
        protected override void OnDisableAndInited()
        {
            mThumbUpBtn.onClick.RemoveListener(OnThumbsUpBtn);
            mThumbUpReportSwitchBtn.onClick.RemoveListener(OnReportBtn);
            host.CountChanged -= OnMyCountChanged;
            host.Reported -= OnReported;
            base.OnDisableAndInited();
        }
        public void RefreshCanReport(bool canReport)
        {
            if (canReport && (hasReported || host.AlreadyReported(gameId, playerId)))
            {
                hasReported = true; reportMode = false;
                mStateSwitcher.SetState(EFightThumbsUpStateType.None);
                return;
            }
            reportMode = canReport;
            if (canReport) mStateSwitcher.SetState(EFightThumbsUpStateType.CanReport);
            else RefreshThumbsUpDisplay();
        }
        private void RefreshThumbsUpDisplay()
        {
            if (!isPlayer || host.IsLimited(stageType)) mStateSwitcher.SetState(EFightThumbsUpStateType.None);
            else if (playerId == host.MainPlayerId)
            {
                int count = host.MyCount(stageType);
                mSelfThumbUpedText.text = count.ToString();
                mStateSwitcher.SetState(count > 0 ? EFightThumbsUpStateType.SelfThumbsUped : EFightThumbsUpStateType.None);
            }
            else RefreshOther();
        }
        private void RefreshOther()
        {
            switch (host.OtherRelative(stageType, playerId))
            {
                case BattleThumbsUpRelative.Each: mStateSwitcher.SetState(EFightThumbsUpStateType.EachThumbsUped); break;
                case BattleThumbsUpRelative.MeToOther: mStateSwitcher.SetState(EFightThumbsUpStateType.None); break;
                case BattleThumbsUpRelative.OtherToMe:
                case BattleThumbsUpRelative.None: mStateSwitcher.SetState(EFightThumbsUpStateType.CanThumbsUp); break;
            }
        }
        private void OnThumbsUpBtn()
        {
            if (!isPlayer || playerId == 0 || playerId == host.MainPlayerId || host.IsLimited(stageType)
                || mStateSwitcher.State != EFightThumbsUpStateType.CanThumbsUp || hasThumbsUped) return;
            hasThumbsUped = true;
            host.Request(stageType, gameId, playerId, () =>
            {
                host.SetThumbsUpToTeam(playerId);
                StartCoroutine(OnOtherSuccess());
            }, () => hasThumbsUped = false);
        }
        private IEnumerator OnOtherSuccess()
        {
            mCanThumbUpAnim.Play();
            yield return new WaitForSeconds(mCanThumbUpAnim.clip.length);
            if (!reportMode) RefreshOther();
        }
        private void OnReportBtn()
        {
            if (reportMode && !hasReported && !host.AlreadyReported(gameId, playerId)
                && isPlayer && playerId != 0 && playerId != host.MainPlayerId)
                host.OpenReport(playerId, playerName, gameId);
        }
        private void OnReported(long target)
        {
            if (target != playerId) return;
            hasReported = true; reportMode = false;
            mStateSwitcher.SetState(EFightThumbsUpStateType.None);
        }
        private void OnMyCountChanged(int count)
        {
            if (playerId == host.MainPlayerId)
            {
                mStateSwitcher.SetState(EFightThumbsUpStateType.SelfThumbsUped);
                mSelfThumbUpedText.text = count.ToString();
                mSelfThumbUpAnim.Play();
            }
            else if (host.OtherRelative(stageType, playerId) == BattleThumbsUpRelative.Each)
                mStateSwitcher.SetState(EFightThumbsUpStateType.EachThumbsUped);
        }
    }
}
