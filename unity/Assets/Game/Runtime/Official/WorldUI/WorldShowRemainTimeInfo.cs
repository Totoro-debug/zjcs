using System;
using System.Collections;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // ShowRemainTimeInfo with a local injectable clock. Online debug-offset
    // subscription is replaced by RefreshClock, called by a host changing time.
    public sealed class WorldShowRemainTimeInfo : WorldInitializedUI
    {
        public WorldMultiLanguageText mRemainTimeText;
        [NonSerialized] public Func<DateTime> Clock = () => DateTime.Now;
        private DateTime endTime;
        private float refreshInterval;
        private WaitForSeconds wait;
        private string customKey;
        private WorldTimeSpanFormatType format;
        private Action callback;
        private bool stopped;

        public void Init(DateTime end, float interval = 1f, string key = null,
            WorldTimeSpanFormatType timeFormat = WorldTimeSpanFormatType.Normal, Action onEnd = null)
            => InitWrapper(() =>
            {
                endTime = end;
                refreshInterval = interval;
                stopped = false;
                customKey = key;
                format = timeFormat;
                callback = onEnd;
            });

        public void StartTimer()
        {
            StopAllCoroutines();
            if (isActiveAndEnabled) StartCoroutine(StartTimerImpl());
        }

        private IEnumerator StartTimerImpl()
        {
            if (wait == null) wait = new WaitForSeconds(refreshInterval);
            while (!(Clock() > endTime))
            {
                int seconds = Mathf.FloorToInt((float)Math.Max(0.0, (endTime - Clock()).TotalSeconds));
                RefreshText(TimeSpan.FromSeconds(seconds));
                yield return wait;
            }
            RefreshText(new TimeSpan(0L));
            callback?.Invoke();
        }

        private void RefreshText(TimeSpan timeSpan)
        {
            if (stopped) return;
            object arg = new WorldCultureTimeSpanArg(timeSpan, format, mRemainTimeText.Localizer);
            if (customKey != null) mRemainTimeText.SetKey(customKey, arg);
            else mRemainTimeText.Format(arg);
        }

        public void SetCustomText(string key, params object[] args)
        {
            Stop();
            mRemainTimeText.gameObject.SetActive(true);
            mRemainTimeText.SetKey(key, args);
        }

        public void Stop(bool inactiveText = true)
        {
            stopped = true;
            StopAllCoroutines();
            if (inactiveText) mRemainTimeText.gameObject.SetActive(false);
        }

        protected override void OnEnableAndInited()
        {
            if (!stopped) mRemainTimeText.gameObject.SetActive(true);
            if (Clock() >= endTime) RefreshText(new TimeSpan(0L));
            else StartTimer();
        }

        protected override void OnDisableAndInited() => StopAllCoroutines();
        public void RefreshClock() { if (!stopped) StartTimer(); }
    }
}
