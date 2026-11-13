namespace Zjcs.Official.BattleThemeUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animation))]
public class AnimationSequence : MonoBehaviour
{
	[SerializeField]
	private Animation mAnimation;

	[SerializeField]
	private List<AnimationClipData> mAnimDataList = new List<AnimationClipData>();

	[SerializeField]
	private bool mPlayOnEnable = true;

	private int mIdx = -1;

	private int mLoopTimes;

	private Coroutine mMonitorCoroutine;

	private Coroutine mRePlayCoroutine;

	public Action<AnimationClip> OnAnimStart;

	public Action<AnimationClip> OnAnimEnd;

	public Action OnAllAnimCompleted;

	private void OnEnable()
	{
		if (mPlayOnEnable)
		{
			Play();
		}
	}

	private void OnDisable()
	{
		StopMonitor();
		StopRePlayCoroutine();
	}

	public void NextAnim()
	{
		StopMonitor();
		AnimationClipData curAnimData = GetCurAnimData();
		if (curAnimData.Valid && (mLoopTimes > 0 || curAnimData.LoopTimes < 0))
		{
			mLoopTimes = 0;
			StopCurrentClip(curAnimData);
			OnAnimEnd?.Invoke(curAnimData.Clip);
			if (mIdx == mAnimDataList.Count - 1)
			{
				OnAllAnimCompleted?.Invoke();
			}
		}
		PlayAnim(mIdx + 1);
	}

	public void PlaySequence(List<AnimationClipData> sequence)
	{
		Stop();
		mAnimDataList = sequence;
		Play();
	}

	public void Play()
	{
		if (EnsureAnimation() && mAnimDataList.Count != 0)
		{
			PlayAnim(0);
		}
	}

	public void SetAnimation(Animation animation)
	{
		mAnimation = animation;
	}

	private void PlayAnim(int dataIdx)
	{
		if (EnsureAnimation() && dataIdx <= mAnimDataList.Count - 1)
		{
			mIdx = dataIdx;
			AnimationClipData curAnimData = GetCurAnimData();
			if (!curAnimData.Valid)
			{
				PlayAnim(dataIdx + 1);
				return;
			}
			OnAnimStart?.Invoke(curAnimData.Clip);
			PlayClip(curAnimData);
			StopMonitor();
			mMonitorCoroutine = ((MonoBehaviour)this).StartCoroutine(MonitorCurrentClip(curAnimData));
		}
	}

	private void PlayClip(AnimationClipData data)
	{
		string name = ((Object)data.Clip).name;
		mAnimation[name].wrapMode = (WrapMode)((data.LoopTimes >= 0) ? 1 : 2);
		mAnimation.Play(name);
	}

	private IEnumerator MonitorCurrentClip(AnimationClipData data)
	{
		string clipName = ((Object)data.Clip).name;
		yield return WaitUntilClipPlayingCoroutine(clipName);
		if (!mAnimation.IsPlaying(clipName))
		{
			mMonitorCoroutine = null;
			yield break;
		}
		if (data.LoopTimes < 0)
		{
			mMonitorCoroutine = null;
			yield break;
		}
		do
		{
			yield return WaitUntilClipFinishedCoroutine(data, clipName);
			mLoopTimes++;
			if (data.LoopTimes - mLoopTimes <= 0)
			{
				break;
			}
			PlayClip(data);
			yield return WaitUntilClipPlayingCoroutine(clipName);
		}
		while (mAnimation.IsPlaying(clipName));
		mMonitorCoroutine = null;
		NextAnim();
	}

	private IEnumerator WaitUntilClipPlayingCoroutine(string clipName)
	{
		float elapsed = 0f;
		while (!mAnimation.IsPlaying(clipName) && elapsed < 1f)
		{
			elapsed += Time.deltaTime;
			yield return null;
		}
	}

	private IEnumerator WaitUntilClipFinishedCoroutine(AnimationClipData data, string clipName)
	{
		float clipLength = Mathf.Max(data.Clip.length, 0.01f);
		float elapsed = 0f;
		while (mAnimation.IsPlaying(clipName) || elapsed < clipLength)
		{
			elapsed += Time.deltaTime;
			yield return null;
		}
	}

	private void StopCurrentClip(AnimationClipData data)
	{
		if (data.Valid)
		{
			mAnimation.Stop(((Object)data.Clip).name);
		}
	}

	private AnimationClipData GetCurAnimData()
	{
		return GetAnimData(mIdx);
	}

	private AnimationClipData GetAnimData(int idx)
	{
		if (idx < 0 || idx >= mAnimDataList.Count)
		{
			return default;
		}
		return mAnimDataList[idx];
	}

	private bool EnsureAnimation()
	{
		if ((Object)(object)mAnimation == (Object)null)
		{
			mAnimation = ((Component)this).GetComponent<Animation>();
		}
		return (Object)(object)mAnimation != (Object)null;
	}

	private void StopMonitor()
	{
		if (mMonitorCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mMonitorCoroutine);
			mMonitorCoroutine = null;
		}
	}

	private void StopRePlayCoroutine()
	{
		if (mRePlayCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mRePlayCoroutine);
			mRePlayCoroutine = null;
		}
	}

	private void Stop()
	{
		StopMonitor();
		StopRePlayCoroutine();
		if (EnsureAnimation())
		{
			mAnimation.Stop();
		}
		mLoopTimes = 0;
		mIdx = -1;
	}

	private IEnumerator RePlayCoroutine()
	{
		Stop();
		yield return null;
		mRePlayCoroutine = null;
		Play();
	}
}

}
