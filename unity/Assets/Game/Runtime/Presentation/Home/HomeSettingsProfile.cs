using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Home
{
    public enum HomeAudioChannel
    {
        Music,
        Sound,
        Voice
    }

    [Serializable]
    public sealed class HomeSettingsData
    {
        public int version = 1;
        public float allSound = 1f;
        public float music;
        public float sound;
        public float voice;
        public int frameRate = 60;
        public bool compactNumbers;
    }

    public sealed class HomeSettingsProfile
    {
        private readonly string path;
        private HomeSettingsData state = new HomeSettingsData();

        public SaveReadStatus LoadStatus { get; private set; }
        public float AllSound => state.allSound;
        public float Music => state.music;
        public float Sound => state.sound;
        public float Voice => state.voice;
        public int FrameRate => state.frameRate;
        public bool CompactNumbers => state.compactNumbers;

        public HomeSettingsProfile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A profile settings path is required.", nameof(path));
            this.path = Path.GetFullPath(path);
        }

        public void Load()
        {
            SaveReadResult result = LocalJsonSaveFile.Read(path, IsValidJson);
            LoadStatus = result.Status;
            state = result.Json == null
                ? new HomeSettingsData()
                : JsonUtility.FromJson<HomeSettingsData>(result.Json);
            if (!IsValid(state)) state = new HomeSettingsData();
            Apply();
        }

        public bool TrySetVolume(HomeAudioChannel channel, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            value = Mathf.Clamp01(value);
            return Commit(next =>
            {
                switch (channel)
                {
                    case HomeAudioChannel.Music: next.music = value; break;
                    case HomeAudioChannel.Sound: next.sound = value; break;
                    case HomeAudioChannel.Voice: next.voice = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(channel));
                }
            });
        }

        public bool TrySetAllSound(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            value = Mathf.Clamp01(value);
            return Commit(next => next.allSound = value);
        }

        public bool TrySetFrameRate(int value)
        {
            if (value != 30 && value != 60) return false;
            return Commit(next => next.frameRate = value);
        }

        public bool TrySetCompactNumbers(bool value)
        {
            return Commit(next => next.compactNumbers = value);
        }

        public string FormatNumber(long value)
        {
            if (!state.compactNumbers || value < 10000)
                return value.ToString(CultureInfo.InvariantCulture);
            if (value >= 100000000)
                return TruncatedUnit(value, 100000000, "亿");
            return TruncatedUnit(value, 10000, "万");
        }

        private static string TruncatedUnit(long value, long unit, string suffix)
        {
            long tenths = value / (unit / 10);
            string whole = (tenths / 10).ToString(CultureInfo.InvariantCulture);
            int fraction = (int)(tenths % 10);
            return whole + (fraction == 0 ? string.Empty : "." + fraction.ToString(CultureInfo.InvariantCulture))
                + suffix;
        }

        private bool Commit(Action<HomeSettingsData> change)
        {
            HomeSettingsData next = Clone(state);
            change(next);
            if (!IsValid(next)) return false;
            string json = JsonUtility.ToJson(next);
            try
            {
                LocalJsonSaveFile.Write(path, json, IsValidJson);
                state = next;
                Apply();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home settings could not be saved: " + error.Message);
                Apply();
                return false;
            }
        }

        private void Apply()
        {
            HomeAudioSettings.Configure(state);
            Application.targetFrameRate = state.frameRate;
        }

        private static HomeSettingsData Clone(HomeSettingsData source)
        {
            return JsonUtility.FromJson<HomeSettingsData>(JsonUtility.ToJson(source));
        }

        private static bool IsValidJson(string json)
        {
            try { return IsValid(JsonUtility.FromJson<HomeSettingsData>(json)); }
            catch { return false; }
        }

        private static bool IsValid(HomeSettingsData value)
        {
            return value != null && value.version == 1
                && ValidVolume(value.allSound) && ValidVolume(value.music)
                && ValidVolume(value.sound) && ValidVolume(value.voice)
                && (value.frameRate == 30 || value.frameRate == 60);
        }

        private static bool ValidVolume(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }
    }

    public static class HomeAudioSettings
    {
        public static float MusicVolume => ChannelVolume(HomeAudioChannel.Music);
        private static readonly Dictionary<AudioSource, HomeAudioChannel> Sources =
            new Dictionary<AudioSource, HomeAudioChannel>();
        private static HomeSettingsData state = new HomeSettingsData();

        public static void Register(AudioSource source, HomeAudioChannel channel)
        {
            if (source == null) return;
            Sources[source] = channel;
            source.volume = ChannelVolume(channel);
        }

        public static void Unregister(AudioSource source)
        {
            if (source != null) Sources.Remove(source);
        }

        public static void Configure(HomeSettingsData value)
        {
            if (value == null) return;
            state = value;
            AudioListener.volume = Mathf.Clamp01(state.allSound);
            List<AudioSource> dead = null;
            foreach (KeyValuePair<AudioSource, HomeAudioChannel> entry in Sources)
            {
                if (entry.Key == null)
                {
                    if (dead == null) dead = new List<AudioSource>();
                    dead.Add(entry.Key);
                    continue;
                }
                entry.Key.volume = ChannelVolume(entry.Value);
            }
            if (dead != null)
                foreach (AudioSource source in dead) Sources.Remove(source);
        }

        private static float ChannelVolume(HomeAudioChannel channel)
        {
            switch (channel)
            {
                case HomeAudioChannel.Music: return Mathf.Clamp01(state.music);
                case HomeAudioChannel.Sound: return Mathf.Clamp01(state.sound);
                case HomeAudioChannel.Voice: return Mathf.Clamp01(state.voice);
                default: return 0f;
            }
        }
    }
}
