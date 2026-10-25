using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    internal sealed class OfficialMap11StoryAudioPlayer : MonoBehaviour
    {
        [Serializable] private sealed class Event { public string eventKey, clip; }
        [Serializable] private sealed class Mapping
        {
            public string manifest, stopEventKey;
            public int mapClassId;
            public Event[] events;
        }

        private readonly Dictionary<string, string> clips = new Dictionary<string, string>(StringComparer.Ordinal);
        private AudioSource source;
        private Func<float> voiceVolume;
        private int version;

        public void Initialize(Func<float> volume)
        {
            TextAsset payload = Resources.Load<TextAsset>("Map11/Audio/voices");
            Mapping mapping = payload == null ? null : JsonUtility.FromJson<Mapping>(payload.text);
            if (mapping == null || mapping.manifest != "854_156383" || mapping.mapClassId != 11
                || mapping.stopEventKey != "Voice_StopAll" || mapping.events == null || mapping.events.Length != 207)
                throw new InvalidDataException("Map11 original dialogue voice mapping is invalid.");
            foreach (Event value in mapping.events)
            {
                if (value == null || string.IsNullOrEmpty(value.eventKey) || string.IsNullOrEmpty(value.clip)
                    || !value.clip.StartsWith("Map11/Audio/Voices/", StringComparison.Ordinal)
                    || value.clip.Contains("..") || clips.ContainsKey(value.eventKey))
                    throw new InvalidDataException("Map11 original dialogue voice binding is malformed.");
                clips.Add(value.eventKey, value.clip);
            }
            voiceVolume = volume ?? (() => 1f);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = Mathf.Clamp01(voiceVolume());
        }

        // TalkDialog.RefreshTalkVoice always posts Voice_StopAll first,
        // including when the following line has no voice event.
        public void RefreshVoice(string eventKey)
        {
            StopVoice();
            if (string.IsNullOrEmpty(eventKey)) return;
            if (!clips.TryGetValue(eventKey, out string path))
                throw new InvalidDataException("Map11 original dialogue voice is not bound: " + eventKey);
            StartCoroutine(LoadAndPlay(path, version));
        }

        private IEnumerator LoadAndPlay(string path, int requestedVersion)
        {
            ResourceRequest request = Resources.LoadAsync<AudioClip>(path);
            yield return request;
            if (requestedVersion != version) yield break;
            AudioClip clip = request.asset as AudioClip;
            if (clip == null || clip.samples <= 0)
                throw new InvalidDataException("Map11 original dialogue voice could not be imported: " + path);
            source.clip = clip;
            source.Play();
        }

        public void StopVoice()
        {
            version++;
            if (source == null) return;
            source.Stop();
            source.clip = null;
        }

        private void LateUpdate()
        {
            if (source != null) source.volume = Mathf.Clamp01(voiceVolume());
        }

        private void OnDisable() => StopVoice();
    }
}
