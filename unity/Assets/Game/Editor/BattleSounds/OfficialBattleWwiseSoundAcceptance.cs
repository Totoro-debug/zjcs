using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.BattleNativePlayables;
using SoundObject = Zjcs.Official.ProfessionUI.AkGameObj;

namespace Zjcs.Editor
{
    public static class OfficialBattleWwiseSoundAcceptance
    {
        private const string Key = "Zjcs.Battle.NativeWwiseAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")))
                throw new InvalidOperationException("Use the isolated validation workspace.");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true);
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
                    Debug.Log("NATIVE_WWISE_HOST_PLAYMODE_PASSED: two engine lifetimes, original event/interface dispatch, cached object host, moving emitters/default listener, empty sends, five global RTPC values, stop/unregister and silent output.");
                }
                EditorApplication.isPlaying = false;
            }
            finally { (steps as IDisposable)?.Dispose(); }
        }
        private static IEnumerator Steps()
        {
            string directory = Path.Combine(Application.dataPath, "Official/Combat/NativeSound/Packages");
            var previous = OfficialWwiseEvent.Host;
            var unrelated = new OtherHost();
            try
            {
                for (int lifetime = 0; lifetime < 2; lifetime++)
                {
                    var listener = new GameObject("Wwise listener interface verification");
                    var emitter = new GameObject("Wwise emitter interface verification");
                    var reference = ScriptableObject.CreateInstance<WwiseEventReference>();
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
            }
            finally { OfficialWwiseEvent.Host = previous; }
        }
        private static void Rtpc(uint id, float expected)
        {
            int type = (int)AkQueryRTPCValue.RTPCValue_Global;
            Require(AkSoundEngine.GetRTPCValue(id, AkSoundEngine.AK_INVALID_GAME_OBJECT, 0, out float actual, ref type)
                == AKRESULT.AK_Success && Math.Abs(actual - expected) < .0001f, "native global RTPC " + id);
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
