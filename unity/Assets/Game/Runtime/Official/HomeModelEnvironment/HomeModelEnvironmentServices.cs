using System;
using UnityEngine;
using Zjcs.Official.HomeEnvironmentTime;

namespace Zjcs.Official.HomeModelEnvironment
{
    // Owner-supplied current stage, original configuration and player notifications.
    public interface IHomeModelEnvironmentServices
    {
        HomeDayTime HomeDayTime { get; }
        HomeEnvironmentConfig TimeConfiguration { get; }
        HomeModelLightConfiguration LightConfiguration { get; }
        string CurrentPlayerBehaviour { get; }
        float PlayableDeltaTime { get; }
        bool IsHomeTimeLimit { get; }
        IDisposable SubscribePlayerBehaviour(Action<PlayerBehaviourNotifyData> callback);
    }

    public static class HomeModelEnvironmentCatalog
    {
        [Serializable]
        public sealed class ClockConfiguration
        {
            public int SecondsPerMinute, MinutesPerHour, HoursPerDay;
            public HomeEnvironmentConfig CreateConfiguration() => new HomeEnvironmentConfig {
                SecondsPerMinute = SecondsPerMinute, MinutesPerHour = MinutesPerHour, HoursPerDay = HoursPerDay
            };
        }
        [Serializable]
        public sealed class Payload
        {
            public HomeModelLightConfiguration Lights;
            public ClockConfiguration Clock;
        }
        public static Payload Load()
        {
            var asset = Resources.Load<TextAsset>("Home/ModelEnvironment/catalog");
            if (!asset) throw new InvalidOperationException("The local original home model environment catalog is missing");
            var payload = JsonUtility.FromJson<Payload>(asset.text);
            if (payload?.Lights?.HomeLightTimeConfigs == null || payload.Clock == null)
                throw new InvalidOperationException("Complete original home light/clock configuration is required");
            return payload;
        }
    }
}
