using System;
using System.Collections;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.BattleResultUI
{
    // Official GameTimeoutUI. Only logging and shared UI type names are adapted.
    public sealed class GameTimeoutUI : MonoBehaviour
    {
        public enum EStatus { None, Normal, Warn }
        public GameObject Content;
        public WorldStateSwitcher<EStatus> StateSwitcher;
        public WorldMultiLanguageText TipsText;
        public WorldMultiLanguageText CountdownText;
        private float mRemainTime, mShowThreshold, mRefreshInterval, mWarnThreshold;
        private string mTipKey;
        private Coroutine mCoro;
        private WaitForSecondsRealtime mWaitRemainShow, mWaitRefreshInterval;
        public event Action OnTimeOut;

        public void Play(float remainTime, string tipKey = "", float showThreshold = float.MaxValue,
            float refreshInterval = 1f, float warnThreshold = -1f)
        {
            if (showThreshold < 0f)
            {
                Debug.LogWarning("[Fight] GameTimeoutUI Init showThreshold is less then 0.", this);
                showThreshold = float.MaxValue;
            }
            if (refreshInterval < 0f)
            {
                Debug.LogWarning("[Fight] GameTimeoutUI Init refreshInterval is less then 0.", this);
                refreshInterval = 1f;
            }
            if (warnThreshold > showThreshold)
            {
                Debug.LogWarning("[Fight] GameTimeoutUI Init warnThreshold is more then showThreshold. Already reset warnThreshold to -1.", this);
                warnThreshold = -1f;
            }
            mRemainTime = remainTime; mTipKey = tipKey; mShowThreshold = showThreshold;
            mRefreshInterval = refreshInterval; mWarnThreshold = warnThreshold;
            if (TipsText && !string.IsNullOrEmpty(mTipKey)) TipsText.SetKey(mTipKey);
            StopCoroutineTime();
            mCoro = StartCoroutine(RefreshTimeout());
        }

        public void Stop() { StateSwitcher.SetState(EStatus.None); StopCoroutineTime(); }
        private void Awake() => StateSwitcher.SetState(EStatus.None);
        private void OnDisable() => StopCoroutineTime();
        private IEnumerator RefreshTimeout()
        {
            float remainTime = mRemainTime;
            var waitRemainShow = GetWaitRemainShow(mRemainTime - mShowThreshold);
            if (waitRemainShow != null)
            {
                StateSwitcher.SetState(EStatus.None);
                Content.SetActive(false);
                yield return waitRemainShow;
                remainTime -= waitRemainShow.waitTime;
            }
            Content.SetActive(true);
            while (remainTime > 0f)
            {
                var refreshStatus = GetRefreshStatus(remainTime);
                if (StateSwitcher.State != refreshStatus) StateSwitcher.SetState(refreshStatus);
                CountdownText.Format(Mathf.RoundToInt(remainTime).ToString());
                yield return GetWaitRefreshInterval(mRefreshInterval);
                remainTime -= mRefreshInterval;
            }
            StopCoroutineTime();
            OnTimeOut?.Invoke();
        }
        private void StopCoroutineTime()
        {
            if (mCoro == null) return;
            StopCoroutine(mCoro); mCoro = null;
        }
        private WaitForSecondsRealtime GetWaitRemainShow(float remainShowTime)
        {
            if (remainShowTime < 0f) return null;
            if (mWaitRemainShow == null || Math.Abs(mWaitRemainShow.waitTime - remainShowTime) > 1E-05f)
                mWaitRemainShow = new WaitForSecondsRealtime(remainShowTime);
            return mWaitRemainShow;
        }
        private WaitForSecondsRealtime GetWaitRefreshInterval(float interval)
        {
            if (mWaitRefreshInterval == null || Math.Abs(mWaitRefreshInterval.waitTime - interval) > 1E-05f)
                mWaitRefreshInterval = new WaitForSecondsRealtime(interval);
            return mWaitRefreshInterval;
        }
        private EStatus GetRefreshStatus(float remainTime) => mWarnThreshold > 0f && remainTime <= mWarnThreshold
            ? EStatus.Warn : EStatus.Normal;
    }
}
