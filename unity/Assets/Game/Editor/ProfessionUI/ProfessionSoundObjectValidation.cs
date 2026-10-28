using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionSoundObjectValidation
    {
        private const string Active = "Zjcs.ProfessionSoundObject.Active";
        private static bool ready;
        private static int phase, frame;
        private static RecordingHost recording;
        private static AkGameObj[] emitters;
        private static GameObject root;
        private static OfflineProfessionSoundHost host;
        private static AudioSource voice;
        private static double posted;
        private static bool advancing, audible;
        static ProfessionSoundObjectValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 150));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Sound-object validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                if (phase == 0) { Initialize(); phase = 1; frame = Time.frameCount + 3; return; }
                if (Time.frameCount < frame) return;
                if (phase == 1)
                {
                    Require(recording.Positions == 3 && recording.Environments > 3, "stationary objects update environment but suppress identical positions");
                    emitters[0].transform.position = new Vector3(11, -7, 2);
                    emitters[0].transform.rotation = Quaternion.Euler(20, 30, 40);
                    emitters[0].transform.localScale = new Vector3(3, 4, 5);
                    emitters[0].m_positionOffsetData.positionOffset = new Vector3(2, 3, 4);
                    phase = 2; frame = Time.frameCount + 2; return;
                }
                if (phase == 2)
                {
                    var item = emitters[0];
                    Vector3 expected = item.transform.position + item.transform.rotation * item.m_positionOffsetData.positionOffset;
                    Require(recording.Positions == 4 && (recording.Position - expected).sqrMagnitude < 1e-8 && (recording.Forward - item.transform.forward).sqrMagnitude < 1e-8 && (recording.Up - item.transform.up).sqrMagnitude < 1e-8, "position/rotation offset ignores transform scale and KeepMe flag");
                    int count = recording.Positions;
                    item.transform.position += Vector3.right * 1e-6f; item.SetPosition();
                    Require(recording.Positions == count, "source epsilon suppresses sub-threshold motion");
                    item.transform.position += Vector3.right * 0.001f; item.SetPosition();
                    Require(recording.Positions == count + 1, "source epsilon accepts larger motion");
                    item.gameObject.SetActive(false);
                    Require(recording.Unregisters == 0, "disable retains original registration");
                    Object.DestroyImmediate(root); root = null;
                    Require(recording.Unregisters == 3, "three source objects unregister at destroy");
                    CheckFailure();
                    BeginNativeAudio(); phase = 3; frame = Time.frameCount + 1; return;
                }
                if (phase == 3)
                {
                    Require(host.RegisteredObjectCount == 3, "native field carriers register with actual Windows host");
                    if (voice && voice.isPlaying)
                    {
                        advancing |= voice.timeSamples > 0;
                        var samples = new float[1024]; voice.GetOutputData(samples, 0);
                        audible |= samples.Any(x => Mathf.Abs(x) > 1e-7f);
                    }
                    if (EditorApplication.timeSinceStartup - posted < 0.15 || !advancing || !audible) return;
                    Object.DestroyImmediate(root); root = null;
                    Require(host.RegisteredObjectCount == 0, "native objects unregister from real local playback host");
                    Object.DestroyImmediate(host.gameObject); host = null;
                    Debug.Log("Original AkGameObj source branch passed Direct3D Play Mode: all three native field carriers, registration/position/environment/listener/scaling order, unchanged-position cache and source threshold, real-frame transform/rotation offset, disable/destroy behavior and original failed-registration flag. All three bind to the Windows host; a source catalog PCM event advances and produces nonzero audio samples. Full page, general Wwise environments/listeners and Player remain unvalidated.");
                    Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static AkGameObj[] InstantiateCarriers()
        {
            root = new GameObject("Source sound object carriers");
            var result = new AkGameObj[3];
            for (int i = 0; i < 3; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProfessionSoundObjectProbe/" + i + ".prefab");
                Require(prefab, "native carrier import " + i);
                var source = prefab.GetComponent<AkGameObj>();
                Require(source && source.m_listeners.useDefaultListeners && source.m_listeners.initialListenerList.Count == 0 && source.isEnvironmentAware && !source.isStaticObject && source.listenerMask == 1 && source.m_posOffsetData == null && source.m_positionOffsetData != null && !source.m_positionOffsetData.KeepMe && source.m_positionOffsetData.positionOffset == Vector3.zero, "all native serialized fields " + i);
                result[i] = Object.Instantiate(prefab, root.transform, false).GetComponent<AkGameObj>();
                result[i].gameObject.SetActive(true);
            }
            return result;
        }
        private static void Initialize()
        {
            recording = new RecordingHost(); OfficialWwiseEvent.Host = recording;
            emitters = InstantiateCarriers();
            Require(recording.Registers == 3 && recording.Positions == 3, "Awake registers and positions exactly once");
            string expected = "register,position,environment-init,environment-update,listeners,scale";
            Require(recording.Events.Count == 18 && string.Join(",", recording.Events.Take(6)) == expected && string.Join(",", recording.Events.Skip(6).Take(6)) == expected && string.Join(",", recording.Events.Skip(12)) == expected, "original Awake call sequence for all three native objects");
            foreach (var emitter in emitters) Require(emitter.Register() == 1 && emitter.enabled, "registered success and dynamic OnEnable");
            Require(recording.Registers == 3, "repeat Register does not reach backend");
        }
        private static void CheckFailure()
        {
            recording.Failure = true;
            int registers = recording.Registers, positions = recording.Positions;
            var failed = new GameObject("Original failed registration"); failed.SetActive(false);
            var component = failed.AddComponent<AkGameObj>(); failed.SetActive(true);
            Require(recording.Registers == registers + 1 && recording.Positions == positions && component.Register() == 1 && recording.Registers == registers + 1, "failed first registration retains flag and early returns from Awake");
            Object.DestroyImmediate(failed); recording.Failure = false;
        }
        private static void BeginNativeAudio()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfflineProfessionAudioCatalog>("Assets/Official/ProfessionAudio/ProfessionAudio.asset");
            Require(catalog && catalog.Events.Length == 5, "existing five-event original PCM catalog");
            host = new GameObject("Windows source sound host", typeof(AudioListener)).AddComponent<OfflineProfessionSoundHost>(); host.Bind(catalog);
            emitters = InstantiateCarriers();
            var entry = catalog.Events.OrderByDescending(x => x.Clip.length).First();
            Require(entry.Clip.length > 0.2f, "source PCM provides an audio observation interval");
            uint playingId = host.PostEvent(entry.EventId, emitters[0].gameObject);
            voice = Object.FindObjectsOfType<AudioSource>().Single(x => x.clip == entry.Clip);
            Require(playingId != 0 && voice.spatialBlend == 0 && !voice.loop && voice.clip == entry.Clip && voice.isPlaying, "original event ID reaches actual PCM source");
            posted = EditorApplication.timeSinceStartup;
        }
        private sealed class RecordingHost : IProfessionSoundHost, IProfessionSoundObjectHost
        {
            public readonly List<string> Events = new List<string>();
            public bool Failure; public int Registers, Positions, Environments, Unregisters;
            public Vector3 Position, Forward, Up;
            public bool IsInitialized => true;
            public float DefaultScalingFactor => 1;
            public uint PostEvent(uint eventId, GameObject emitter) => throw new InvalidOperationException("No synthetic audio.");
            public int RegisterGameObject(GameObject emitter, string name) { Require(name == emitter.name, "original game-object name"); Events.Add("register"); Registers++; return Failure ? 2 : 1; }
            public void SetObjectPosition(GameObject emitter, Vector3 position, Vector3 forward, Vector3 up) { Events.Add("position"); Positions++; Position = position; Forward = forward; Up = up; }
            public void InitializeEmptyEnvironment(GameObject emitter, Collider collider) { Require(!collider, "source has no collider"); Events.Add("environment-init"); }
            public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position) { Require(position == emitter.transform.position, "environment uses transform position"); Events.Add("environment-update"); Environments++; }
            public void SetDefaultListeners(GameObject emitter) { Events.Add("listeners"); }
            public void SetScalingFactor(GameObject emitter, float factor) { Require(factor == 1, "original missing-settings scaling fallback"); Events.Add("scale"); }
            public void UnregisterGameObject(GameObject emitter) { Unregisters++; }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Sound object: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            if (root) Object.DestroyImmediate(root);
            if (host) Object.DestroyImmediate(host.gameObject);
            foreach (var source in Object.FindObjectsOfType<OfflineProfessionSoundVoice>()) Object.DestroyImmediate(source.gameObject);
            OfficialWwiseEvent.Host = null;
            EditorApplication.Exit(code);
        }
    }
}
