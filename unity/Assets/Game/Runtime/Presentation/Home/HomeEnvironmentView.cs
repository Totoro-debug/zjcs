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
        public HomeMoonVariant[] moonVariants;
    }

    /// <summary>
    /// Port of ``HomeEnvironmentManage``/``HomeClock`` for the offline home:
    /// the day advances at ``HomeTimeScale``, the day stage and the light groups
    /// follow ``HomeLightTimeConfigs``, and night effects switch at
    /// ``EffectLightOnTime``/``EffectLightOffTime``.  Sky colour curves live in
    /// the official ``HomeEnvironmentConfig`` animation clips which are not part
    /// of the extracted payload yet, so no colour curve is invented here.
    /// </summary>
    public sealed class HomeEnvironmentView
    {
        private static readonly Dictionary<string, string> LightPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "DoorLight", "/HouseOutside/HouseOutSideLight/Door_Light" },
            { "LobbyLight", "/House/HouseLight/Lobby_Light" },
            { "BedRoomLight", "/House/HouseLight/Bedroom_Light" },
            { "StudyLight", "/House/HouseLight/Study_Light" },
            { "DeskLight", "/Desk/Desk_Light" },
            { "DiningTableLight", "/DiningTable/HomeDiningTable_Light" }
        };

        private readonly HomeSceneView scene;
        private readonly HomeEnvironmentConfig config;
        private readonly HomeMoonVariant[] moonVariants;
        private double daySeconds;
        private int appliedStage = -1;
        private bool appliedNight;
        private int appliedMoon;
        private double meteorTimer;
        private int dayIndex;

        public int Stage { get; private set; }
        public int Hour => (int)(daySeconds / 3600) % 24;
        public int Minute => (int)(daySeconds / 60) % 60;
        public bool IsNight => NightAt(daySeconds);
        public int MoonPhase { get; private set; }
        public HomeEnvironmentConfig Config => config;

        public HomeEnvironmentView(HomeSceneView scene, long nowUnixSeconds)
        {
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
            string path = Path.Combine(Application.streamingAssetsPath, HomeAssetCatalog.FolderName, "environment.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Home environment payload is missing.", path);
            HomeEnvironmentData data = JsonUtility.FromJson<HomeEnvironmentData>(File.ReadAllText(path));
            if (data == null || data.config == null || data.config.stages == null || data.config.stages.Length == 0)
                throw new InvalidDataException("The official home environment config is incomplete.");
            config = data.config;
            moonVariants = data.moonVariants ?? Array.Empty<HomeMoonVariant>();
            DateTime local = DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, nowUnixSeconds)).LocalDateTime;
            daySeconds = local.Hour * 3600 + local.Minute * 60 + local.Second;
            dayIndex = local.DayOfYear;
            Apply(true);
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (unscaledDeltaSeconds <= 0) return;
            daySeconds += unscaledDeltaSeconds * Math.Max(1.0, config.timeScale);
            while (daySeconds >= 86400)
            {
                daySeconds -= 86400;
                dayIndex++;
            }
            Apply(false);
            UpdateMeteor(unscaledDeltaSeconds);
        }

        private void Apply(bool force)
        {
            int stage = StageAt(daySeconds);
            if (force || stage != appliedStage)
            {
                appliedStage = stage;
                Stage = stage;
                ApplyLightDefaults();
            }
            bool night = NightAt(daySeconds);
            if (force || night != appliedNight)
            {
                appliedNight = night;
                ApplyNightEffects(night, force);
            }
            int moon = MoonPhaseAt();
            if (force || moon != appliedMoon)
            {
                appliedMoon = moon;
                MoonPhase = moon;
            }
        }

        private void ApplyLightDefaults()
        {
            HomeLightTime active = null;
            foreach (HomeLightTime group in config.lightTimes ?? Array.Empty<HomeLightTime>())
            {
                if (group?.begin == null || group.begin.Seconds > daySeconds) continue;
                if (active == null || group.begin.Seconds >= active.begin.Seconds) active = group;
            }
            if (active?.lights == null) return;
            foreach (HomeLightState light in active.lights)
            {
                if (light == null || string.IsNullOrEmpty(light.name)) continue;
                if (!LightPaths.TryGetValue(light.name, out string path)) continue;
                scene.SetLayerGroupVisible(path, light.on);
            }
        }

        private void ApplyNightEffects(bool night, bool force)
        {
            // The official night pass is driven by HomeEnvironmentConfig animation
            // clips (sky/moon/star colours) that are not part of the extracted
            // payload.  Only the light groups and the stage clock are applied here.
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
            int phase = dayIndex / duration % cycle;
            HomeMoonVariant variant = moonVariants[phase % moonVariants.Length];
            return variant.index;
        }

        private void UpdateMeteor(float unscaledDeltaSeconds)
        {
            HomeMeteorConfig meteor = config.meteor;
            if (meteor == null || meteor.interval <= 0) return;
            double begin = meteor.begin?.Seconds ?? 20 * 3600;
            double end = meteor.end?.Seconds ?? 6 * 3600;
            bool window = begin <= end ? daySeconds >= begin && daySeconds < end : daySeconds >= begin || daySeconds < end;
            if (!window)
            {
                meteorTimer = 0;
                return;
            }
            meteorTimer += unscaledDeltaSeconds * Math.Max(1.0, config.timeScale);
            if (meteorTimer < meteor.interval) return;
            meteorTimer = 0;
            if (UnityEngine.Random.value > meteor.probability) return;
            MeteorTriggered?.Invoke();
        }

        public event Action MeteorTriggered;
    }
}
