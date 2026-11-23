// Selected original home entity registration/timeline implementation; see its source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityRuntime
{
using Object = UnityEngine.Object;
public class HomeTimelinePlayable : MonoBehaviour, IHomeTimelinePlayable
{
	public TimelineAsset DefaultTimelineAsset;

	public HomePlayableBinding PlayableBinding;

	private PlayableGraph mPlayableGraph;

	private Dictionary<string, AnimationMixerPlayable> mTimelineMixPlayableDict = new Dictionary<string, AnimationMixerPlayable>();

	private Dictionary<AnimationClip, AnimationMixerPlayable> mClipMixPlayableDict = new Dictionary<AnimationClip, AnimationMixerPlayable>();

	private List<Playable> mClipPlayables = new List<Playable>();

	private double mPlayableDuration;

	private double mPlayableTime;

	private TimelineAsset mCurrentTimelineAsset;

	private int mCurrentInputIndex;

	private int mNextInputIndex;

	private bool mEnabled;

	private CancellationTokenSource mCancellationTokenSource;

	private float mTransitionTime;

	public bool IsValid => mPlayableGraph.IsValid();

	public bool InTransition { get; private set; }

	private void OnEnable()
	{
		InitIfNeeded();
		mPlayableGraph.Evaluate();
		mEnabled = true;
	}

	private void OnDisable()
	{
		mEnabled = false;
	}

	private void OnDestroy()
	{
		mPlayableGraph.Destroy();
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
		}
	}

	private void InitIfNeeded()
	{
		if (!IsValid)
		{
			mPlayableGraph = PlayableGraph.Create(((Object)this).name);
			mPlayableGraph.SetTimeUpdateMode((DirectorUpdateMode)3);
			mCurrentTimelineAsset = DefaultTimelineAsset;
			mCurrentInputIndex = 0;
			mNextInputIndex = 1;
			mPlayableDuration = ((PlayableAsset)DefaultTimelineAsset).duration;
			AddTimelineAsset(DefaultTimelineAsset, mCurrentInputIndex, 1f);
			mPlayableGraph.Play();
		}
	}

	public void DoUpdate(float normalizeTime)
	{
		InitIfNeeded();
		SetPlayableTime(normalizeTime);
		if (mEnabled)
		{
			mPlayableGraph.Evaluate();
		}
	}

	public void AddClipPlayable(AnimationClip clip, Animator animator)
	{
		InitIfNeeded();
		if (mClipMixPlayableDict.ContainsKey(clip))
		{

			Debug.LogWarning("Official HomeTimelinePlayable.AddClipPlayable");
		}
		else
		{
			string name = ((Object)clip).name;
			AnimationMixerPlayable val = CreateMixerPlayable(name, animator);
			AnimationClipPlayable val2 = CreateClipPlayable(clip, 0, 1f, val);
			mClipPlayables.Add((Playable)val2);
			mClipMixPlayableDict.Add(clip, val);
		}
	}

	public void RemoveClipPlayable(AnimationClip clip)
	{
		if (!IsValid)
		{
			return;
		}
		if (!mClipMixPlayableDict.TryGetValue(clip, out var value))
		{

			Debug.LogWarning("Official HomeTimelinePlayable.RemoveClipPlayable");
		}
		else
		{
			Playable input = PlayableExtensions.GetInput<AnimationMixerPlayable>(value, 0);
			mClipPlayables.Remove(input);
			mPlayableGraph.DestroyPlayable<Playable>(input);
			mPlayableGraph.DestroyPlayable<AnimationMixerPlayable>(value);
			mClipMixPlayableDict.Remove(clip);
		}
	}

	public void SetPlayableTime(float normalizeTime)
	{
		if (!((Object)(object)mCurrentTimelineAsset == (Object)null))
		{
			mPlayableTime = mPlayableDuration * (double)normalizeTime;
			SetClipPlayableTime(mPlayableTime);
		}
	}

	private void SetClipPlayableTime(double time)
	{
		foreach (Playable mClipPlayable in mClipPlayables)
		{
			PlayableExtensions.SetTime<Playable>(mClipPlayable, time);
		}
	}

	private void AddTimelineAsset(TimelineAsset timelineAsset, int index, float weight)
	{
		foreach (TrackAsset outputTrack in timelineAsset.GetOutputTracks())
		{
			PlayableBinding val = ((PlayableAsset)outputTrack).outputs.First();
			Object sourceObject = val.sourceObject;
			AnimationTrack val2 = (AnimationTrack)(object)((sourceObject is AnimationTrack) ? sourceObject : null);
			if (val2 != null)
			{
				string name = ((Object)outputTrack).name;
				if (!mTimelineMixPlayableDict.TryGetValue(name, out var value))
				{
					Animator bindingAnimator = PlayableBinding.GetBindingAnimator(name);
					value = CreateMixerPlayable(name, bindingAnimator);
					mTimelineMixPlayableDict.Add(name, value);
				}
				AnimationClip val3 = val2.infiniteClip;
				if ((Object)(object)val3 == (Object)null)
				{
					TimelineClip val4 = ((TrackAsset)val2).GetClips().FirstOrDefault();
					val3 = ((val4 != null) ? val4.animationClip : null);
				}
				if (!((Object)(object)val3 == (Object)null))
				{
					AnimationClipPlayable val5 = CreateClipPlayable(val3, index, weight, value);
					mClipPlayables.Add((Playable)val5);
				}
			}
		}
	}

	private AnimationMixerPlayable CreateMixerPlayable(string playableName, Animator animator)
	{
		AnimationPlayableOutput val = AnimationPlayableOutput.Create(mPlayableGraph, playableName, animator);
		AnimationMixerPlayable val2 = AnimationMixerPlayable.Create(mPlayableGraph, 2);
		PlayableOutputExtensions.SetSourcePlayable<AnimationPlayableOutput, AnimationMixerPlayable>(val, val2);
		return val2;
	}

	private AnimationClipPlayable CreateClipPlayable(AnimationClip clip, int index, float weight, AnimationMixerPlayable mixerPlayable)
	{
		AnimationClipPlayable val = AnimationClipPlayable.Create(mPlayableGraph, clip);
		mPlayableGraph.Connect<AnimationClipPlayable, AnimationMixerPlayable>(val, 0, mixerPlayable, index);
		PlayableExtensions.SetInputWeight<AnimationMixerPlayable>(mixerPlayable, index, weight);
		return val;
	}

	public void TranslateTimeline(TimelineAsset timelineAsset, float duration, Action onComplete)
	{
		if (!IsValid)
		{
			return;
		}
		if (InTransition)
		{

			Debug.LogWarning("Official HomeTimelinePlayable.TranslateTimeline");
		}
		else
		{
			AddTimelineAsset(timelineAsset, mNextInputIndex, 0f);
			SetClipPlayableTime(mPlayableTime);
			InTransition = true;
			mCancellationTokenSource = new CancellationTokenSource();
			UniTaskVoid val = ExecuteTransition(duration, mCancellationTokenSource.Token, onComplete);
			val.Forget();
		}
	}
	private async UniTaskVoid ExecuteTransition(float duration, CancellationToken token, Action onComplete)
    {
        mTransitionTime = 0f;
        while (mTransitionTime < duration)
        {
            mTransitionTime += Time.deltaTime;
            float weight = Mathf.Clamp01(mTransitionTime / duration);
            SetMixPlayableInputWeight(mCurrentInputIndex, 1f - weight, mNextInputIndex, weight);
            await UniTask.Yield(token, false);
        }
        foreach (KeyValuePair<string, AnimationMixerPlayable> pair in mTimelineMixPlayableDict)
        {
            AnimationMixerPlayable mixer = pair.Value;
            Playable input = PlayableExtensions.GetInput<AnimationMixerPlayable>(mixer, mCurrentInputIndex);
            mClipPlayables.Remove(input);
            mPlayableGraph.DestroyPlayable<Playable>(input);
            mPlayableGraph.DestroyPlayable<AnimationMixerPlayable>(mixer);
        }
        SwapInputIndex();
        onComplete?.Invoke();
        InTransition = false;
    }

	private void SetMixPlayableInputWeight(int index1, float weight1, int index2, float weight2)
	{
		string text = default;
		AnimationMixerPlayable val = default;
		foreach (KeyValuePair<string, AnimationMixerPlayable> item in mTimelineMixPlayableDict)
		{
			item.Deconstruct(out text, out val);
			AnimationMixerPlayable val2 = val;
			PlayableExtensions.SetInputWeight<AnimationMixerPlayable>(val2, index1, weight1);
			PlayableExtensions.SetInputWeight<AnimationMixerPlayable>(val2, index2, weight2);
		}
	}

	private void SwapInputIndex()
	{
		int num = mNextInputIndex;
		int num2 = mCurrentInputIndex;
		mCurrentInputIndex = num;
		mNextInputIndex = num2;
	}
}
}
