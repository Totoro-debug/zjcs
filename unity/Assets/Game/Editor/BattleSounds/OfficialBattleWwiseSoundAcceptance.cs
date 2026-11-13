using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.BattleNativePlayables;
using SoundObject = Zjcs.Official.ProfessionUI.AkGameObj;
using PortedEventReference = Zjcs.Official.ProfessionUI.WwiseEventReference;
using NativeListener = global::AkAudioListener;
using NativeSoundObject = global::AkGameObj;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleWwiseSoundAcceptance
    {
        private const string Key = "Zjcs.Battle.NativeWwiseAcceptance";
        private static bool started;
        private static int errors;
        public static void Run() => RunAcceptance(false);
        public static void RunFormalController() => RunAcceptance(true);
        private static void RunAcceptance(bool controllerOnly)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")))
                throw new InvalidOperationException("Use the isolated validation workspace.");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            if (controllerOnly)
                Type.GetType("Zjcs.Editor.Combat.BattleGameViewInput, Zjcs.Editor.Combat", true)
                    .GetMethod("Focus").Invoke(null, null);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".controllerOnly", controllerOnly);
            SessionState.SetInt(Key + ".result", 1);
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
            if (AkSoundEngine.IsInitialized())
            {
                SessionState.SetInt(Key + ".result", 1);
                Debug.LogError("Application sound runtime did not terminate when Play Mode ended.");
            }
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Native Wwise host verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Native Wwise verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
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
                    Debug.Log("NATIVE_WWISE_HOST_PLAYMODE_PASSED controllerOnly=" + SessionState.GetBool(Key + ".controllerOnly", false));
                }
                EditorApplication.isPlaying = false;
            }
            finally { (steps as IDisposable)?.Dispose(); }
        }
        private static IEnumerator Steps()
        {
            if (SessionState.GetBool(Key + ".controllerOnly", false))
            {
                var controllerSteps = ControllerSteps();
                try { while (controllerSteps.MoveNext()) yield return controllerSteps.Current; }
                finally { (controllerSteps as IDisposable)?.Dispose(); }
                yield break;
            }
            string directory = Path.Combine(Application.dataPath, "Official/Combat/NativeSound/Packages");
            var previous = OfficialWwiseEvent.Host;
            var unrelated = new OtherHost();
            try
            {
                for (int lifetime = 0; lifetime < 2; lifetime++)
                {
                    var listener = new GameObject("Wwise listener interface verification");
                    var emitter = new GameObject("Wwise emitter interface verification");
                    var reference = ScriptableObject.CreateInstance<PortedEventReference>();
                    reference.id = 1557230835u; // Original Skill_10010_1.
                    var sound = new OfficialWwiseEvent { WwiseObjectReference = reference };
                    try
                    {
                        using (var host = new OfflineBattleWwiseSoundHost(directory, listener, true))
                        {
                            VectorReturns();
                            OfficialWwiseEvent.Host = host;
                            uint playing = sound.Post(emitter);
                            Require(playing != 0 && playing == sound.PlayingId, "original event returns its native playing ID");
                            OfficialWwiseEvent.Host = unrelated;
                            Require(emitter.GetComponent<SoundObject>() != null, "native Post adds original AkGameObj");
                            host.SetVolumes(.427f, .316f, .638f, .749f);
                            foreach (float speed in new[] { 1f, 2.3f, 0f })
                            {
                                host.SetSpeed(speed);
                                emitter.transform.position = new Vector3(3 + speed, 4, 5);
                                listener.transform.position = new Vector3(-1, 2 + speed, -3);
                                yield return null;
                                host.RenderFrame();
                                yield return null;
                                Position(emitter); Position(listener);
                                Rtpc(414249091u, speed);
                                Rtpc(3240620755u, 42); Rtpc(3659619896u, 31);
                                Rtpc(3676256460u, 63); Rtpc(3673881719u, 74);
                                Require(OfficialWwiseEvent.Host == unrelated, "cached object updates preserve unrelated UI backend");
                            }
                            host.SetSpeed(1);
                            Require(host.PostEvent(reference.id, emitter) != 0, "registered emitter can be reused");
                            host.RenderFrame();
                            UnityEngine.Object.Destroy(emitter);
                            yield return null;
                            host.RenderFrame();
                        }
                        Require(!AkSoundEngine.IsInitialized(), "native engine terminates before next lifetime");
                    }
                    finally
                    {
                        UnityEngine.Object.Destroy(emitter); UnityEngine.Object.Destroy(listener);
                        UnityEngine.Object.Destroy(reference);
                    }
                    yield return null;
                }
                var bgmSteps = BgmSteps();
                try { while (bgmSteps.MoveNext()) yield return bgmSteps.Current; }
                finally { (bgmSteps as IDisposable)?.Dispose(); }
                var listenerSteps = ListenerSteps();
                try { while (listenerSteps.MoveNext()) yield return listenerSteps.Current; }
                finally { (listenerSteps as IDisposable)?.Dispose(); }
                var runtimeSteps = RuntimeSteps();
                try { while (runtimeSteps.MoveNext()) yield return runtimeSteps.Current; }
                finally { (runtimeSteps as IDisposable)?.Dispose(); }
                var musicScenes = BgmSceneSteps();
                try { while (musicScenes.MoveNext()) yield return musicScenes.Current; }
                finally { (musicScenes as IDisposable)?.Dispose(); }
            }
            finally { OfficialWwiseEvent.Host = previous; }
        }
        private static IEnumerator ListenerSteps()
        {
            Scene originalScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene("Original listener verification " + Guid.NewGuid().ToString("N"));
            var camera = new GameObject("Main Camera");
            camera.SetActive(false);
            SceneManager.MoveGameObjectToScene(camera, scene);
            var prefab = Resources.Load<GameObject>("Combat/Wwise/AudioListener");
            Require(prefab != null, "original listener prefab is explicitly bound");
            GameObject listener = UnityEngine.Object.Instantiate(prefab, camera.transform, false);
            var previous = OfficialWwiseEvent.Host;
            try
            {
                Require(listener.GetComponent<NativeListener>() != null && listener.GetComponent<NativeSoundObject>() != null,
                    "prefab binds both original SDK scripts");
                Require(listener.transform.localPosition == new Vector3(0, 0, 20)
                    && listener.transform.localRotation == Quaternion.identity && listener.transform.localScale == Vector3.one,
                    "original listener local camera pose");
                Require(NativeListener.DefaultListeners.ListenerList.Count == 0, "default listener list starts empty");
                using (var host = new OfflineBattleWwiseSoundHost(OfflineBattleWwisePackages.DirectoryPath, listener, true))
                {
                    OfficialWwiseEvent.Host = host;
                    camera.SetActive(true);
                    yield return null;
                    Require(!listener.activeSelf && NativeListener.DefaultListeners.ListenerList.Count == 0,
                        "original scene controller disables an inactive-scene listener");
                    Require(SceneManager.SetActiveScene(scene), "activate listener scene");
                    yield return null;
                    Require(listener.activeSelf && NativeListener.DefaultListeners.ListenerList.Count == 1,
                        "original scene activation enables its default listener");
                    camera.transform.position = new Vector3(7, 9, -20);
                    yield return null;
                    host.RenderFrame();
                    // Native audio consumes queued registration/pose commands on
                    // its own audio thread; the query observes the consumed frame.
                    yield return new WaitForSecondsRealtime(.05f);
                    Require(listener.transform.position == new Vector3(7, 9, 0), "listener follows original camera child offset");
                    Position(listener);
                    Require(SceneManager.SetActiveScene(originalScene), "deactivate listener scene");
                    yield return null;
                    Require(!listener.activeSelf && NativeListener.DefaultListeners.ListenerList.Count == 0,
                        "scene exit removes original default listener");
                    Require(SceneManager.SetActiveScene(scene), "reactivate listener scene");
                    yield return null;
                    Require(listener.activeSelf && NativeListener.DefaultListeners.ListenerList.Count == 1,
                        "scene reentry restores original default listener");
                    UnityEngine.Object.Destroy(camera);
                    yield return null;
                    Require(NativeListener.DefaultListeners.ListenerList.Count == 0, "destroy unregisters original listener");
                    host.RenderFrame();
                }
                Require(!AkSoundEngine.IsInitialized(), "prefab lifetime releases native engine");
                Debug.Log("NATIVE_WWISE_ORIGINAL_LISTENER_PASSED original prefab, z=20 camera child, real scene enter/exit/reentry and destruction");
            }
            finally
            {
                OfficialWwiseEvent.Host = previous;
                SceneManager.SetActiveScene(originalScene);
                UnityEngine.Object.Destroy(camera);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
        private static IEnumerator RuntimeSteps()
        {
            var camera = new GameObject("Main Camera runtime ownership verification");
            var emitter = new GameObject("Runtime original event emitter");
            var previous = OfficialWwiseEvent.Host;
            try
            {
                OfflineBattleWwiseRuntime.ConfigureVolumes(.427f, .316f, .638f, .749f);
                IProfessionSoundHost first;
                using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, 1000))
                {
                    first = lease.SoundHost;
                    Require(AkSoundEngine.IsInitialized(), "application runtime initializes the native engine");
                    yield return null;
                    Require(NativeListener.DefaultListeners.ListenerList.Count == 1, "lease mounts original default listener");
                    foreach (float scale in new[] { 1f, 2.3f, 0f })
                    {
                        Time.timeScale = scale;
                        yield return null;
                        yield return null;
                        Rtpc(414249091u, scale);
                    }
                    Rtpc(3240620755u, 42); Rtpc(3659619896u, 31);
                    Rtpc(3676256460u, 63); Rtpc(3673881719u, 74);
                    lease.SetVisible(false);
                    Require(NativeListener.DefaultListeners.ListenerList.Count == 0, "hidden lease removes original listener");
                    lease.SetVisible(true);
                    Require(NativeListener.DefaultListeners.ListenerList.Count == 1, "visible lease restores original listener");
                    OfflineBattleWwiseRuntime.ConfigureVolumes(.517f, .246f, .345f, .456f);
                    Time.timeScale = 1;
                    yield return null;
                    yield return null;
                    Rtpc(3240620755u, 51); Rtpc(3659619896u, 24);
                    Rtpc(3676256460u, 34); Rtpc(3673881719u, 45);
                    Require(first.PostEvent(1557230835u, emitter) != 0, "application-owned event dispatch remains native");
                    UnityEngine.Object.Destroy(emitter);
                    yield return null;
                }
                yield return null;
                Require(AkSoundEngine.IsInitialized() && NativeListener.DefaultListeners.ListenerList.Count == 0,
                    "closing a battle releases its listener and retains the application engine");
                using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, 1000))
                {
                    Require(ReferenceEquals(first, lease.SoundHost), "next battle reuses the same initialized native host");
                    yield return null;
                    yield return null;
                    Rtpc(3240620755u, 51);
                }
                yield return null;
                Require(AkSoundEngine.IsInitialized(), "idle application retains native package and RTPC state");
                Debug.Log("NATIVE_WWISE_APPLICATION_RUNTIME_PASSED two original listener leases, retained engine/volumes, actual 1/2.3/0 time scales and native event dispatch");
            }
            finally
            {
                Time.timeScale = 1;
                OfficialWwiseEvent.Host = previous;
                UnityEngine.Object.Destroy(emitter); UnityEngine.Object.Destroy(camera);
            }
        }
        private static void Rtpc(uint id, float expected)
        {
            int type = (int)AkQueryRTPCValue.RTPCValue_Global;
            var result = AkSoundEngine.GetRTPCValue(id, AkSoundEngine.AK_INVALID_GAME_OBJECT, 0, out float actual, ref type);
            Require(result == AKRESULT.AK_Success && Math.Abs(actual - expected) < .0001f,
                "native global RTPC " + id + ": result=" + result + ", actual=" + actual + ", expected=" + expected);
        }
        private static void VectorReturns()
        {
            var expected = new Vector3(1.25f, -2.5f, 3.75f);
            CheckVector(AkSoundEngine.ConvertAkVectorToAkVector64(expected), expected);
            using (var pose = new AkWorldTransform())
            {
                pose.SetPosition(expected.x, expected.y, expected.z);
                CheckVector(pose.Position(), expected);
            }
            using (var source = new AkImageSourceParams())
            {
                source.sourcePosition = expected;
                CheckVector(source.sourcePosition, expected);
            }
            using (var reflection = new AkReflectionPathInfo())
            {
                reflection.imageSource = expected;
                CheckVector(reflection.imageSource, expected);
            }
            using (var diffraction = new AkDiffractionPathInfo())
            {
                diffraction.emitterPos = expected;
                CheckVector(diffraction.emitterPos, expected);
            }
            Debug.Log("WWISE_NATIVE_VECTOR_RETURNS_PASSED four borrowed fields and one value conversion");
        }
        private static void CheckVector(AkVector64 actual, Vector3 expected)
        {
            Require(actual.X == expected.x && actual.Y == expected.y && actual.Z == expected.z,
                "original double-vector return preserves its values");
        }
        private static void Position(GameObject value)
        {
            using (var pose = new AkWorldTransform())
            {
                Require(AkSoundEngine.GetPosition(AkSoundEngine.GetAkGameObjectID(value), pose) == AKRESULT.AK_Success,
                    "native sound pose is queryable");
                AkVector64 position = pose.Position();
                Require(Math.Abs(position.X - value.transform.position.x) < .0001
                    && Math.Abs(position.Y - value.transform.position.y) < .0001
                    && Math.Abs(position.Z - value.transform.position.z) < .0001, "native position follows Unity transform");
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Native Wwise host: " + message); }
        private sealed class OtherHost : IProfessionSoundHost
        { public uint PostEvent(uint id, GameObject emitter) => throw new InvalidOperationException("Unrelated UI host received battle audio."); }
        private sealed class Runner : MonoBehaviour { }
    }
}
