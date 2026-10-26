using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    internal sealed class OfficialMap10AudioPlayer : MonoBehaviour
    {
        private const string MappingFile = "Map10Audio/map10-audio.json";

        private readonly Dictionary<string, Map10AudioFile> voiceFiles =
            new Dictionary<string, Map10AudioFile>(StringComparer.Ordinal);
        private readonly Dictionary<string, Map10PlayableSound[]> playableSounds =
            new Dictionary<string, Map10PlayableSound[]>(StringComparer.Ordinal);
        private AudioSource source;
        private AudioSource effectsSource;
        private AudioSource bgmSource;
        private AudioClip currentClip;
        private AudioClip bgmClip;
        private int requestVersion;
        private int bgmRequestVersion;

        [Serializable]
        private sealed class AudioMap
        {
            public Map10AudioEvent[] events;
            public Map10Playable[] playables;
            public Map10Bgm mapBgm;
        }

        [Serializable]
        private sealed class Map10AudioEvent
        {
            public string eventKey;
            public Map10AudioFile[] files;
        }

        [Serializable]
        private sealed class Map10AudioFile
        {
            public string path;
            public string audioType;
        }

        [Serializable]
        private sealed class Map10Playable
        {
            public string assetGuid;
            public string[] keys;
            public Map10PlayableSound[] sounds;
        }

        [Serializable]
        private sealed class Map10PlayableSound
        {
            public string eventKey;
            public float start;
        }

        [Serializable]
        private sealed class Map10Bgm
        {
            public int bgmId;
            public string playEventKey;
            public string stopEventKey;
            public string pauseEventKey;
            public string resumeEventKey;
            public bool loop;
            public Map10AudioFile file;
        }

        public void Initialize()
        {
            string path = Path.Combine(Application.streamingAssetsPath, MappingFile);
            if (!File.Exists(path)) throw new FileNotFoundException("Official Map10 voice mapping is missing.", path);
            AudioMap map = JsonUtility.FromJson<AudioMap>(File.ReadAllText(path));
            if (map?.events == null) throw new InvalidDataException("Official Map10 voice mapping is malformed.");
            foreach (Map10AudioEvent entry in map.events)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.eventKey)
                    || entry.files == null || entry.files.Length != 1
                    || string.IsNullOrWhiteSpace(entry.files[0].path)
                    || (entry.files[0].audioType != "WAV" && entry.files[0].audioType != "OGGVORBIS")
                    || voiceFiles.ContainsKey(entry.eventKey))
                    throw new InvalidDataException("Official Map10 voice mapping contains an invalid event.");
                voiceFiles.Add(entry.eventKey, entry.files[0]);
            }
            foreach (Map10Playable playable in map.playables ?? Array.Empty<Map10Playable>())
            {
                if (playable == null || string.IsNullOrWhiteSpace(playable.assetGuid) || playable.sounds == null
                    || playable.sounds.Any(sound => sound == null || string.IsNullOrWhiteSpace(sound.eventKey)
                        || !voiceFiles.ContainsKey(sound.eventKey)
                        || sound.start < 0f))
                    throw new InvalidDataException("Official Map10 playable sound mapping is invalid.");
                AddPlayableSoundBinding(playable.assetGuid, playable.sounds);
                foreach (string key in playable.keys ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(key)) AddPlayableSoundBinding(key, playable.sounds);
                }
            }
            if (map.mapBgm == null || map.mapBgm.bgmId != 10 || !map.mapBgm.loop
                || string.IsNullOrWhiteSpace(map.mapBgm.playEventKey)
                || string.IsNullOrWhiteSpace(map.mapBgm.stopEventKey)
                || string.IsNullOrWhiteSpace(map.mapBgm.pauseEventKey)
                || string.IsNullOrWhiteSpace(map.mapBgm.resumeEventKey)
                || map.mapBgm.file == null || string.IsNullOrWhiteSpace(map.mapBgm.file.path)
                || !voiceFiles.TryGetValue(map.mapBgm.playEventKey, out Map10AudioFile playFile)
                || playFile.path != map.mapBgm.file.path)
                throw new InvalidDataException("Official Map10 BGM mapping is invalid.");

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.loop = false;
            effectsSource.spatialBlend = 0f;
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = map.mapBgm.loop;
            bgmSource.spatialBlend = 0f;
            StartCoroutine(LoadAndPlayBgm(map.mapBgm.file, ++bgmRequestVersion));
        }

        public void PlayPlayable(string keyOrGuid)
        {
            if (!playableSounds.TryGetValue(keyOrGuid ?? string.Empty, out Map10PlayableSound[] sounds)) return;
            foreach (Map10PlayableSound sound in sounds) StartCoroutine(PlayEffectAt(sound));
        }

        public void PlayVoice(string eventKey)
        {
            StopVoice();
            if (string.IsNullOrWhiteSpace(eventKey)) return;
            if (!voiceFiles.TryGetValue(eventKey, out Map10AudioFile file))
            {
                Debug.LogWarning("Official Map10 voice event is not mapped locally: " + eventKey);
                return;
            }
            int version = ++requestVersion;
            StartCoroutine(LoadAndPlay(file, eventKey, version));
        }

        public void StopVoice()
        {
            requestVersion++;
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
            if (currentClip != null) Destroy(currentClip);
            currentClip = null;
        }

        public void StopMapBgm()
        {
            bgmRequestVersion++;
            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
            if (bgmClip != null) Destroy(bgmClip);
            bgmClip = null;
        }

        private IEnumerator LoadAndPlay(Map10AudioFile file, string eventKey, int version)
        {
            string path = Path.Combine(Application.streamingAssetsPath,
                file.path.Replace('/', Path.DirectorySeparatorChar));
            if (!Uri.TryCreate(path, UriKind.Absolute, out Uri uriValue))
            {
                Debug.LogWarning("Official Map10 voice path is invalid for " + eventKey + ".");
                yield break;
            }

            AudioType type = file.audioType == "WAV" ? AudioType.WAV : AudioType.OGGVORBIS;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uriValue.AbsoluteUri, type))
            {
                yield return request.SendWebRequest();
                if (version != requestVersion) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Official Map10 voice could not be loaded for " + eventKey + ": " + request.error);
                    yield break;
                }

                currentClip = DownloadHandlerAudioClip.GetContent(request);
                if (currentClip == null)
                {
                    Debug.LogWarning("Official Map10 voice decoded to an empty clip: " + eventKey);
                    yield break;
                }
                source.clip = currentClip;
                source.Play();
            }

            while (version == requestVersion && source != null && source.isPlaying) yield return null;
            if (version == requestVersion) StopVoice();
        }

        private void AddPlayableSoundBinding(string key, Map10PlayableSound[] sounds)
        {
            if (playableSounds.ContainsKey(key))
                throw new InvalidDataException("Official Map10 playable sound key is duplicated: " + key);
            playableSounds.Add(key, sounds);
        }

        private IEnumerator PlayEffectAt(Map10PlayableSound sound)
        {
            if (sound.start > 0f) yield return new WaitForSecondsRealtime(sound.start);
            Map10AudioFile file = voiceFiles[sound.eventKey];
            string path = Path.Combine(Application.streamingAssetsPath,
                file.path.Replace('/', Path.DirectorySeparatorChar));
            if (!Uri.TryCreate(path, UriKind.Absolute, out Uri uriValue)) yield break;
            AudioType type = file.audioType == "WAV" ? AudioType.WAV : AudioType.OGGVORBIS;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uriValue.AbsoluteUri, type))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Official Map10 playable sound could not be loaded for "
                        + sound.eventKey + ": " + request.error);
                    yield break;
                }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null) yield break;
                effectsSource.PlayOneShot(clip);
                Destroy(clip, clip.length + 1f);
            }
        }

        private IEnumerator LoadAndPlayBgm(Map10AudioFile file, int version)
        {
            string path = Path.Combine(Application.streamingAssetsPath,
                file.path.Replace('/', Path.DirectorySeparatorChar));
            if (!Uri.TryCreate(path, UriKind.Absolute, out Uri uriValue)) yield break;
            AudioType type = file.audioType == "WAV" ? AudioType.WAV : AudioType.OGGVORBIS;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uriValue.AbsoluteUri, type))
            {
                yield return request.SendWebRequest();
                if (version != bgmRequestVersion) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Official Map10 BGM could not be loaded: " + request.error);
                    yield break;
                }
                bgmClip = DownloadHandlerAudioClip.GetContent(request);
                if (bgmClip == null) yield break;
                bgmSource.clip = bgmClip;
                bgmSource.Play();
            }
        }
    }
}
