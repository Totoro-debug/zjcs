using System;

namespace Zjcs.Official.HomeEnvironmentTime
{
    // Time-only extraction of HomeEnvironmentManage.InitTime/Update and
    // InitStageHandler. Server time and frame sampling are explicit inputs;
    // actor commands and global subscriptions remain with their feature owners.
    public sealed class HomeEnvironmentTimeState : IHomeEnvironmentManager, IDisposable
    {
        HomeDayTime mHomeDayTime;
        int mLastTotalDays;
        double mSecondsSinceStartOfDay;

        public HomeEnvironmentConfigValue Config { get; }
        public HomeDateTime HomeDateTime { get; }
        public HomeDayTime HomeDayTime => mHomeDayTime;
        public HomeDayTime StageHomeDayTime { get; private set; }
        public float DeltaTime { get; private set; }
        public float StageDeltaTime { get; private set; }
        public float PlayableDeltaTime { get; private set; }
        public float PlayableTimeScale { get; private set; }
        public float HomeRuntimeSeconds { get; private set; }
        public bool IsPlayableDay { get; private set; }
        public HomeDayStageHandleProxy StageHandleProxy { get; } = new HomeDayStageHandleProxy();
        public IHomeClock HomeClock { get; } = new HomeClock();
        public Action OnRefreshDay { get; set; }
        public event Action<bool> OnDayNightChange;

        public HomeEnvironmentTimeState(HomeEnvironmentConfig configuration, double totalSeconds,
            IHomeNaturalTimeLimit naturalTimeLimit = null)
        {
            Config = new HomeEnvironmentConfigValue(configuration ?? throw new ArgumentNullException(nameof(configuration)));
            var value = Config.Value;
            HomeDateTime = new HomeDateTime(value.SecondsPerMinute, value.MinutesPerHour,
                value.HoursPerDay, value.DaysPerMonth, value.MonthsPerYear, totalSeconds);
            mLastTotalDays = HomeDateTime.TotalDays;
            mHomeDayTime = HomeDateTime.GetHomeDayTime();
            HomeClock.Init(mHomeDayTime);
            StageHandleProxy.AddHandler(new NaturalDayStageHandler(this));
            if (naturalTimeLimit != null && naturalTimeLimit.IsLimited)
                StageHandleProxy.AddHandler(new AllDayStageHandler(naturalTimeLimit));
            StageHomeDayTime = StageHandleProxy.HomeDayTime;
            UpdateDayNight(StageHomeDayTime);
        }

        public void Update(float unscaledDeltaTime)
        {
            HomeEnvironmentConfig value = Config.Value;
            float num = (DeltaTime = unscaledDeltaTime * value.HomeTimeScaleRuntime);
            HomeDateTime.Update(num);
            mHomeDayTime = HomeDateTime.GetHomeDayTime();
            int secondsSinceStartOfDay = mHomeDayTime.GetSecondsSinceStartOfDay(value.SecondsPerMinute, value.MinutesPerHour);
            int totalDays = HomeDateTime.TotalDays;
            if ((mSecondsSinceStartOfDay < value.RefreshDaySecondsSinceStartOfDay
                    && secondsSinceStartOfDay >= value.RefreshDaySecondsSinceStartOfDay)
                || mLastTotalDays - totalDays > 1)
                OnRefreshDay?.Invoke();
            mSecondsSinceStartOfDay = secondsSinceStartOfDay;
            mLastTotalDays = totalDays;
            StageHandleProxy.Update();
            HomeClock.Update(mHomeDayTime);
            float num3 = StageHandleProxy.DeltaTime;
            if (num3 < 0f) num3 = num;
            StageDeltaTime = num3;
            float num4 = num3 / value.HomeTimeScale;
            if (num4 > value.SecondsPerDay) num4 %= value.SecondsPerDay;
            PlayableDeltaTime = num4;
            PlayableTimeScale = PlayableDeltaTime / unscaledDeltaTime;
            HomeDayTime homeDayTime = StageHandleProxy.HomeDayTime;
            if (!StageHandleProxy.HasHandler) homeDayTime = mHomeDayTime;
            HomeRuntimeSeconds += num;
            StageHomeDayTime = homeDayTime;
            UpdateDayNight(homeDayTime);
        }

        void UpdateDayNight(HomeDayTime homeDayTime)
        {
            var value = Config.Value;
            bool flag = homeDayTime >= value.EffectLightOffTime && homeDayTime < value.EffectLightOnTime;
            if (flag == IsPlayableDay) return;
            IsPlayableDay = flag;
            OnDayNightChange?.Invoke(flag);
        }

        public void FastHangUp()
        {
            var value = Config.Value;
            StageHandleProxy.AddHandler(new FastCycleDayStageHandler(this, value.FastCycleDays, value.FastCycleDaySeconds));
        }

        public void Dispose() => StageHandleProxy.Clear();
    }
}
