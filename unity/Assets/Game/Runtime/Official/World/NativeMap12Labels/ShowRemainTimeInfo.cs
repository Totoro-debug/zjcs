using System;
using System.Collections;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI;

namespace Zjcs.Official.World.NativeMap12Labels
{
    // Original ShowRemainTimeInfo. Map12's table scope never initializes this timer.
    public class ShowRemainTimeInfo : UIBase
    {
        [SerializeField] public MultiLanguageText mRemainTimeText;
        private DateTime mEndTime;
        private float mRefreshInterval;
        private WaitForSeconds mWaitForSeconds;
        private string mCustomKey;
        private int mFormat;
        private Action mCallBack;
        private bool mStop;
        public void Init(DateTime endTime, float refreshInterval = 1f, string customKey = null, int timeSpanFormat = 0, Action callBack = null)
        {
            InitWrapper(() => { mEndTime = endTime; mRefreshInterval = refreshInterval; mStop = false; mCustomKey = customKey; mFormat = timeSpanFormat; mCallBack = callBack; });
        }
        public void StartTimer() { StopAllCoroutines(); if (isActiveAndEnabled) StartCoroutine(StartTimerImpl()); }
        private IEnumerator StartTimerImpl()
        {
            if (mWaitForSeconds == null) mWaitForSeconds = new WaitForSeconds(mRefreshInterval);
            while (!(Services.Now > mEndTime))
            {
                int num = Mathf.FloorToInt((float)Services.CalcRemainSecond(mEndTime));
                RefreshText(TimeSpan.FromSeconds(num));
                yield return mWaitForSeconds;
            }
            RefreshText(new TimeSpan(0L)); mCallBack?.Invoke();
        }
        private void RefreshText(TimeSpan timeSpan) { if (!mStop) RefrshTimeText(timeSpan); }
        private void RefrshTimeText(TimeSpan timeSpan)
        {
            object arg = Services.FormatTimeSpan(timeSpan, mFormat);
            if (mCustomKey != null) mRemainTimeText.SetKey(mCustomKey, arg); else mRemainTimeText.Format(arg);
        }
        public void SetCustomText(string textKey, params object[] args) { Stop(); mRemainTimeText.gameObject.SetActive(true); mRemainTimeText.SetKey(textKey, args); }
        public void Stop(bool inactiveText = true) { mStop = true; StopAllCoroutines(); if (inactiveText) mRemainTimeText.gameObject.SetActive(false); }
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited(); Services.Player.OnDebugTimeOffset += OnDebugTimeOffset;
            if (!mStop) mRemainTimeText.gameObject.SetActive(true);
            if (Services.Now >= mEndTime) RefreshText(new TimeSpan(0L)); else StartTimer();
        }
        protected override void OnDisableAndInited()
        {
            base.OnDisableAndInited(); StopAllCoroutines();
            if (Services.Player != null) Services.Player.OnDebugTimeOffset -= OnDebugTimeOffset;
        }
        private void OnDebugTimeOffset(long tick) { if (!mStop) StartTimer(); }
    }
}
