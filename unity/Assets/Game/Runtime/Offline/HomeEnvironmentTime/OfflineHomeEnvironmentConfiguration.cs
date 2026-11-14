using System;
using UnityEngine;
using Zjcs.Official.HomeEnvironmentTime;

namespace Zjcs.Offline.HomeEnvironmentTime
{
    public static class OfflineHomeEnvironmentConfiguration
    {
        [Serializable] sealed class Payload { public string manifest; public Configuration configuration; }
        [Serializable] sealed class Configuration
        {
            public float HomeTimeScale;
            public int SecondsPerMinute, MinutesPerHour, HoursPerDay, DaysPerMonth, MonthsPerYear;
            public int FastCycleDays;
            public float FastCycleDaySeconds;
            public Curve FastCycleCurve;
            public HomeDayTime RefreshDayTime, EffectLightOnTime, EffectLightOffTime;
            public Meteor MeteorConfig;
        }
        [Serializable] sealed class Meteor
        {
            public HomeDayTime BeginTime, EndTime;
            public float Probability;
            public int Interval;
            public Template[] Templates;
        }
        [Serializable] sealed class Template { public Asset mProxyRes; }
        [Serializable] sealed class Asset { public string mAssetGuid; }
        [Serializable] sealed class Curve { public Key[] m_Curve; public int m_PreInfinity, m_PostInfinity; }
        [Serializable] sealed class Key
        {
            public float time, value, inSlope, outSlope, inWeight, outWeight;
            public int weightedMode;
        }

        public static HomeEnvironmentConfig Load()
        {
            var asset = Resources.Load<TextAsset>("Home/home-environment-time");
            if (!asset) throw new InvalidOperationException("Selected original home environment configuration is missing.");
            var payload = JsonUtility.FromJson<Payload>(asset.text);
            var data = payload?.configuration;
            if (payload?.manifest != "884_156692" || data == null || data.HomeTimeScale <= 0
                || data.SecondsPerMinute <= 0 || data.MinutesPerHour <= 0 || data.HoursPerDay <= 0
                || data.DaysPerMonth <= 0 || data.MonthsPerYear <= 0 || data.FastCycleDays <= 0
                || data.FastCycleDaySeconds <= 0 || data.FastCycleCurve?.m_Curve == null
                || data.FastCycleCurve.m_Curve.Length == 0 || data.MeteorConfig?.Templates == null
                || data.MeteorConfig.Templates.Length == 0)
                throw new InvalidOperationException("Selected original home environment configuration is incomplete.");
            var keys = new Keyframe[data.FastCycleCurve.m_Curve.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                var key = data.FastCycleCurve.m_Curve[i];
                keys[i] = new Keyframe(key.time, key.value, key.inSlope, key.outSlope, key.inWeight, key.outWeight)
                    { weightedMode = (WeightedMode)key.weightedMode };
            }
            var curve = new AnimationCurve(keys) {
                preWrapMode = (WrapMode)data.FastCycleCurve.m_PreInfinity,
                postWrapMode = (WrapMode)data.FastCycleCurve.m_PostInfinity
            };
            var templates = new string[data.MeteorConfig.Templates.Length];
            for (int i = 0; i < templates.Length; i++) templates[i] = data.MeteorConfig.Templates[i].mProxyRes.mAssetGuid;
            return new HomeEnvironmentConfig {
                HomeTimeScale = data.HomeTimeScale, HomeTimeScaleRuntime = data.HomeTimeScale,
                SecondsPerMinute = data.SecondsPerMinute, MinutesPerHour = data.MinutesPerHour,
                HoursPerDay = data.HoursPerDay, DaysPerMonth = data.DaysPerMonth, MonthsPerYear = data.MonthsPerYear,
                FastCycleDays = data.FastCycleDays, FastCycleDaySeconds = data.FastCycleDaySeconds,
                FastCycleCurve = curve, RefreshDayTime = data.RefreshDayTime,
                EffectLightOnTime = data.EffectLightOnTime, EffectLightOffTime = data.EffectLightOffTime,
                MeteorConfig = new HomeMeteorConfig {
                    BeginTime = data.MeteorConfig.BeginTime, EndTime = data.MeteorConfig.EndTime,
                    Probability = data.MeteorConfig.Probability, Interval = data.MeteorConfig.Interval, Templates = templates
                }
            };
        }
    }
}
