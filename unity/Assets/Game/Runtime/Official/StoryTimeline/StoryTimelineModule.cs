using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Zjcs.Official.StoryMasks;

namespace Zjcs.Official.StoryTimeline
{
    public interface IStoryTimelineHost : IStoryTimelineBindings
    {
        GameObject LoadPrefab(string guid);
        Transform SceneRoot(int scene);
        IStoryMaskManager Masks { get; }
        void EnterStoryCamera();
        void ExitStoryCamera();
    }

    [Serializable]
    public sealed class StoryTimelineRequest
    {
        [Serializable] public sealed class AssetGuid { public string mAssetGuid; }
        public AssetGuid StoryPrefabGuid;
        public int NeedMask, PlayableScene = 3, TimeOutSafeEnd;
        public float OptionTime;
        [NonSerialized] public bool OutputConnected;
        [NonSerialized] public Action<PlayableDirector> Output;
        [NonSerialized] public StoryTimelineTrackBinding[] TrackBindings;
    }

    public readonly struct StoryTimelineTrackBinding
    {
        public readonly int Index;
        public readonly object Value;
        public readonly Type ValueType;
        public StoryTimelineTrackBinding(int index, object value, Type valueType)
        { Index = index; Value = value; ValueType = valueType; }
    }

    // PlayStoryTimelineNode.InternalRun/WaitPlayableDirectorPlay/OnEnd lowered
    // from the selected original async state machines to an owned coroutine.
    // A disposed wait always detaches stopped and releases its scene instance.
    public sealed class StoryTimelineModule : IDisposable
    {
        private readonly IStoryTimelineHost host;
        private readonly List<GameObject> retained = new List<GameObject>();
        private GameObject active;
        private IEnumerator pending;
        private bool running, disposed;
        public bool SafeEnd { get; private set; }
        public StoryTimelineModule(IStoryTimelineHost host) => this.host = host ?? throw new ArgumentNullException(nameof(host));

        public IEnumerator Play(StoryTimelineRequest request)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StoryTimelineModule));
            if (pending != null || running) throw new InvalidOperationException("A story Timeline wait is already running.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            pending = Run(request);
            return pending;
        }

        private IEnumerator Run(StoryTimelineRequest request)
        {
            SafeEnd = false;
            running = true;
            PlayableDirector director = null;
            Action<PlayableDirector> stopped = null;
            bool completed = false;
            GameObject staging = null;
            try
            {
                if (request.StoryPrefabGuid == null) throw new InvalidOperationException("Original story Timeline prefab reference is absent.");
                Transform root = host.SceneRoot(request.PlayableScene);
                if (!root) throw new InvalidOperationException("Original story Timeline scene root is absent: " + request.PlayableScene);
                GameObject prefab = host.LoadPrefab(request.StoryPrefabGuid.mAssetGuid);
                if (!prefab) throw new InvalidOperationException("Original story Timeline prefab is not bound: " + request.StoryPrefabGuid.mAssetGuid);
                staging = new GameObject("Story Timeline dependency binding");
                staging.SetActive(false);
                staging.transform.SetParent(root, false);
                active = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                active.SetActive(false);
                foreach (var binding in active.GetComponentsInChildren<CinemachineTrackStoryBinder>(true)) binding.Bind(host);
                foreach (var binding in active.GetComponentsInChildren<VirtualCameraFollowBinder>(true)) binding.Bind(host);
                active.transform.SetParent(root, false);
                UnityEngine.Object.Destroy(staging); staging = null;
                if (!active.TryGetComponent(out director)) throw new InvalidOperationException("Original story Timeline has no PlayableDirector.");
                request.Output?.Invoke(director);
                var timeline = director.playableAsset as TimelineAsset;
                foreach (var binding in request.TrackBindings ?? Array.Empty<StoryTimelineTrackBinding>())
                {
                    var track = timeline.GetOutputTrack(binding.Index);
                    if (track && !director.GetGenericBinding(track) && binding.Value != null)
                        director.SetGenericBinding(track, (UnityEngine.Object)Convert.ChangeType(binding.Value, binding.ValueType));
                }
                host.Masks.Reset();
                if (request.NeedMask != 0) host.Masks.SetInvisibleMask(true);
                active.SetActive(true);
                if (director.state != PlayState.Playing)
                {
                    host.EnterStoryCamera();
                    director.Play();
                }
                if (director.state != PlayState.Paused && !request.OutputConnected)
                {
                    bool hasStopped = false;
                    stopped = _ => hasStopped = true;
                    director.stopped += stopped;
                    double timeout = director.duration + 5.0;
                    float start = Time.time;
                    while (!hasStopped)
                    {
                        if (disposed) yield break;
                        if (Time.time - start >= timeout)
                        {
                            SafeEnd = request.TimeOutSafeEnd != 0;
                            yield break;
                        }
                        yield return null;
                    }
                    director.stopped -= stopped; stopped = null;
                }
                if (!Mathf.Approximately(request.OptionTime, 0))
                {
                    if (request.OptionTime > 0) yield return new WaitForSeconds(request.OptionTime);
                    else while (!disposed && director && director.state == PlayState.Playing) yield return null;
                }
                if (disposed) yield break;
                completed = true;
            }
            finally
            {
                if (director && stopped != null) director.stopped -= stopped;
                host.ExitStoryCamera();
                if (active)
                {
                    if (completed && request.OutputConnected && !disposed) retained.Add(active);
                    else { active.SetActive(false); UnityEngine.Object.Destroy(active); }
                }
                if (staging) UnityEngine.Object.Destroy(staging);
                active = null;
                running = false;
                pending = null;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            (pending as IDisposable)?.Dispose(); pending = null;
            if (active) { active.SetActive(false); UnityEngine.Object.Destroy(active); }
            foreach (var instance in retained)
                if (instance) { instance.SetActive(false); UnityEngine.Object.Destroy(instance); }
            retained.Clear();
        }
    }
}
