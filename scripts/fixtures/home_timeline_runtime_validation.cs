using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Zjcs.Official.HomeEntityRuntime;

public sealed partial class HomeEntityRuntimeChecks
{
    TimelineAsset FixedTimeline(string name, AnimationClip clip = null)
    {
        var asset = Keep(ScriptableObject.CreateInstance<TimelineAsset>());
        asset.name = name; asset.durationMode = TimelineAsset.DurationMode.FixedLength; asset.fixedDuration = 2;
        if (clip) {
            var track = Keep(asset.CreateTrack<AnimationTrack>(null, "OriginalTrackBindingFixture"));
            var timelineClip = track.CreateClip<AnimationPlayableAsset>();
            Keep(timelineClip.asset); ((AnimationPlayableAsset)timelineClip.asset).clip = clip;
        }
        return asset;
    }
    AnimationClip Curve(string name, float from, float to)
    {
        var clip = Keep(new AnimationClip { name = name });
        clip.SetCurve("ClockPart", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, from, 2, to));
        return clip;
    }
    HomeTimelinePlayable Timeline(string name, TimelineAsset asset, out GameObject part)
    {
        var root = New(name); part = New("ClockPart", root.transform); part.SetActive(true);
        var animator = root.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var binding = root.AddComponent<HomePlayableBinding>();
        binding.AnimatorBindings.Add(new PlayableAnimatorBinding { Key = "OriginalTrackBindingFixture", Animator = animator });
        var timeline = root.AddComponent<HomeTimelinePlayable>(); timeline.DefaultTimelineAsset = asset; timeline.PlayableBinding = binding;
        return timeline;
    }
    IEnumerator CheckTimeline()
    {
        var clip = Curve("OriginalDynamicClipFixture", 0, 2);
        var timeline = Timeline("DynamicPlayableFixture", FixedTimeline("EmptyDefaultFixture"), out var part);
        bool called = false;
        timeline.RemoveClipPlayable(clip); timeline.TranslateTimeline(FixedTimeline("UninitializedTransitionFixture"), 0, () => called = true);
        Require(!timeline.IsValid && !called, "Uninitialized remove/transition preserve the original early returns");
        timeline.gameObject.SetActive(true); yield return null;
        Require(timeline.IsValid, "Timeline OnEnable initializes a real manually updated native graph");
        timeline.AddClipPlayable(clip, timeline.GetComponent<Animator>()); timeline.DoUpdate(.25f);
        Require(Mathf.Abs(part.transform.localPosition.x - .5f) < .02f, "Dynamic clip time follows original default duration times normalized time");
        timeline.AddClipPlayable(clip, timeline.GetComponent<Animator>());
        Require(Field<Dictionary<AnimationClip, AnimationMixerPlayable>>(timeline, "mClipMixPlayableDict").Count == 1,
            "Duplicate clip registration retains one mixer and emits the original warning");
        timeline.enabled = false; float oldPose = part.transform.localPosition.x; timeline.DoUpdate(.75f);
        Require(Mathf.Abs(part.transform.localPosition.x - oldPose) < .001f, "Disabled timeline updates times without evaluating the graph");
        timeline.enabled = true;
        Require(Mathf.Abs(part.transform.localPosition.x - 1.5f) < .02f, "Re-enable evaluates the time retained while disabled");
        timeline.RemoveClipPlayable(clip); timeline.RemoveClipPlayable(clip);
        Require(Field<Dictionary<AnimationClip, AnimationMixerPlayable>>(timeline, "mClipMixPlayableDict").Count == 0 &&
            Field<List<Playable>>(timeline, "mClipPlayables").Count == 0, "Clip removal destroys its native input/mixer and removes both registrations");
        var graph = Field<PlayableGraph>(timeline, "mPlayableGraph"); UnityEngine.Object.Destroy(timeline.gameObject); yield return null;
        Require(!graph.IsValid(), "Timeline destruction releases the real native graph");

        var first = FixedTimeline("FirstTrackFixture", Curve("FirstTrackClipFixture", 0, 2));
        var second = FixedTimeline("SecondTrackFixture", Curve("SecondTrackClipFixture", 2, 4));
        var tracked = Timeline("TrackTransitionFixture", first, out var trackedPart);
        Require(tracked.PlayableBinding.GetBindingAnimator("absent") == null, "Animator binding uses the original exact-key lookup");
        tracked.gameObject.SetActive(true); yield return null; tracked.DoUpdate(.5f);
        Require(Mathf.Abs(trackedPart.transform.localPosition.x - 1) < .02f, "Original animation-track first-clip branch binds and evaluates on its named animator");
        int completions = 0; bool callbackSawTransition = false; bool duplicate = false;
        tracked.TranslateTimeline(second, .1f, () => { completions++; callbackSawTransition = tracked.InTransition; });
        Require(tracked.InTransition && completions == 0, "Nonempty transition enters the original asynchronous state before completion");
        tracked.TranslateTimeline(second, .1f, () => duplicate = true);
        float deadline = Time.realtimeSinceStartup + 5;
        while (tracked.InTransition && Time.realtimeSinceStartup < deadline) yield return null;
        Require(completions == 1 && callbackSawTransition && !tracked.InTransition && !duplicate,
            "Transition rejects concurrent requests and invokes completion before clearing its state");
        Require(Field<int>(tracked, "mCurrentInputIndex") == 1 && Field<int>(tracked, "mNextInputIndex") == 0,
            "Completed transition swaps original input indices");
        var mixers = Field<Dictionary<string, AnimationMixerPlayable>>(tracked, "mTimelineMixPlayableDict");
        Require(mixers.Count == 1 && !mixers["OriginalTrackBindingFixture"].IsValid(),
            "Verified source transition destroys its native timeline mixer; no unverified repair is substituted");
        UnityEngine.Object.Destroy(tracked.gameObject); yield return null;

        var immediate = Timeline("ImmediateTransitionFixture", FixedTimeline("ImmediateDefaultFixture"), out _);
        immediate.gameObject.SetActive(true); called = false; callbackSawTransition = false;
        immediate.TranslateTimeline(FixedTimeline("ImmediateNextFixture"), 0, () => { called = true; callbackSawTransition = immediate.InTransition; });
        Require(called && callbackSawTransition && !immediate.InTransition, "Zero-duration transition preserves synchronous callback/state ordering");
        bool cancelledCallback = false;
        immediate.TranslateTimeline(FixedTimeline("CancelledNextFixture"), 10, () => cancelledCallback = true);
        Require(immediate.InTransition, "Long transition has a pending native owner lifetime");
        var cancelledGraph = Field<PlayableGraph>(immediate, "mPlayableGraph"); UnityEngine.Object.Destroy(immediate.gameObject);
        yield return null; yield return null;
        Require(!cancelledGraph.IsValid() && !cancelledCallback, "Owner destruction cancels the pending transition and releases its graph");
    }
}
