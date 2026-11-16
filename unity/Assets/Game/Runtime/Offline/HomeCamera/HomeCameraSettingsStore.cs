using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.State;
using Zjcs.Official.HomeCamera;

namespace Zjcs.Offline.HomeCamera
{
    // DesktopDevice.GetPerformanceLevel in the selected client returns Ultra.
    public sealed class HomeCameraDesktopDevice : IDevice
    {
        public PerformanceLevel GetPerformanceLevel() => PerformanceLevel.Ultra;
    }

    // The original service keeps BloomIntensitySettingState and the legacy
    // PerformanceSettingState separately. This file stores their relevant
    // fields locally without modifying the existing sound settings file.
    public sealed class HomeCameraSettingsStore
    {
        [Serializable]
        private sealed class SaveData
        {
            public int version = 1;
            public int intensityId;
            public bool hasIntensity;
            public int intensity;
            public int legacyId;
            public bool hasLegacyBloom;
            public bool legacyBloom;
        }

        private sealed class PersistentValue<T> : IPersistentState<T>
        {
            private readonly HomeCameraSettingsStore owner;
            public T State { get; }
            public PersistentValue(HomeCameraSettingsStore owner, T state) { this.owner = owner; State = state; }
            public Task WriteStateAsync()
            {
                try { owner.Write(); return Task.CompletedTask; }
                catch (Exception error) { return Task.FromException(error); }
            }
        }

        private readonly string path;
        private readonly PersistentValue<BloomIntensitySettingState> current;
        private readonly PersistentValue<PerformanceSettingState> legacy;
        public IPersistentState<BloomIntensitySettingState> Current => current;
        public IPersistentState<PerformanceSettingState> Legacy => legacy;
        public SaveReadStatus LoadStatus { get; }

        public HomeCameraSettingsStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A camera settings path is required.", nameof(path));
            this.path = Path.GetFullPath(path);
            SaveReadResult result = LocalJsonSaveFile.Read(this.path, IsValidJson);
            LoadStatus = result.Status;
            SaveData saved = result.Json == null ? new SaveData() : JsonUtility.FromJson<SaveData>(result.Json);
            current = new PersistentValue<BloomIntensitySettingState>(this, new BloomIntensitySettingState
            {
                Id = saved.intensityId,
                BloomIntensityLevel = saved.hasIntensity ? (EBloomIntensityLevel?)saved.intensity : null
            });
            legacy = new PersistentValue<PerformanceSettingState>(this, new PerformanceSettingState
            {
                Id = saved.legacyId,
                BloomEnabled = saved.hasLegacyBloom ? (bool?)saved.legacyBloom : null
            });
        }

        private void Write()
        {
            var intensity = current.State.BloomIntensityLevel;
            var legacyBloom = legacy.State.BloomEnabled;
            var data = new SaveData
            {
                intensityId = current.State.Id,
                hasIntensity = intensity.HasValue,
                intensity = intensity.HasValue ? (int)intensity.Value : 0,
                legacyId = legacy.State.Id,
                hasLegacyBloom = legacyBloom.HasValue,
                legacyBloom = legacyBloom.GetValueOrDefault()
            };
            LocalJsonSaveFile.Write(path, JsonUtility.ToJson(data), IsValidJson);
        }

        private static bool IsValidJson(string json)
        {
            try { return JsonUtility.FromJson<SaveData>(json)?.version == 1; }
            catch { return false; }
        }
    }
}
