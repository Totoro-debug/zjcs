using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    /// <summary>Actual original PCM and shared dispatch. Dry test voices are not a production mixer.</summary>
    public static class OfficialBattleSoundAcceptance
    {
        private const string Key = "Zjcs.Battle.SoundAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_SOUND_STANDALONE") != "1")
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
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
            Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original battle sound verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original battle sound verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var previous = OfficialWwiseEvent.Host;
            var steps = Steps();
            try
            {
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
                    Debug.Log("Original battle sound Play Mode passed: 21 skill/21 per-cell owners, eight source cues/five native event IDs, decoded PCM import identity, original sequence/delay/loop/stop, scaled dispatch clock, owner cancellation, separate backends and UI host restoration. Original production Wwise DSP/TimeStretch and full battle-host audio binding remain pending.");
                }
                EditorApplication.isPlaying = false;
            }
            finally { (steps as IDisposable)?.Dispose(); OfficialWwiseEvent.Host = previous; }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original battle sound: " + message); }
        [Serializable] private sealed class Data { public Record[] skills, actions; }
        [Serializable] private sealed class Record { public int id; public OfficialBattleSoundCue[] cues; }

        // The fixture renders unmodified source samples solely to verify native
        // import and dispatch; it does not replace original gain/filter/FX rules.
        private sealed class Probe : IProfessionSoundHost, IDisposable
        {
            private readonly OfficialBattleSoundCatalog catalog;
            private readonly GameObject voices = new GameObject("Dry source sample verification only");
            public readonly List<uint> Ids = new List<uint>();
            public readonly List<GameObject> Emitters = new List<GameObject>();
            public Probe(OfficialBattleSoundCatalog catalog) { this.catalog = catalog; }
            public uint PostEvent(uint id, GameObject emitter)
            {
                Ids.Add(id); Emitters.Add(emitter);
                var voice = new GameObject(catalog.Event(id).name);
                voice.transform.SetParent(voices.transform, false);
                var source = voice.AddComponent<AudioSource>();
                source.clip = Resources.Load<AudioClip>(catalog.Event(id).clip_resource);
                source.spatialBlend = 0f; source.pitch = 1f;
                voice.AddComponent<SampleProbe>(); source.Play();
                return (uint)Ids.Count;
            }
            public void Dispose() { UnityEngine.Object.Destroy(voices); }
        }
        public sealed class SampleProbe : MonoBehaviour
        {
            public static int NonzeroBuffers;
            private void OnAudioFilterRead(float[] data, int channels)
            {
                foreach (float sample in data)
                    if (Math.Abs(sample) > .00001f) { Interlocked.Increment(ref NonzeroBuffers); break; }
            }
        }
        private static IEnumerator Steps()
        {
            var catalog = new OfficialBattleSoundCatalog();
            Require(catalog.Events.Count() == 5, "original five event bindings");
            foreach (var entry in catalog.Events)
            {
                var clip = Resources.Load<AudioClip>(entry.clip_resource);
                Require(clip != null && clip.channels == entry.channels && clip.frequency == entry.sample_rate
                    && clip.samples == entry.frames && clip.loadType == AudioClipLoadType.DecompressOnLoad, "native source PCM dimensions: " + entry.name);
                var values = new float[clip.samples * clip.channels]; Require(clip.GetData(values, 0), "native PCM data readable");
                var raw = new byte[values.Length * 2];
                for (int i = 0; i < values.Length; i++)
                {
                    short value = (short)Mathf.Clamp(Mathf.RoundToInt(values[i] * 32768f), short.MinValue, short.MaxValue);
                    raw[i * 2] = (byte)value; raw[i * 2 + 1] = (byte)(value >> 8);
                }
                using (var hash = SHA256.Create())
                    Require(BitConverter.ToString(hash.ComputeHash(raw)).Replace("-", "").ToLowerInvariant() == entry.pcm_sha256,
                        "unmodified imported PCM hash: " + entry.name);
            }
            var root = new GameObject("Original battle sound owners");
            var source = new GameObject("Skill source"); var target = new GameObject("Skill target");
            root.AddComponent<AudioListener>(); SampleProbe.NonzeroBuffers = 0;
            var previous = OfficialWwiseEvent.Host;
            using (var probe = new Probe(catalog))
            using (var other = new Probe(catalog))
            using (var playback = new OfficialBattleSoundPlayback(catalog, probe, root.transform))
            using (var second = new OfficialBattleSoundPlayback(catalog, other, root.transform))
            {
                try
                {
                    // A distinct UI backend must survive every synchronous call.
                    OfficialWwiseEvent.Host = other;
                    var data = JsonUtility.FromJson<Data>(Resources.Load<TextAsset>("Combat/Audio/movement").text);
                    Require(data.skills.Length == 21 && data.actions.Length == 21, "original owner selection");
                    int expected = 0;
                    foreach (bool skill in new[] { true, false })
                    foreach (var row in skill ? data.skills : data.actions)
                    {
                        var handle = skill ? playback.PlaySkill(row.id, source, target) : playback.PlayMovementAction(row.id, source, target);
                        Require(OfficialWwiseEvent.Host == other, "Play restores UI host");
                        playback.Tick(0); Require(OfficialWwiseEvent.Host == other, "paused clock restores UI host");
                        Require(probe.Ids.Count == expected + row.cues.Count(cue => cue.time == 0f), "original immediate cues only");
                        yield return null; playback.Tick(.5f);
                        Require(probe.Ids.Count == expected + row.cues.Count(cue => cue.time <= .5f), "original delayed cues remain pending");
                        yield return null; playback.Tick(.5f);
                        expected += row.cues.Length;
                        Require(probe.Ids.Count == expected, "each original source cue posts exactly once: " + row.id);
                        int start = expected - row.cues.Length;
                        for (int i = 0; i < row.cues.Length; i++)
                            Require(probe.Ids[start + i] == row.cues[i].event_id && probe.Emitters[start + i] == target,
                                "original event identity, serialized order and target owner");
                        handle?.Dispose(); Require(playback.ActiveCount == 0 && OfficialWwiseEvent.Host == other, "owner cleanup restores UI host");
                    }
                    Require(expected == 8 && other.Ids.Count == 0, "all selected source nodes use injected battle backend");
                    uint first = catalog.Events.First().id, last = catalog.Events.Last().id;
                    var delayed = playback.Play(new[] { new OfficialBattleSoundCue { time = .4f, event_id = first, duration = 10, stop_event_id = last } }, source, target);
                    delayed.Dispose();
                    yield return null; playback.Tick(1);
                    Require(probe.Ids.Count == expected, "cancelled cell suppresses pending start and stop events");
                    var loop = playback.Play(new[] { new OfficialBattleSoundCue { event_id = first, duration = 10, loop_interval = .25f, stop_event_id = last } }, source, target);
                    Require(probe.Ids.Count == ++expected, "loop posts on original Play");
                    yield return null; playback.Tick(.25f); Require(probe.Ids.Count == expected, "original loop threshold uses previous frame time");
                    yield return null; playback.Tick(.125f); Require(probe.Ids.Count == ++expected, "original loop repeats once");
                    var independent = second.Play(new[] { new OfficialBattleSoundCue { event_id = first, duration = 10 } }, source, null);
                    var sameHost = playback.Play(new[] { new OfficialBattleSoundCue { time = .5f, event_id = first } }, source, target);
                    Require(other.Ids.Count == 1 && other.Emitters[0] != source && other.Emitters[0] != target
                        && other.Emitters[0].name == "OriginalBattleSound", "no-target emitter is playable GO, not source");
                    loop.Dispose(); Require(probe.Ids.Count == ++expected && probe.Ids.Last() == last && independent.IsPlaying, "cell end posts configured stop without stopping other host");
                    loop.Dispose(); Require(probe.Ids.Count == expected && sameHost.IsPlaying, "repeated owner stop preserves another unit's pending sound");
                    yield return null; playback.Tick(.5f);
                    Require(probe.Ids.Count == ++expected, "stopping one unit preserves the shared scheduler and another unit's cue");
                    sameHost.Dispose(); playback.Stop(); Require(probe.Ids.Count == expected, "Stop after owner cleanup is idempotent");
                    independent.Dispose(); Require(second.ActiveCount == 0, "separate host cleanup");
                    var atSpeed = playback.Play(new[] { new OfficialBattleSoundCue { time = .75f, event_id = first } }, source, target);
                    yield return null; playback.Tick(.5f); Require(probe.Ids.Count == expected, "scaled clock before configured cue");
                    yield return null; playback.Tick(.25f); Require(probe.Ids.Count == ++expected, "scaled clock crosses original cue");
                    atSpeed.Dispose();
                    // Allow the actual audio thread to consume the dry test voice.
                    yield return new WaitForSecondsRealtime(.2f);
                    Require(Volatile.Read(ref SampleProbe.NonzeroBuffers) > 0, "original decoded clips produce nonzero native audio buffers");
                    Require(OfficialWwiseEvent.Host == other, "all native callbacks restore unrelated UI host");
                    Debug.Log("Original battle sound evidence: eight source posts, five exact native PCM hashes, nonzero audio buffers=" + SampleProbe.NonzeroBuffers);
                }
                finally
                {
                    OfficialWwiseEvent.Host = previous;
                    UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(source); UnityEngine.Object.Destroy(target);
                }
            }
            yield return null;
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
