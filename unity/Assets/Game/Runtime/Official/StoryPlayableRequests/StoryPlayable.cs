// Migrated original client request body; see playable requests receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.ProfessionUI;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableRequests
{
[Serializable]
public class StoryPlayable
{
	[Tooltip("源物体Key")]
	public string SourceKey;

	[Tooltip("目标物体Key")]
	public string TargetKey;

	[Tooltip("要播放的Playable")]
	public AssetGuidProxy<GameObject> Playable;

	[Tooltip("是否等待Playable停止")]
	public bool IsWaitPlayableStop;

	[Tooltip("Playable持续时间，如果设置-1则按照Playable原本时长播放")]
	public float PlayableDura = -1f;

	[Tooltip("是否在跳过时执行")]
	public bool IsRunWhenSkip;

	private MyPlayable mPlayable;

	private bool mIsPlayingPlayable;

	private Action mOnPlayalbeEnd;

	private IStoryHandleProxy mStoryHandleProxy;

	private StoryPlayableResourceBag mDisposableBag;
	public IStoryNodeRegisterMgr NodeRegisterManager { get; set; }
	public IProfessionImageResourceLoader ResourceLoader { get; set; }

	public void Init(IStoryHandleProxy storyHandleProxy, Action onPlayableEnd)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		mOnPlayalbeEnd = (Action)Delegate.Combine(mOnPlayalbeEnd, onPlayableEnd);
		mStoryHandleProxy = storyHandleProxy;
		mDisposableBag = new StoryPlayableResourceBag();
	}

	public void Clear()
	{
		mOnPlayalbeEnd = null;
		mIsPlayingPlayable = false;
		mPlayable = null;
		mDisposableBag.Dispose();
	}

	public void RunPlayable()
	{
		mIsPlayingPlayable = true;
		PlayPlayable();
		if (IsWaitPlayableStop && (Object)(object)mPlayable != (Object)null)
		{
			mPlayable.OnStop += new Action<MyPlayable>(OnPlayableStop);
		}
		else
		{
			mOnPlayalbeEnd?.Invoke();
		}
	}

	private void PlayPlayable()
	{
		if (Playable == null)
		{
			return;
		}
		GameObject node = NodeRegisterManager.GetNode(SourceKey);
		GameObject node2 = NodeRegisterManager.GetNode(TargetKey);
		GameObject result = mDisposableBag.Add(ResourceLoader.LoadAssetByGuid<GameObject>(Playable.AssetGuid)).Result;
		mPlayable = mStoryHandleProxy.PlayPlayable(result, node, node2);
		if ((Object)(object)mPlayable != (Object)null && PlayableDura > 0f)
		{
			mPlayable.SetAutoStopCountDown(PlayableDura);
		}
		else if (PlayableDura <= 0f && !Mathf.Approximately(PlayableDura, -1f))
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
	}

	public void SkipPlayable()
	{
		if (!mIsPlayingPlayable && IsRunWhenSkip && Playable != null)
		{
			RunPlayable();
		}
		if (IsWaitPlayableStop)
		{
			StopPlayable();
		}
	}

	private void OnPlayableStop(MyPlayable playable)
	{
		mOnPlayalbeEnd?.Invoke();
	}

	public void StopPlayable()
	{
		if ((Object)(object)mPlayable != (Object)null)
		{
			mPlayable.Stop(immediate: true);
		}
	}
}

}
