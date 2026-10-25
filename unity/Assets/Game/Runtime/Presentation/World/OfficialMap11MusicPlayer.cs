using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    // MapBgmHandler's default event binding, with native Windows AudioClip
    // playback replacing the original Android Wwise output device.
    internal sealed class OfficialMap11MusicPlayer : MonoBehaviour
    {
        [Serializable] private sealed class Event
        {
            public string eventKey;
            public int actionType, transitionMs, fadeCurve;
        }

        [Serializable] private sealed class Config
        {
            public string manifest, clip;
            public int mapClassId, bgmId;
            public bool loop;
            public Event play, stop, pause, resume;
        }

        private Config config;
        private AudioSource source;
        private Func<float> musicVolume;
        private Coroutine transition;
        private float envelope;
        private bool paused, released;

        public void Initialize(Func<float> volume)
        {
            if (source != null) throw new InvalidOperationException("Map11 music is already initialized.");
            TextAsset mapping = Resources.Load<TextAsset>("Map11/Audio/bgm");
            config = mapping == null ? null : JsonUtility.FromJson<Config>(mapping.text);
            if (config == null || config.manifest != "854_156383" || config.mapClassId != 11
                || config.bgmId != 11 || !config.loop || config.clip != "Map11/Audio/Map11Bgm"
                || !Valid(config.play, "Play", 1027) || !Valid(config.stop, "Stop", 258)
                || !Valid(config.pause, "Pause", 514) || !Valid(config.resume, "Resume", 770))
                throw new InvalidDataException("Map11 original music event mapping is invalid.");
            musicVolume = volume ?? (() => 1f);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = config.loop;
            source.volume = 0f;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Load());
        }

        private static bool Valid(Event value, string suffix, int actionType) =>
            value != null && value.eventKey == "Map_11_" + suffix && value.actionType == actionType
            && value.transitionMs >= 0 && value.fadeCurve == 1;

        private IEnumerator Load()
        {
            ResourceRequest request = Resources.LoadAsync<AudioClip>(config.clip);
            yield return request;
            if (released) yield break;
            source.clip = request.asset as AudioClip;
            if (source.clip == null || source.clip.samples <= 0)
                throw new InvalidDataException("Map11 original music clip could not be imported on Windows.");
            if (FindObjectOfType<AudioListener>() == null)
            {
                var listener = new GameObject("OfficialWorldAudioListener", typeof(AudioListener));
                DontDestroyOnLoad(listener);
            }
            if (!paused)
            {
                source.Play();
                Change(config.play, 1f, null);
            }
        }

        // BgmManager pauses the current music before playing a higher-priority
        // binding, then resumes it when that binding is removed.
        public void Pause()
        {
            if (released || paused) return;
            paused = true;
            Change(config.pause, 0f, () => source.Pause());
        }

        public void Resume()
        {
            if (released || !paused) return;
            paused = false;
            if (source.clip != null)
            {
                source.UnPause();
                if (!source.isPlaying) source.Play();
            }
            Change(config.resume, 1f, null);
        }

        public void StopAndRelease()
        {
            if (released) return;
            released = true;
            if (source == null || source.clip == null)
            {
                Destroy(gameObject);
                return;
            }
            Change(config.stop, 0f, () =>
            {
                source.Stop();
                Destroy(gameObject);
            });
        }

        private void Change(Event action, float target, Action completed)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Fade(action.transitionMs / 1000f, target, completed));
        }

        private IEnumerator Fade(float seconds, float target, Action completed)
        {
            float initial = envelope;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                // All four original actions select AkCurveInterpolation.Sine.
                envelope = Mathf.LerpUnclamped(initial, target,
                    Mathf.Sin(Mathf.Clamp01(elapsed / seconds) * Mathf.PI * .5f));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            envelope = target;
            transition = null;
            completed?.Invoke();
        }

        private void LateUpdate()
        {
            if (source != null) source.volume = Mathf.Clamp01(musicVolume()) * envelope;
        }

        private void OnDestroy()
        {
            if (source != null) source.Stop();
        }
    }
}
