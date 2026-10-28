// Migrated official source; see map14-story-animator-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Zjcs.Official.StoryAnimation
{
public class AnimationController : MonoBehaviour
{
	public struct AnimatorParamBuffer<T>
	{
		public int Unique;

		public T Value;

		public bool IsEndResume;
	}

	public bool InitOnAwake;

	public Animator myAnimator;

	public bool CreateOverrideController;

	public bool NotCreateGraph;

	[SerializeField]
	private bool mResetAnimatorInitedOnDisable;

	private AnimationMixerPlayable myMixer;

	private List<int> myIndexList = new List<int>();

	private bool myGraphInited = true;

	private bool myAnimatorInited;

	private RuntimeAnimatorController myRuntimeAnimatorController;

	private int mPauseCounter;

	private float mPreAnimSpeed = 1f;

	private HashSet<string> mSetAnimatorClipWarnedClipNames;

	private Dictionary<int, StateMergeList<bool>> mBoolParam = new Dictionary<int, StateMergeList<bool>>();

	private Dictionary<int, StateMergeList<float>> mFloatParam = new Dictionary<int, StateMergeList<float>>();

	private Dictionary<int, StateMergeList<int>> mIntParam = new Dictionary<int, StateMergeList<int>>();

	private Dictionary<int, List<AnimatorParamBuffer<bool>>> mBoolBuffers;

	private Dictionary<int, List<AnimatorParamBuffer<float>>> mFloatBuffers;

	private Dictionary<int, List<AnimatorParamBuffer<int>>> mIntBuffers;

	private Action<int, int, bool, bool> mInitBoolParamAction;

	private Action<int, int, float, bool> mInitFloatParamAction;

	private Action<int, int, int, bool> mInitIntegerParamAction;

	private static readonly List<KeyValuePair<AnimationClip, AnimationClip>> sOverrideList = new List<KeyValuePair<AnimationClip, AnimationClip>>();

	public bool myInited
	{
		get
		{
			if (myGraphInited)
			{
				return myAnimatorInited;
			}
			return false;
		}
	}

	public PlayableGraph myGraph { get; set; }

	public RuntimeAnimatorController RuntimeAnimatorController => myRuntimeAnimatorController;

	public event Action OnRuntimeAnimatorControllerChanged;

	private void Awake()
	{
		if (InitOnAwake)
		{
			Init();
		}
	}

	private void OnDisable()
	{
		if (mResetAnimatorInitedOnDisable)
		{
			ClearAllBuffParams();
			myAnimatorInited = false;
		}
	}

	public void Init()
	{
		if (!((Object)(object)myAnimator == (Object)null) && !((Object)(object)myAnimator.runtimeAnimatorController == (Object)null))
		{
			SetRuntimeAnimationController(myAnimator.runtimeAnimatorController);
		}
	}

	public void SetBool(int uniqueKey, int paramKey, bool paramValue, bool isEndResume)
	{
		if ((Object)(object)myAnimator == (Object)null || mPauseCounter > 0)
		{
			return;
		}
		if (!myAnimatorInited)
		{
			AddBuffer(ref mBoolBuffers, uniqueKey, paramKey, paramValue, isEndResume);
			return;
		}
		if (isEndResume)
		{
			if (!mBoolParam.TryGetValue(paramKey, out var value))
			{
				value = new StateMergeList<bool>(myAnimator.GetBool(paramKey));
				mBoolParam.Add(paramKey, value);
			}
			value.Add((uniqueKey, paramValue));
		}
		myAnimator.SetBool(paramKey, paramValue);
	}

	public void SetFloat(int uniqueKey, int paramKey, float paramValue, bool isEndResume)
	{
		if ((Object)(object)myAnimator == (Object)null || mPauseCounter > 0)
		{
			return;
		}
		if (!myAnimatorInited)
		{
			AddBuffer(ref mFloatBuffers, uniqueKey, paramKey, paramValue, isEndResume);
			return;
		}
		if (isEndResume)
		{
			if (!mFloatParam.TryGetValue(paramKey, out var value))
			{
				value = new StateMergeList<float>(myAnimator.GetFloat(paramKey));
				mFloatParam.Add(paramKey, value);
			}
			value.Add((uniqueKey, paramValue));
		}
		myAnimator.SetFloat(paramKey, paramValue);
	}

	public void SetInteger(int uniqueKey, int paramKey, int paramValue, bool isEndResume)
	{
		if ((Object)(object)myAnimator == (Object)null || mPauseCounter > 0)
		{
			return;
		}
		if (!myAnimatorInited)
		{
			AddBuffer(ref mIntBuffers, uniqueKey, paramKey, paramValue, isEndResume);
			return;
		}
		if (isEndResume)
		{
			if (!mIntParam.TryGetValue(paramKey, out var value))
			{
				value = new StateMergeList<int>(myAnimator.GetInteger(paramKey));
				mIntParam.Add(paramKey, value);
			}
			value.Add((uniqueKey, paramValue));
		}
		myAnimator.SetInteger(paramKey, paramValue);
	}

	public void SetTrigger(int uniqueKey, int paramKey)
	{
		if (!((Object)(object)myAnimator == (Object)null) && myAnimatorInited && mPauseCounter <= 0)
		{
			myAnimator.SetTrigger(paramKey);
		}
	}

	public void ResumeBool(int uniqueKey, int paramKey)
	{
		if ((Object)(object)myAnimator == (Object)null)
		{
			return;
		}
		if (!myAnimatorInited)
		{
			RemoveBuffer(mBoolBuffers, uniqueKey, paramKey);
		}
		if (mBoolParam.TryGetValue(paramKey, out var value))
		{
			value.Remove(uniqueKey);
			myAnimator.SetBool(paramKey, value.CurState);
			if (value.Count <= 0)
			{
				mBoolParam.Remove(paramKey);
			}
		}
	}

	public void ResumeFloat(int uniqueKey, int paramKey)
	{
		if ((Object)(object)myAnimator == (Object)null)
		{
			return;
		}
		if (!myAnimatorInited)
		{
			RemoveBuffer(mFloatBuffers, uniqueKey, paramKey);
		}
		if (mFloatParam.TryGetValue(paramKey, out var value))
		{
			value.Remove(uniqueKey);
			myAnimator.SetFloat(paramKey, value.CurState);
			if (value.Count <= 0)
			{
				mFloatParam.Remove(paramKey);
			}
		}
	}

	public void ResumeInteger(int uniqueKey, int paramKey)
	{
		if ((Object)(object)myAnimator == (Object)null)
		{
			return;
		}
		if (!myAnimatorInited)
		{
			RemoveBuffer(mIntBuffers, uniqueKey, paramKey);
		}
		if (mIntParam.TryGetValue(paramKey, out var value))
		{
			value.Remove(uniqueKey);
			myAnimator.SetInteger(paramKey, value.CurState);
			if (value.Count <= 0)
			{
				mIntParam.Remove(paramKey);
			}
		}
	}

	public void Pause()
	{
		if (mPauseCounter == 0)
		{
			mPreAnimSpeed = myAnimator.speed;
			myAnimator.speed = 0f;
		}
		mPauseCounter++;
	}

	public void Resume()
	{
		if (mPauseCounter != 0)
		{
			mPauseCounter--;
			if (mPauseCounter == 0)
			{
				myAnimator.speed = mPreAnimSpeed;
			}
		}
	}

	public void ForceResumeAllPause()
	{
		if (mPauseCounter > 0)
		{
			mPauseCounter = 0;
			if ((Object)(object)myAnimator != (Object)null)
			{
				myAnimator.speed = mPreAnimSpeed;
			}
		}
	}

	public void SetRuntimeAnimationController(RuntimeAnimatorController controller)
	{
		if ((Object)(object)myRuntimeAnimatorController == (Object)(object)controller)
		{
			InitAnimation();
			return;
		}
		myRuntimeAnimatorController = (CreateOverrideController ? CreateNewOverrideController(controller) : controller);
		myAnimator.runtimeAnimatorController = myRuntimeAnimatorController;
		InitAnimation();
		OnRuntimeAnimatorControllerChanged?.Invoke();
	}

	public void InitAnimation()
	{
		myAnimatorInited = true;
		InitSetParamsAction();
		SetParams(mBoolBuffers, mInitBoolParamAction);
		SetParams(mFloatBuffers, mInitFloatParamAction);
		SetParams(mIntBuffers, mInitIntegerParamAction);
		ClearAllBuffParams();
	}

	private void ClearAllBuffParams()
	{
		if (mBoolBuffers != null)
		{
			mBoolBuffers.Clear();
		}
		if (mFloatBuffers != null)
		{
			mFloatBuffers.Clear();
		}
		if (mIntBuffers != null)
		{
			mIntBuffers.Clear();
		}
	}

	private void InitSetParamsAction()
	{
		if (mInitBoolParamAction == null)
		{
			mInitBoolParamAction = SetBool;
		}
		if (mInitFloatParamAction == null)
		{
			mInitFloatParamAction = SetFloat;
		}
		if (mInitIntegerParamAction == null)
		{
			mInitIntegerParamAction = SetInteger;
		}
	}

	public void SetAnimatorClip(string originClipName, AnimationClip overrideClip)
	{
		RuntimeAnimatorController val = myRuntimeAnimatorController;
		AnimatorOverrideController val2 = (AnimatorOverrideController)(object)((val is AnimatorOverrideController) ? val : null);
		if (val2 != null)
		{
			val2[originClipName] = overrideClip;
			return;
		}
		if (mSetAnimatorClipWarnedClipNames == null)
		{
			mSetAnimatorClipWarnedClipNames = new HashSet<string>();
		}
		if (mSetAnimatorClipWarnedClipNames.Add(originClipName))
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
	}

	private RuntimeAnimatorController CreateNewOverrideController(RuntimeAnimatorController runtimeAnimatorController)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected Obj, but got Unknown
		AnimatorOverrideController val = new AnimatorOverrideController(runtimeAnimatorController)
		{
			name = ((Object)runtimeAnimatorController).name
		};
		AnimatorOverrideController val2 = (AnimatorOverrideController)(object)((runtimeAnimatorController is AnimatorOverrideController) ? runtimeAnimatorController : null);
		if (val2 != null)
		{
			val2.GetOverrides(sOverrideList);
			val.ApplyOverrides((IList<KeyValuePair<AnimationClip, AnimationClip>>)sOverrideList);
			sOverrideList.Clear();
		}
		return (RuntimeAnimatorController)(object)val;
	}

	public int PlayClip(AnimationClip clip, float speed = 1f)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (!myInited)
		{
			return -1;
		}
		AnimationClipPlayable val = AnimationClipPlayable.Create(myGraph, clip);
		PlayableExtensions.SetSpeed<AnimationClipPlayable>(val, (double)speed);
		int num = -1;
		for (int i = 1; i < PlayableExtensions.GetInputCount<AnimationMixerPlayable>(myMixer); i++)
		{
			if (!myIndexList.Contains(i))
			{
				PlayableExtensions.ConnectInput<AnimationMixerPlayable, AnimationClipPlayable>(myMixer, i, val, 0);
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			num = PlayableExtensions.AddInput<AnimationMixerPlayable, AnimationClipPlayable>(myMixer, val, 0, 0f);
		}
		myIndexList.Add(num);
		return num;
	}

	public void StopClip(int index)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (index > 0)
		{
			Playable input = PlayableExtensions.GetInput<AnimationMixerPlayable>(myMixer, index);
			PlayableExtensions.DisconnectInput<AnimationMixerPlayable>(myMixer, index);
			PlayableExtensions.Destroy<Playable>(input);
			myIndexList.Remove(index);
		}
	}

	public void SetClipWeight(int index, float weight)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		if (index > 0)
		{
			PlayableExtensions.SetInputWeight<AnimationMixerPlayable>(myMixer, index, weight);
		}
	}

	private void AddBuffer<T>(ref Dictionary<int, List<AnimatorParamBuffer<T>>> paramBuffers, int unique, int paramKey, T value, bool isEndResume)
	{
		if (paramBuffers == null)
		{
			paramBuffers = new Dictionary<int, List<AnimatorParamBuffer<T>>>();
		}
		if (!paramBuffers.TryGetValue(paramKey, out var value2))
		{
			value2 = new List<AnimatorParamBuffer<T>>();
			paramBuffers.Add(paramKey, value2);
		}
		RemoveSameUniqueParamKey(value2, unique);
		value2.Add(new AnimatorParamBuffer<T>
		{
			IsEndResume = isEndResume,
			Unique = unique,
			Value = value
		});
	}

	private void RemoveSameUniqueParamKey<T>(List<AnimatorParamBuffer<T>> buffers, int unique)
	{
		if (buffers == null)
		{
			return;
		}
		for (int num = buffers.Count - 1; num >= 0; num--)
		{
			if (buffers[num].Unique == unique)
			{
				buffers.RemoveAt(num);
			}
		}
	}

	private void RemoveBuffer<T>(Dictionary<int, List<AnimatorParamBuffer<T>>> paramBuffers, int unique, int paramKey)
	{
		if (paramBuffers != null && paramBuffers.TryGetValue(paramKey, out var value))
		{
			RemoveSameUniqueParamKey(value, unique);
		}
	}

	private void SetParams<T>(Dictionary<int, List<AnimatorParamBuffer<T>>> paramBuffers, Action<int, int, T, bool> setParamsAction)
	{
		if (paramBuffers == null)
		{
			return;
		}
		foreach (KeyValuePair<int, List<AnimatorParamBuffer<T>>> paramBuffer in paramBuffers)
		{
			foreach (AnimatorParamBuffer<T> item in paramBuffer.Value)
			{
				setParamsAction?.Invoke(item.Unique, paramBuffer.Key, item.Value, item.IsEndResume);
			}
		}
	}
}

}
