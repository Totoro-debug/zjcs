using UnityEngine;

namespace Zjcs.Official.HomeEnvironmentTime
{
    // Projection of the original config fields consumed by the time handlers.
    // Values and the complete curve are loaded from the selected official asset.
    public sealed class HomeEnvironmentConfig
    {
        public float HomeTimeScale;
        public float HomeTimeScaleRuntime;
        public int SecondsPerMinute;
        public int MinutesPerHour;
        public int HoursPerDay;
        public int DaysPerMonth;
        public int MonthsPerYear;
        public int FastCycleDays;
        public float FastCycleDaySeconds;
        public AnimationCurve FastCycleCurve;
        public HomeDayTime RefreshDayTime;
        public HomeDayTime EffectLightOnTime;
        public HomeDayTime EffectLightOffTime;
        public HomeMeteorConfig MeteorConfig;
        public int SecondsPerDay => SecondsPerMinute * MinutesPerHour * HoursPerDay;
        public int RefreshDaySecondsSinceStartOfDay => RefreshDayTime.GetSecondsSinceStartOfDay(this);
    }

    public sealed class HomeMeteorConfig
    {
        public HomeDayTime BeginTime, EndTime;
        public float Probability;
        public int Interval;
        public string[] Templates;

        public bool InActiveTime(HomeDayTime homeDayTime)
        {
            if (EndTime > BeginTime)
            {
                if (homeDayTime >= BeginTime) return homeDayTime < EndTime;
                return false;
            }
            if (!(homeDayTime >= BeginTime)) return homeDayTime < EndTime;
            return true;
        }
    }

    public sealed class HomeEnvironmentConfigValue
    {
        public HomeEnvironmentConfig Value { get; }
        public HomeEnvironmentConfigValue(HomeEnvironmentConfig value) { Value = value; }
    }

    // Explicit ports for the original handler's config and natural clock.
    public interface IHomeEnvironmentManager
    {
        HomeEnvironmentConfigValue Config { get; }
        HomeDateTime HomeDateTime { get; }
        HomeDayTime HomeDayTime { get; }
        float DeltaTime { get; }
    }
}
