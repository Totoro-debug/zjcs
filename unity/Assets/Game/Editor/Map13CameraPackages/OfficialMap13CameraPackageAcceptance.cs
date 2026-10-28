using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Zjcs.Editor.Map13CameraPackages
{
    public static class OfficialMap13CameraPackageAcceptance
    {
        private const string Key = "Zjcs.Map13.CameraPackages";
        private static bool started, runtimeError;
        [Serializable] private sealed class Source { public ObjectSource[] objects; public Node[] nodes; }
        [Serializable] private sealed class Node { public string path, @object; public string[] components; }
        [Serializable] private sealed class ObjectSource { public string identity, type; public Fields fields; }
        [Serializable] private sealed class Fields
        {
            public string m_Name;
            public LensSettings m_Lens;
            public int m_Priority, m_StandbyUpdate, m_BindingMode, m_AngularDampingMode;
            public Vector3 m_FollowOffset;
            public float m_XDamping, m_YDamping, m_ZDamping, m_PitchDamping, m_YawDamping, m_RollDamping, m_AngularDamping;
            public CinemachineBlendDefinition m_DefaultBlend;
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180f);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false; runtimeError = false;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged; EditorApplication.playModeStateChanged += ModeChanged;
        }
        private static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = true; }
        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map13 camera/timeline engine compatibility probe timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 camera/timeline engine probe").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            IEnumerator steps = Steps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Map13 camera/timeline engine compatibility passed: original lens/transposer configuration, movement damping, animation evaluation and natural director stop. This is an engine probe; original map camera/controller and story prefab gameplay remain pending.");
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(v => v.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            foreach (Camera existing in UnityEngine.Object.FindObjectsOfType<Camera>()) existing.enabled = false;
            Time.timeScale = 1;
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/Source/world-camera.json");
            Require(text != null, "The verified original camera source is missing.");
            Source source = JsonUtility.FromJson<Source>(text.text);
            var objects = source.objects.ToDictionary(o => o.identity);
            Node free = source.nodes.Single(n => n.path == "/WorldCamera/controller/freeVCam");
            Fields lens = objects[free.components.Single(k => objects[k].type == "Cinemachine.CinemachineVirtualCamera")].fields;
            Node pipeline = source.nodes.Single(n => n.path == free.path + "/cm");
            Fields transposer = objects[pipeline.components.Single(k => objects[k].type == "Cinemachine.CinemachineTransposer")].fields;
            Fields brainFields = objects[source.nodes.Single(n => n.path == "/WorldCamera/Main Camera").components.Single(k => objects[k].type == "Cinemachine.CinemachineBrain")].fields;
            var target = new GameObject("Diagnostic target");
            var cameraObject = new GameObject("Diagnostic camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .2f, .3f, 1);
            var brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.m_DefaultBlend = brainFields.m_DefaultBlend;
            // The compatibility probe drives one frame explicitly. Production
            // camera update/controller mode remains the captured original value.
            brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.ManualUpdate;
            var vcam = new GameObject("Diagnostic original freeVCam").AddComponent<CinemachineVirtualCamera>();
            vcam.m_Lens = lens.m_Lens; vcam.Priority = lens.m_Priority;
            vcam.m_StandbyUpdate = (CinemachineVirtualCameraBase.StandbyUpdateMode)lens.m_StandbyUpdate;
            vcam.Follow = target.transform;
            var body = vcam.AddCinemachineComponent<CinemachineTransposer>();
            body.m_BindingMode = (CinemachineTransposer.BindingMode)transposer.m_BindingMode;
            body.m_FollowOffset = transposer.m_FollowOffset;
            body.m_XDamping = transposer.m_XDamping; body.m_YDamping = transposer.m_YDamping; body.m_ZDamping = transposer.m_ZDamping;
            body.m_AngularDampingMode = (CinemachineTransposer.AngularDampingMode)transposer.m_AngularDampingMode;
            body.m_PitchDamping = transposer.m_PitchDamping; body.m_YawDamping = transposer.m_YawDamping; body.m_RollDamping = transposer.m_RollDamping; body.m_AngularDamping = transposer.m_AngularDamping;
            yield return null;
            brain.ManualUpdate();
            Require(Mathf.Abs(camera.transform.position.z - transposer.m_FollowOffset.z) < .01f, "Original native camera depth was not applied.");
            Require(Mathf.Abs(camera.orthographicSize - lens.m_Lens.OrthographicSize) < .001f, "Original native lens size was not applied.");
            float initial = camera.transform.position.x;
            target.transform.position = new Vector3(4, 2, 0);
            yield return null; brain.ManualUpdate();
            Require(camera.transform.position.x > initial && camera.transform.position.x < 4, "Original transposer did not damp a moving target.");
            float until = Time.time + 1f;
            while (Time.time < until) { yield return null; brain.ManualUpdate(); }
            Require(Vector2.Distance(camera.transform.position, target.transform.position) < .02f, "Native camera follow failed to converge.");
            var animator = target.AddComponent<Animator>();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var track = timeline.CreateTrack<AnimationTrack>(null, "Engine natural-stop probe");
            var animation = new AnimationClip();
            animation.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 4, .5f, 7));
            TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
            ((AnimationPlayableAsset)clip.asset).clip = animation;
            clip.start = 0; clip.duration = .5;
            var director = target.AddComponent<PlayableDirector>();
            director.playOnAwake = false; director.playableAsset = timeline; director.extrapolationMode = DirectorWrapMode.None;
            director.SetGenericBinding(track, animator);
            int stopped = 0; bool evaluated = false;
            director.stopped += _ => stopped++;
            director.Play();
            until = Time.time + 2;
            while (stopped == 0 && Time.time < until)
            {
                yield return null; brain.ManualUpdate();
                if (target.transform.position.x > 4.1f) evaluated = true;
            }
            Require(evaluated, "Timeline animation was not evaluated by the native graph.");
            Require(stopped == 1 && director.state == PlayState.Paused, "Native director did not deliver one natural stop.");
            yield return new WaitForEndOfFrame();
            string folder = Path.GetFullPath("../.tools/map13-camera-timeline-engine"); Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "engine-playmode.png"));
            yield return null;
            UnityEngine.Object.Destroy(timeline); UnityEngine.Object.Destroy(animation);
            Require(!runtimeError, "Unity reported an error during the camera/timeline compatibility probe.");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        private sealed class Runner : MonoBehaviour { }
    }
}
