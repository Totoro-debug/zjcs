using System;
using System.Collections;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionTimeService
    {
        DateTime Now { get; }
        double CalcRemainSecond(DateTime endTime);
        object TimeSpanArgument(TimeSpan span, int format);
        event Action<long> DebugTimeOffset;
    }
    // Original ShowRemainTimeInfo; clock and culture are local service boundaries.
    public sealed class ShowRemainTimeInfo : MonoBehaviour
    {
        [SerializeField] public WorldMultiLanguageText mRemainTimeText;
        private DateTime mEndTime;
        private float mRefreshInterval;
        private WaitForSeconds mWaitForSeconds;
        private string mCustomKey;
        private int mFormat;
        private Action mCallBack;
        private bool mStop, initialized, subscribed;
        private IProfessionTimeService time;
        public void Bind(IProfessionTimeService value) { time = value ?? throw new ArgumentNullException(nameof(value)); }
        public void Init(DateTime endTime, float refreshInterval = 1f, string customKey = null,
            int timeSpanFormat = 0, Action callBack = null)
        {
            if (time == null) throw new InvalidOperationException("Profession clock is not bound.");
            mEndTime = endTime;
            mRefreshInterval = refreshInterval;
            mStop = false;
            mCustomKey = customKey;
            mFormat = timeSpanFormat;
            mCallBack = callBack;
            initialized = true;
            if (isActiveAndEnabled) Activate();
        }
        public void StartTimer()
        {
            StopAllCoroutines();
            if (isActiveAndEnabled) StartCoroutine(StartTimerImpl());
        }
        private IEnumerator StartTimerImpl()
        {
            if (mWaitForSeconds == null) mWaitForSeconds = new WaitForSeconds(mRefreshInterval);
            while (!(time.Now > mEndTime))
            {
                int seconds = Mathf.FloorToInt((float)time.CalcRemainSecond(mEndTime));
                RefreshText(TimeSpan.FromSeconds(seconds));
                yield return mWaitForSeconds;
            }
            RefreshText(new TimeSpan(0L));
            mCallBack?.Invoke();
        }
        private void RefreshText(TimeSpan span)
        {
            if (mStop) return;
            object argument = time.TimeSpanArgument(span, mFormat);
            if (mCustomKey != null) mRemainTimeText.SetKey(mCustomKey, argument);
            else mRemainTimeText.Format(argument);
        }
        public void SetCustomText(string textKey, params object[] args)
        {
            Stop();
            mRemainTimeText.gameObject.SetActive(true);
            mRemainTimeText.SetKey(textKey, args);
        }
        public void Stop(bool inactiveText = true)
        {
            mStop = true;
            StopAllCoroutines();
            if (inactiveText) mRemainTimeText.gameObject.SetActive(false);
        }
        private void OnEnable() { if (initialized) Activate(); }
        private void Activate()
        {
            if (!subscribed) { time.DebugTimeOffset += OnDebugTimeOffset; subscribed = true; }
            if (!mStop) mRemainTimeText.gameObject.SetActive(true);
            if (time.Now >= mEndTime) RefreshText(new TimeSpan(0L));
            else StartTimer();
        }
        private void OnDisable()
        {
            StopAllCoroutines();
            if (subscribed) { time.DebugTimeOffset -= OnDebugTimeOffset; subscribed = false; }
        }
        private void OnDebugTimeOffset(long tick) { if (!mStop) StartTimer(); }
    }
}
