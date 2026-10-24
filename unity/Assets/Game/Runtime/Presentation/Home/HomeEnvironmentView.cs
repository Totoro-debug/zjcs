using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    [Serializable]
    public sealed class HomeMoment
    {
        public int hour;
        public int minute;
        public int second;
        public int Seconds => hour * 3600 + minute * 60 + second;
    }

    [Serializable]
    public sealed class HomeDayStage
    {
        public int stage;
        public HomeMoment time;
    }

    [Serializable]
    public sealed class HomeLightState
    {
        public string name;
        public bool on;
    }

    [Serializable]
    public sealed class HomeLightTime
    {
        public HomeMoment begin;
        public HomeLightState[] lights;
    }

    [Serializable]
    public sealed class HomeMoonConfig
    {
        public int DayDuration;
        public int DayCycle;
    }

    [Serializable]
    public sealed class HomeMeteorConfig
    {
        public HomeMoment begin;
        public HomeMoment end;
        public float probability;
        public int interval;
    }

    [Serializable]
    public sealed class HomeEnvironmentConfig
    {
        public float timeScale;
        public int secondsPerMinute;
        public int minutesPerHour;
        public int hoursPerDay;
        public int daysPerMonth;
        public int monthsPerYear;
        public HomeMoment refreshDay;
        public HomeDayStage[] stages;
        public HomeLightTime[] lightTimes;
        public HomeMoment effectLightOn;
        public HomeMoment effectLightOff;
        public HomeMoonConfig moon;
        public HomeMeteorConfig meteor;
    }

    [Serializable]
    public sealed class HomeMoonVariant
    {
        public int index;
        public string sprite;
    }

    [Serializable]
    public sealed class HomeEnvironmentData
    {
        public string version;
        public HomeEnvironmentConfig config;
        public HomeMeteorRuntimePayload meteorRuntime;
        public HomeMoonVariant[] moonVariants;
    }

    [Serializable]
    public sealed class HomeMoonVisualData
    {
        public string sourceManifest;
        public string sourceMoonJsonSha256;
        public string rootPath;
        public float[] rootPosition;
        public float[] scalePosition;
        public float[] scaleBasePosition;
        public float duration;
        public bool loop;
        public HomeMoonLayerData[] layers;
        public HomeMoonAnimationTrack[] tracks;
    }

    [Serializable]
    public sealed class HomeMoonLayerData
    {
        public int phase;
        public string kind;
        public string name;
        public string file;
        public float[] vertices;
        public float[] color;
        public float[] scale;
        public int sortingLayer;
        public int sortingOrder;
    }

    [Serializable]
    public sealed class HomeMoonAnimationTrack
    {
        public string path;
        public string property;
        public bool stepped;
        public HomeMoonAnimationKey[] keys;
    }

    [Serializable]
    public sealed class HomeMoonAnimationKey
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;
    }

    /// <summary>
    /// Port of ``HomeEnvironmentManage``/``HomeClock`` for the offline home:
    /// the day advances at ``HomeTimeScale``, the day stage and the light groups
    /// follow ``HomeLightTimeConfigs``, and night effects switch at
    /// ``EffectLightOnTime``/``EffectLightOffTime``.  The official Moon renderer
    /// and ``Night_Common`` curve drive the phase and night visibility.
    /// </summary>
    public sealed class HomeEnvironmentView : IDisposable
    {
        private static readonly Dictionary<string, string> LightPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "DoorLight", "/HouseOutside/HouseOutSideLight/Door_Light" },
            { "LobbyLight", "/House/HouseLight/Lobby_Light" },
            { "BedRoomLight", "/House/HouseLight/Bedroom_Light" },
            { "StudyLight", "/House/HouseLight/Study_Light" },
            { "DeskLight", "/Desk/Desk_Light" },
            { "DanluLight", "/Danlu/danlu_repaired/Danlu_Light" },
            { "DiningTableLight", "/DiningTable/HomeDiningTable_Light" }
        };

        private readonly HomeSceneView scene;
        private readonly HomeEnvironmentConfig config;
        private readonly HomeMeteorRuntimePayload meteorRuntime;
        private readonly HomeMoonVariant[] moonVariants;
        private readonly HomeMoonVisualData moonVisuals;
        private readonly HomeSkyTimelineView daySky;
        private double daySeconds;
        private int appliedStage = -1;
        private int appliedLightTimeIndex = -1;
        private bool appliedNight;
        private int appliedMoon;
        private double nextMeteorHomeSeconds;
        private bool meteorActive;
        private bool hasMeteorSchedule;
        private long dayIndex;

        public int Stage { get; private set; }
        public int Hour => (int)(daySeconds / 3600) % 24;
        public int Minute => (int)(daySeconds / 60) % 60;
        public bool IsNight => NightAt(daySeconds);
        public int MoonPhase { get; private set; }
        public HomeEnvironmentConfig Config => config;
        public HomeMeteorRuntimePayload MeteorRuntime => meteorRuntime;

        public HomeEnvironmentView(HomeSceneView scene, long nowUnixSeconds, long homeStartUnixSeconds = 0)
        {
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
            string path = Path.Combine(Application.streamingAssetsPath, HomeAssetCatalog.FolderName, "environment.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Home environment payload is missing.", path);
            HomeEnvironmentData data = JsonUtility.FromJson<HomeEnvironmentData>(File.ReadAllText(path));
            if (data == null || data.config == null || data.config.stages == null || data.config.stages.Length == 0)
                throw new InvalidDataException("The official home environment config is incomplete.");
            config = data.config;
            meteorRuntime = data.meteorRuntime;
            if (config.meteor != null && config.meteor.interval > 0
                && (meteorRuntime == null || meteorRuntime.templates == null || meteorRuntime.templates.Length == 0))
                throw new InvalidDataException("Official Home meteor runtime templates are missing.");
            moonVariants = data.moonVariants ?? Array.Empty<HomeMoonVariant>();
            long now = Math.Max(0, nowUnixSeconds);
            long start = homeStartUnixSeconds > 0 ? homeStartUnixSeconds : now;
            double elapsedHomeSeconds = Math.Max(0.0, (double)now - start)
                * Math.Max(1.0, config.timeScale);
            dayIndex = (long)Math.Floor(elapsedHomeSeconds / SecondsPerDay);
            daySeconds = elapsedHomeSeconds - dayIndex * SecondsPerDay;
            string moonPath = Path.Combine(Application.streamingAssetsPath, HomeAssetCatalog.FolderName,
                "environment-visuals", "moon-visuals.json");
            if (!File.Exists(moonPath)) throw new FileNotFoundException("Official home moon visual payload is missing.", moonPath);
            moonVisuals = JsonUtility.FromJson<HomeMoonVisualData>(File.ReadAllText(moonPath));
            if (moonVisuals == null || moonVisuals.layers == null || moonVisuals.tracks == null)
                throw new InvalidDataException("The official home moon visual payload is incomplete.");
            daySky = HomeSkyTimelineView.CreateIfSupported(this.scene);
            Apply(true);
            UpdateMeteor();
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (unscaledDeltaSeconds <= 0) return;
            double elapsedHomeSeconds = daySeconds + unscaledDeltaSeconds * Math.Max(1.0, config.timeScale);
            if (elapsedHomeSeconds >= SecondsPerDay)
            {
                long daysPassed = (long)Math.Floor(elapsedHomeSeconds / SecondsPerDay);
                dayIndex += daysPassed;
                elapsedHomeSeconds -= daysPassed * SecondsPerDay;
            }
            daySeconds = elapsedHomeSeconds;
            Apply(false);
            UpdateMeteor();
        }

        private double SecondsPerDay => (double)Math.Max(1, config.secondsPerMinute)
            * Math.Max(1, config.minutesPerHour) * Math.Max(1, config.hoursPerDay);

        private void Apply(bool force)
        {
            int stage = StageAt(daySeconds);
            if (force || stage != appliedStage)
            {
                appliedStage = stage;
                Stage = stage;
                scene.SetSkyStage(stage);
            }
            int lightTimeIndex = LightTimeIndexAt(daySeconds);
            if (force || lightTimeIndex != appliedLightTimeIndex)
            {
                appliedLightTimeIndex = lightTimeIndex;
                ApplyLightDefaults(lightTimeIndex);
            }
            bool night = NightAt(daySeconds);
            if (force || night != appliedNight)
            {
                appliedNight = night;
                ApplyNightEffects(night);
            }
            int moon = MoonPhaseAt();
            if (force || moon != appliedMoon)
            {
                appliedMoon = moon;
                MoonPhase = moon;
            }
            scene.SetMoonVisual(moonVisuals, MoonPhase,
                (float)(daySeconds / SecondsPerDay * moonVisuals.duration));
            daySky?.Apply((float)(daySeconds / SecondsPerDay));
        }

        public void Dispose()
        {
            daySky?.Dispose();
        }

        private int LightTimeIndexAt(double seconds)
        {
            int activeIndex = -1;
            double activeSeconds = double.NegativeInfinity;
            HomeLightTime[] lightTimes = config.lightTimes ?? Array.Empty<HomeLightTime>();
            for (int index = 0; index < lightTimes.Length; index++)
            {
                HomeLightTime group = lightTimes[index];
                if (group?.begin == null || group.begin.Seconds > seconds) continue;
                if (group.begin.Seconds >= activeSeconds)
                {
                    activeSeconds = group.begin.Seconds;
                    activeIndex = index;
                }
            }
            return activeIndex;
        }

        private void ApplyLightDefaults(int activeIndex)
        {
            HomeLightTime[] lightTimes = config.lightTimes ?? Array.Empty<HomeLightTime>();
            if (activeIndex < 0 || activeIndex >= lightTimes.Length) return;
            HomeLightState[] lights = lightTimes[activeIndex]?.lights;
            if (lights == null || lights.Length == 0) return;
            foreach (HomeLightState light in lights)
            {
                if (light == null || string.IsNullOrEmpty(light.name)) continue;
                if (!LightPaths.TryGetValue(light.name, out string path)) continue;
                scene.SetLayerGroupVisible(path, light.on);
            }
        }

        private void ApplyNightEffects(bool night)
        {
            scene.SetNightVisuals(night);
        }

        private int StageAt(double seconds)
        {
            int stage = 0;
            double best = -1;
            foreach (HomeDayStage entry in config.stages ?? Array.Empty<HomeDayStage>())
            {
                if (entry?.time == null) continue;
                double value = entry.time.Seconds;
                if (value <= seconds && value >= best)
                {
                    best = value;
                    stage = entry.stage;
                }
            }
            if (best < 0 && config.stages.Length > 0 && config.stages[0]?.time != null)
                stage = config.stages[0].stage;
            return stage;
        }

        private bool NightAt(double seconds)
        {
            double on = config.effectLightOn?.Seconds ?? 18 * 3600;
            double off = config.effectLightOff?.Seconds ?? 6 * 3600;
            return on <= off ? seconds >= on && seconds < off : seconds >= on || seconds < off;
        }

        private int MoonPhaseAt()
        {
            int duration = config.moon?.DayDuration ?? 2;
            int cycle = config.moon?.DayCycle ?? 30;
            if (moonVariants.Length == 0 || duration <= 0 || cycle <= 0) return 0;
            int elapsedDays = (int)(((dayIndex % cycle) + cycle) % cycle);
            int index = elapsedDays / duration + 1;
            return moonVariants.Any(variant => variant != null && variant.index == index) ? index : 0;
        }

        private double AbsoluteHomeSeconds => dayIndex * SecondsPerDay + daySeconds;

        private void UpdateMeteor()
        {
            HomeMeteorConfig meteor = config.meteor;
            if (meteor == null || meteor.interval <= 0) return;
            double begin = meteor.begin?.Seconds ?? 20 * 3600;
            double end = meteor.end?.Seconds ?? 6 * 3600;
            bool window = begin < end ? daySeconds >= begin && daySeconds < end : daySeconds >= begin || daySeconds < end;
            if (!window)
            {
                meteorActive = false;
                hasMeteorSchedule = false;
                return;
            }

            double now = AbsoluteHomeSeconds;
            if (!meteorActive)
            {
                meteorActive = true;
                hasMeteorSchedule = true;
                nextMeteorHomeSeconds = now + meteor.interval;
                return;
            }
            if (!hasMeteorSchedule)
            {
                hasMeteorSchedule = true;
                nextMeteorHomeSeconds = now + meteor.interval;
                return;
            }
            if (now < nextMeteorHomeSeconds) return;
            if (UnityEngine.Random.Range(0, 100) / 100f > meteor.probability) return;
            int templateCount = meteorRuntime.templates.Length;
            int templateIndex = UnityEngine.Random.Range(0, templateCount);
            MeteorTriggered?.Invoke(templateIndex);
            nextMeteorHomeSeconds = now + meteor.interval;
        }

        public event Action<int> MeteorTriggered;
    }
}
