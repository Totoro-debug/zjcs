using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace Zjcs.Presentation.Home
{
    internal sealed class FantasyTowerAudioPlayer : MonoBehaviour
    {
        [Serializable] private sealed class AudioMap { public string manifest; public Event[] events; }
        [Serializable] private sealed class Event
        {
            public string name, file, sha256;
            public float duration;
        }

        private readonly Dictionary<string, Event> events = new Dictionary<string, Event>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private AudioSource source;

        public void Initialize()
        {
            if (source != null) return;
            string path = Path.Combine(Application.streamingAssetsPath, "TowerPush/audio/audio.json");
            AudioMap map = JsonUtility.FromJson<AudioMap>(File.ReadAllText(path));
            if (map == null || map.manifest != "854_156383" || map.events == null)
                throw new InvalidDataException("Official Fantasy Tower audio mapping is invalid.");
            foreach (Event entry in map.events)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.name)
                    || string.IsNullOrWhiteSpace(entry.file) || Path.GetFileName(entry.file) != entry.file
                    || entry.duration <= 0f || entry.sha256 == null || entry.sha256.Length != 64
                    || events.ContainsKey(entry.name))
                    throw new InvalidDataException("Official Fantasy Tower audio entry is malformed.");
                events.Add(entry.name, entry);
            }
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        public void Play(string eventName)
        {
            if (!events.TryGetValue(eventName, out Event entry))
                throw new InvalidOperationException("Official Fantasy Tower audio event is unavailable: " + eventName);
            if (clips.TryGetValue(eventName, out AudioClip clip))
            {
                source.PlayOneShot(clip);
                return;
            }
            StartCoroutine(LoadAndPlay(entry));
        }

        private IEnumerator LoadAndPlay(Event entry)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "TowerPush/audio", entry.file);
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                       new Uri(path).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException("Official Fantasy Tower audio failed to load: " + request.error);
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                byte[] data = File.ReadAllBytes(path);
                using (SHA256 sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                    if (hash != entry.sha256 || Mathf.Abs(clip.length - entry.duration) > .05f)
                    {
                        Destroy(clip);
                        throw new InvalidDataException("Official Fantasy Tower audio does not match its manifest: "
                            + entry.name);
                    }
                }
                clips.Add(entry.name, clip);
                source.PlayOneShot(clip);
            }
        }
    }
}
