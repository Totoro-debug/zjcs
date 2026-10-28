// Migrated original client request body; see playable requests receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.ProfessionUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Zjcs.Official.StoryPlayableRequests
{
public class PlayPlayableForGameObjectNode : StoryPlayableRequestNode
{
	[Tooltip("GameObjects")]
	public List<GameObject> SourceGameObjects;

	[Tooltip("要播放的Playables")]
	public List<StoryPlayableSelect> Playables;

	[Tooltip("Playable持续时间(每个都是这个时长)，如果设置-1则按照Playable原本时长播放")]
	public float PlayableDura = -1f;

	[Tooltip("是否等待Playable停止")]
	public bool IsWaitPlayableStop;

	[Tooltip("是否在跳过时执行")]
	public bool IsRunWhenSkip;

	[Tooltip("是否在结束剧情时，结束这个节点创建的Playable")]
	public bool IsStopAllPlayableWhenStoryFinish = true;

	private List<MyPlayable> mPlayables = new List<MyPlayable>();

	private int mRunningPlayableCount;
	public IStoryHandleProxy StoryHandleProxy { get; set; }

	protected override async UniTask<StoryRunNodeResultCode> InternalRun(CancellationToken token)
	{
		DoRun();
		mRunningPlayableCount = mPlayables.Count;
		if (IsWaitPlayableStop && mPlayables != null)
		{
			foreach (MyPlayable playable in mPlayables) playable.OnStop += new Action<MyPlayable>(OnPlayableStop);
			if (mRunningPlayableCount > 0)
				await UniTask.WaitUntil(() => mRunningPlayableCount <= 0, (PlayerLoopTiming)8, token, false)
					.Timeout(TimeSpan.FromSeconds(20.0), (DelayType)0, (PlayerLoopTiming)8, null);
		}
		return await base.InternalRun(token);
	}

	protected override UniTask<StoryRunNodeResultCode> InternalSkip(CancellationToken token)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (IsRunWhenSkip)
		{
			DoRun();
		}
		StopAll();
		return base.InternalSkip(token);
	}

	protected override UniTask<StoryRunNodeResultCode> AsyncTransRunToSkip(CancellationToken token)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		StopAll();
		return base.AsyncTransRunToSkip(token);
	}

	private void DoRun()
	{
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		mPlayables.Clear();
		if (Playables == null || Playables.Count == 0)
		{
			return;
		}
		bool flag = default;
		for (int i = 0; i < Playables.Count; i++)
		{
			GameObject inputValue = GetInputValue<GameObject>(string.Format(string.Format("{0} {1}", "SourceGameObjects", i)));
			GameObject prefab = Playables[i].Load(StoryHandleProxy);
			MyPlayable myPlayable = StoryHandleProxy?.PlayPlayable(prefab, inputValue, null);
			if ((Object)null == (Object)(object)myPlayable)
			{
				continue;
			}
			mPlayables.Add(myPlayable);
			if (PlayableDura > 0f)
			{
				myPlayable.SetAutoStopCountDown(PlayableDura);
			}
			else if (PlayableDura <= 0f && !Mathf.Approximately(PlayableDura, -1f))
			{
				UnityEngine.Debug.LogError("[Story] PlayableDura config error: " + PlayableDura);
			}
		}
	}

	public override void OnEnd()
	{
		base.OnEnd();
		if (mPlayables != null)
		{
			foreach (MyPlayable mPlayable in mPlayables)
			{
				mPlayable.OnStop -= new Action<MyPlayable>(OnPlayableStop);
			}
		}
		mRunningPlayableCount = 0;
	}

	private void StopAll()
	{
		if (!IsWaitPlayableStop || mPlayables.Count <= 0)
		{
			return;
		}
		foreach (MyPlayable mPlayable in mPlayables)
		{
			mPlayable.OnStop -= new Action<MyPlayable>(OnPlayableStop);
			mPlayable.Stop(immediate: true);
		}
	}

	private void OnPlayableStop(MyPlayable playable)
	{
		mRunningPlayableCount--;
	}

	public override void CleanWhileStoryAllEnd()
	{
		base.CleanWhileStoryAllEnd();
		if (mPlayables != null)
		{
			if (mPlayables != null && IsStopAllPlayableWhenStoryFinish)
			{
				foreach (MyPlayable mPlayable in mPlayables)
				{
					mPlayable.Stop(immediate: true);
				}
			}
			mPlayables.Clear();
		}
		if (Playables == null || !IsStopAllPlayableWhenStoryFinish)
		{
			return;
		}
		foreach (StoryPlayableSelect playable in Playables)
		{
			playable.UnLoad();
		}
	}
}

}
