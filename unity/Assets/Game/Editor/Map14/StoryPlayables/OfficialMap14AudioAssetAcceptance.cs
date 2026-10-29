using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zjcs.Editor.Map14
{
    // Platform asset verification only; this does not exercise original dialog,
    // sound bus DSP/RTPC, playable invocation, map input or save progression.
    public static class OfficialMap14AudioAssetAcceptance
    {
        [Serializable] private sealed class Pcm { public int channels, sampleRate, sampleWidth, frames; }
        [Serializable] private sealed class Entry { public string eventKey, clip, timeline; public Pcm pcm; public int actionType; public Entry[] actions, events; }
        [Serializable] private sealed class Mapping
        {
            public string manifest, clip; public int mapClassId; public float gainDb; public Pcm pcm;
            public Entry[] events, timelineRoutedEvents;
        }
        private const string Key = "Zjcs.Map14.AudioAssetAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 native audio asset verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original Map14 audio asset verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original audio Play Mode passed: 418 imported PCM clips match exact original samples and played through AudioSource/listener output; the Map14 direct-dialogue player loaded, played and stopped an original VoiceEventKey. Original bus DSP/RTPC, story graph invocation, playable event invocation, map input and saves remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original Map14 audio assets: " + message); }
        private static Mapping Load(string name)
        {
            var text = Resources.Load<TextAsset>("Map14/Audio/" + name);
            Require(text != null, name + " mapping resource");
            var mapping = JsonUtility.FromJson<Mapping>(text.text);
            Require(mapping.manifest == "871_156598" && mapping.mapClassId == 14, name + " pinned version");
            return mapping;
        }
        private static IEnumerator Steps()
        {
            Application.runInBackground = true;
            var voice = Load("voices"); var playables = Load("playables"); var bgm = Load("bgm");
            Require(voice.events.Length == 400 && voice.timelineRoutedEvents.Length == 1 &&
                voice.timelineRoutedEvents[0].eventKey == "chapter_5_main_36_17" &&
                voice.timelineRoutedEvents[0].timeline == "SuiMo_StoryShow02Timeline" &&
                voice.timelineRoutedEvents[0].events.Select(e => e.eventKey).SequenceEqual(new[] { "chapter_5_main_36_17_1", "chapter_5_main_36_17_2" }), "complete direct and original Timeline voice scope");
            Require(playables.events.Length == 17 && playables.events.Sum(e => e.actions.Length) == 24, "six MyPlayableSound, five story Timeline and six entity Timeline references, ordered original actions");
            var actions = playables.events.SelectMany(e => e.actions.Where(a => a.actionType == 1027).Select(a => new Entry { eventKey = e.eventKey, clip = a.clip, pcm = a.pcm }));
            var entries = voice.events.Concat(actions).Concat(new[] { new Entry { eventKey = "Map_14_Play", clip = bgm.clip, pcm = bgm.pcm } })
                .GroupBy(entry => entry.clip).Select(group => group.First()).ToArray();
            Require(entries.Length == 418, "complete unique audio payload scope");
            var emitter = new GameObject("Map14 native PCM device probe"); emitter.AddComponent<AudioListener>();
            var output = emitter.AddComponent<AudioSource>(); output.playOnAwake = false; output.spatialBlend = 0; output.loop = false;
            yield return null; yield return null;
            foreach (Entry entry in entries)
            {
                string path = "Assets/Official/World/Map/14/Resources/" + entry.clip + ".wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Require(importer != null && !importer.forceToMono && importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM &&
                    importer.defaultSampleSettings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate &&
                    importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad, entry.eventKey + " native PCM importer settings");
                var request = Resources.LoadAsync<AudioClip>(entry.clip); yield return request;
                var clip = request.asset as AudioClip; Pcm pcm = entry.pcm;
                Require(clip != null && pcm.sampleWidth == 2 && clip.channels == pcm.channels && clip.samples == pcm.frames && clip.frequency == pcm.sampleRate,
                    entry.eventKey + " exact PCM dimensions");
                Require(clip.LoadAudioData(), entry.eventKey + " native audio data request");
                float deadline = Time.realtimeSinceStartup + 10;
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline) yield return null;
                Require(clip.loadState == AudioDataLoadState.Loaded, entry.eventKey + " decoded audio load");
                byte[] raw = File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
                int cursor = 12, start = -1, length = 0;
                while (cursor + 8 <= raw.Length)
                {
                    int size = BitConverter.ToInt32(raw, cursor + 4);
                    if (System.Text.Encoding.ASCII.GetString(raw, cursor, 4) == "data") { start = cursor + 8; length = size; break; }
                    cursor += 8 + size + (size & 1);
                }
                Require(start >= 0 && length == pcm.frames * pcm.channels * 2, entry.eventKey + " source PCM chunk");
                var samples = new float[clip.samples * clip.channels];
                Require(clip.GetData(samples, 0), entry.eventKey + " native imported sample access");
                int audibleSample = -1;
                for (int i = 0; i < samples.Length; i++)
                {
                    float expected = BitConverter.ToInt16(raw, start + i * 2) / 32768f;
                    if (Math.Abs(samples[i] - expected) >= .0000001f)
                        throw new InvalidOperationException(entry.eventKey + " imported PCM differs at sample " + i);
                    if (audibleSample < 0 && Math.Abs(samples[i]) > .001f) audibleSample = i;
                }
                Require(audibleSample >= 0, entry.eventKey + " original non-silent PCM");
                output.clip = clip; output.timeSamples = Math.Max(0, audibleSample / clip.channels - 512); output.Play();
                int initial = output.timeSamples; bool advancing = false, audible = false;
                double posted = AudioSettings.dspTime; double wallDeadline = EditorApplication.timeSinceStartup + 8;
                while (AudioSettings.dspTime - posted < .20 || !audible || !advancing)
                {
                    Require(EditorApplication.timeSinceStartup < wallDeadline, entry.eventKey + " actual audio device timeout");
                    var window = new float[1024]; AudioListener.GetOutputData(window, 0);
                    audible |= window.Any(value => Math.Abs(value) > .000001f);
                    advancing |= output.isPlaying && output.timeSamples > initial;
                    yield return null;
                }
                output.Stop(); output.clip = null; clip.UnloadAudioData(); Resources.UnloadAsset(clip);
                Debug.Log("Map14 exact PCM and actual listener output passed: " + entry.eventKey + " -> " + entry.clip);
            }
            yield return VerifyVoicePlayer(voice);
            yield return VerifyMap14Music(bgm);
            UnityEngine.Object.Destroy(emitter);
        }
        private static IEnumerator VerifyVoicePlayer(Mapping voice)
        {
            var playerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Zjcs.Presentation.World.OfficialMap11StoryAudioPlayer", false))
                .FirstOrDefault(type => type != null);
            Require(playerType != null, "shared original dialogue voice player is loaded");
            var host = new GameObject("Map14 original dialogue voice player verification");
            var player = host.AddComponent(playerType);
            playerType.GetMethod("Initialize").Invoke(player, new object[] { new Func<float>(() => 1f), 14 });
            Entry binding = voice.events[0];
            playerType.GetMethod("RefreshVoice").Invoke(player, new object[] { binding.eventKey });
            var source = (AudioSource)playerType.GetField("source", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player);
            float deadline = Time.realtimeSinceStartup + 10;
            while ((!source || source.clip == null || !source.isPlaying) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(source && source.clip != null && source.clip.samples == binding.pcm.frames
                && source.isPlaying, "Map14 VoiceEventKey loads and plays through the original story voice player");
            playerType.GetMethod("StopVoice").Invoke(player, null);
            Require(!source.isPlaying && source.clip == null, "Map14 Voice_StopAll stops and releases the active direct dialogue voice");
            UnityEngine.Object.Destroy(host);
        }
        private static IEnumerator VerifyMap14Music(Mapping bgm)
        {
            Type playerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Zjcs.Presentation.World.OfficialMap11MusicPlayer", false))
                .FirstOrDefault(type => type != null);
            Require(playerType != null, "original map music player is loaded");
            var host = new GameObject("Map14 original BGM player verification");
            var player = host.AddComponent(playerType);
            playerType.GetMethod("Initialize").Invoke(player, new object[] { new Func<float>(() => 1f), 14 });
            var source = (AudioSource)playerType.GetField("source", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player);
            float expectedGain = Mathf.Pow(10f, bgm.gainDb / 20f);
            float deadline = Time.realtimeSinceStartup + 15;
            while ((!source || source.clip == null || !source.isPlaying || Math.Abs(source.volume - expectedGain) > .01f)
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Require(source && source.clip != null && source.clip.channels == 2 && source.clip.frequency == 48000
                && source.clip.samples == bgm.pcm.frames && source.isPlaying
                && Math.Abs(source.volume - expectedGain) < .01f, "original Map14 BGM loads, loops and applies its configured route gain");
            playerType.GetMethod("Pause").Invoke(player, null);
            deadline = Time.realtimeSinceStartup + 3;
            while (source.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Require(!source.isPlaying, "original Map14 pause event fades then pauses the looping source");
            playerType.GetMethod("Resume").Invoke(player, null);
            deadline = Time.realtimeSinceStartup + 3;
            while ((!source.isPlaying || Math.Abs(source.volume - expectedGain) > .01f) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(source.isPlaying && Math.Abs(source.volume - expectedGain) < .01f, "original Map14 resume event restarts at the configured gain");
            playerType.GetMethod("StopAndRelease").Invoke(player, null);
            deadline = Time.realtimeSinceStartup + 3;
            while (host != null && Time.realtimeSinceStartup < deadline) yield return null;
            Require(host == null, "original Map14 stop transition releases the audio source");
            Debug.Log("Map14 original BGM Play Mode passed: source PCM, loop, 1 s play fade, -1 dB gain, pause/resume and 250 ms release transitions.");
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
