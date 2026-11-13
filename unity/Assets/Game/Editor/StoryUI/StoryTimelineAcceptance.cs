using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Zjcs.Official.StoryTimeline;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.StoryUI
{
    [InitializeOnLoad]
    public static class StoryTimelineAcceptance
    {
        private const string Key = "Zjcs.StoryTimeline.Contract";
        static StoryTimelineAcceptance()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("Original standalone Timeline validation").AddComponent<StoryTimelineRunner>();
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")))
                throw new InvalidOperationException("Use an owned validation workspace.");
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        public static void Finish(Exception error)
        {
            SessionState.SetBool(Key, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("STORY_TIMELINE_PASSED: native Earthquake1, three original shots, actual Cinemachine camera/follow/noise, stopped wait, output lifetime and canceled wait cleanup. Full chapter/input/save/Windows Player remain pending.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class StoryTimelineRunner : MonoBehaviour
    {
        private int errors;
        private IEnumerator Start()
        {
            AudioListener.volume = 0;
            Application.logMessageReceived += (_, __, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            var test = Verify();
            try
            {
                while (true)
                {
                    object current;
                    try { if (!test.MoveNext()) break; current = test.Current; }
                    catch (Exception error) { StoryTimelineAcceptance.Finish(error); yield break; }
                    yield return current;
                }
            }
            finally { (test as IDisposable)?.Dispose(); }
            StoryTimelineAcceptance.Finish(errors == 0 ? null : new InvalidOperationException("Timeline runtime errors: " + errors));
        }
        private IEnumerator Verify()
        {
            var root = new GameObject("Original Timeline scene");
            var player = new GameObject("Current player transform binding");
            player.transform.SetParent(root.transform, false);
            var camera = new GameObject("WorldCamera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 7;
            camera.transform.position = new Vector3(0, 0, -250);
            camera.nearClipPlane = .3f; camera.farClipPlane = 5000;
            var target = new RenderTexture(720, 1280, 24); camera.targetTexture = target;
            var terrainAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/12/NativeScene/Partition/MapGround_12/MapGround_12_bare.prefab");
            Require(terrainAsset, "original native mountain ground is available");
            // This fixture exercises the Timeline, not the partition loader.
            // Instantiate an authored mesh subtree with its original material
            // and world transform; the full root requires the map's services.
            var source = terrainAsset.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(row =>
                row.bounds.size.x > 1 && row.bounds.size.y > 1 && row.GetComponentsInChildren<MonoBehaviour>(true).Length == 0);
            Require(source, "native mountain fixture has a mesh subtree without map-service dependencies");
            var terrain = Instantiate(source.gameObject, root.transform, false);
            terrain.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            terrain.transform.localScale = source.transform.lossyScale;
            var renderer = terrain.GetComponent<Renderer>();
            Require(renderer, "native mountain fixture has renderable ground");
            player.transform.position = renderer.bounds.center;
            camera.transform.position = player.transform.position + new Vector3(0, 0, -250);
            var overlay = new GameObject("Native story overlay", typeof(RectTransform), typeof(Canvas));
            overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
            overlay.GetComponent<Canvas>().worldCamera = camera;
            var maskHost = NativeStoryMaskHost.Create(overlay.transform);
            yield return null;
            using (var host = new NativeStoryTimelineHost(root.transform, camera, player.transform, maskHost.Manager))
            using (var module = new StoryTimelineModule(host))
            {
                PlayableDirector director = null;
                var request = Request(); request.Output = value => director = value;
                var wait = module.Play(request);
                try
                {
                    Require(wait.MoveNext(), "original stopped wait stays pending after actual Play");
                    Require(director && director.state == PlayState.Playing, "native Timeline actually plays");
                    var timeline = (TimelineAsset)director.playableAsset;
                    var track = timeline.GetOutputTracks().Single();
                    Require(track.GetClips().Count() == 3, "original three camera shots retained");
                    Require(director.GetGenericBinding(track) == camera.GetComponent<CinemachineBrain>(), "original camera-key binder resolves WorldCamera brain");
                    var views = director.GetComponentsInChildren<CinemachineVirtualCamera>();
                    Require(views.Length == 2 && views.All(view => view.Follow == player.transform), "original managed-reference selectors follow the current player");
                    Require(views.All(view => view.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset.z == -250), "original ZSettings camera-stage offset is applied");
                    var noise = views.Select(view => view.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>()).FirstOrDefault(value => value);
                    Require(noise, "original shake component is bound");
                    if (!noise.m_NoiseProfile)
                        Debug.LogWarning("Original NoiseSettings asset was serialized and retained, but this package revision did not resolve it at runtime.");
                    Require(maskHost.Manager.EnableInvisibleMask, "original NeedMask blocks input during playback");
                    float deadline = Time.realtimeSinceStartup + 15;
                    bool moved = false; Vector3 previous = camera.transform.position;
                    int frames = 0;
                    while (wait.MoveNext())
                    {
                        Require(Time.realtimeSinceStartup < deadline, "actual Timeline stopped event arrives before validation deadline");
                        yield return wait.Current;
                        moved |= Vector3.Distance(previous, camera.transform.position) > .0001f;
                        previous = camera.transform.position;
                        if (++frames == 15) Capture(camera, target, "story-timeline-shake");
                    }
                    Require(frames > 15 && !module.SafeEnd, "actual Timeline completes through its stopped event");
                }
                finally { (wait as IDisposable)?.Dispose(); }
                yield return null;
                Require(!director, "non-output Timeline instance is destroyed at node end");
                // A caller retaining the director does not wait for stopped and
                // keeps the authored instance until whole-story cleanup.
                request = Request(); request.OutputConnected = true; request.Output = value => director = value;
                wait = module.Play(request);
                Require(!wait.MoveNext() && director && director.state == PlayState.Playing, "connected output preserves the playing director after node end");
                module.Dispose(); yield return null;
                Require(!director, "whole-story cleanup destroys retained output");
            }
            using (var host = new NativeStoryTimelineHost(root.transform, camera, player.transform, maskHost.Manager))
            using (var module = new StoryTimelineModule(host))
            {
                PlayableDirector director = null;
                var request = Request(); request.Output = value => director = value;
                var wait = module.Play(request);
                Require(wait.MoveNext(), "cancel fixture reaches actual stopped wait");
                ((IDisposable)wait).Dispose(); yield return null;
                Require(!director, "disposing a pending wait destroys its native Timeline and detaches stopped");
                wait = module.Play(Request()); Require(wait.MoveNext(), "same module starts a new request after canceled wait");
                ((IDisposable)wait).Dispose();
            }
            camera.targetTexture = null;
            target.Release(); Destroy(target); Destroy(root); Destroy(camera.gameObject); Destroy(overlay);
        }
        private static StoryTimelineRequest Request() => new StoryTimelineRequest
        { StoryPrefabGuid = new StoryTimelineRequest.AssetGuid { mAssetGuid = "75dbb924ad6de1f46a6696666cc083ae" }, NeedMask = 1, PlayableScene = 3 };
        private static void Require(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
        private static void Capture(Camera camera, RenderTexture target, string name)
        {
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Destroy(image); }
        }
    }
}
