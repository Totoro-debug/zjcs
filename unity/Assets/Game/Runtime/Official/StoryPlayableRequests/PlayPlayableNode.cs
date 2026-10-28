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
public class PlayPlayableNode : StoryPlayableRequestNode
{
	[Tooltip("物件Entity")]
	public List<StoryPlayableEntityBinding> SourceEntitys;

	[Tooltip("目标物体Entitys")]
	public List<StoryPlayableEntityBinding> TargetEntitys;

	[Tooltip("要播放的Playables")]
	public List<AssetGuidProxy<GameObject>> PlayablesProxy;

	[Tooltip("Playable持续时间(每个都是这个时长)，如果设置-1则按照Playable原本时长播放")]
	public float PlayableDura = -1f;

	[Tooltip("是否等待Playable停止")]
	public bool IsWaitPlayableStop;

	[Tooltip("是否在跳过时执行")]
	public bool IsRunWhenSkip;

	[Tooltip("是否在结束剧情时，结束这个节点创建的Playable")]
	public bool IsStopAllPlayableWhenStoryFinish;

	[Tooltip("是否在节点开始前，结束传入的Entities原来的playables")]
	public bool IsStopEntitesPlayableSupportBeforeNodeRun;

	[Tooltip("是否在节点结束后，重开传入的Entities原来的playables")]
	public bool IsStartEntitiesPlayableSupportAfterNodeRun;

	private StoryPlayableResourceBag mDisposableBag;

	private List<MyPlayable> mPlayables = new List<MyPlayable>();

	private int mRunningPlayableCount;
	public IStoryHandleProxy StoryHandleProxy { get; set; }
	public IProfessionImageResourceLoader ResourceLoader { get; set; }

	public override void OnStart()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		base.OnStart();
		mDisposableBag = new StoryPlayableResourceBag();
		SetEntityForceVisibleState(visible: true);
		StopAllPlayablesBeforeNodeRun();
	}

	protected override StoryRunNodeResultCode GetCodeForTimeout()
	{
		return StoryRunNodeResultCode.OK;
	}

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
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		mPlayables.Clear();
		if (PlayablesProxy == null || PlayablesProxy.Count == 0)
		{
			return;
		}
		bool flag = default;
		for (int i = 0; i < PlayablesProxy.Count; i++)
		{
			StoryPlayableEntityBinding inputValue = GetInputValue<StoryPlayableEntityBinding>(string.Format(string.Format("{0} {1}", "SourceEntitys", i)));
			StoryPlayableEntityBinding inputValue2 = GetInputValue<StoryPlayableEntityBinding>(string.Format(string.Format("{0} {1}", "TargetEntitys", i)));
			GameObject source = (((Object)(object)inputValue == (Object)null) ? null : ((Component)inputValue).gameObject);
			GameObject target = (((Object)(object)inputValue2 == (Object)null) ? null : ((Component)inputValue2).gameObject);
			GameObject result = mDisposableBag.Add(ResourceLoader.LoadAssetByGuid<GameObject>(PlayablesProxy[i].AssetGuid)).Result;
			MyPlayable myPlayable = StoryHandleProxy?.PlayPlayable(result, source, target);
			if (!((Object)(object)myPlayable != (Object)null))
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
		SetEntityForceVisibleState(visible: false);
		StartAllPlayablesAfterNodeRun();
		mDisposableBag.Dispose();
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

	private void SetEntityForceVisibleState(bool visible)
	{
		StoryPlayableEntityBinding[] inputValues = GetInputValues<StoryPlayableEntityBinding>("SourceEntitys");
		StoryPlayableEntityBinding[] inputValues2 = GetInputValues<StoryPlayableEntityBinding>("TargetEntitys");
		if (inputValues != null)
		{
			StoryPlayableEntityBinding[] array = inputValues;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].ClientVisibilitySupporter.SetForceVisible(visible);
			}
		}
		if (inputValues2 != null)
		{
			StoryPlayableEntityBinding[] array = inputValues2;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].ClientVisibilitySupporter.SetForceVisible(visible);
			}
		}
	}

	private StoryPlayableEntityBinding GetSourceEntity(int index)
	{
		return GetInputValue<StoryPlayableEntityBinding>(string.Format(string.Format("{0} {1}", "SourceEntitys", index)));
	}

	private StoryPlayableEntityBinding GetTargetEntity(int index)
	{
		return GetInputValue<StoryPlayableEntityBinding>(string.Format(string.Format("{0} {1}", "TargetEntitys", index)));
	}

	private void StopAllPlayablesBeforeNodeRun()
	{
		if (IsStopEntitesPlayableSupportBeforeNodeRun && PlayablesProxy != null)
		{
			for (int i = 0; i < PlayablesProxy.Count; i++)
			{
				StoryPlayableEntityBinding sourceEntity = GetSourceEntity(i);
				StoryPlayableEntityBinding targetEntity = GetTargetEntity(i);
				GetWorldStageSupport(sourceEntity)?.Stop();
				GetWorldStageSupport(targetEntity)?.Stop();
			}
		}
	}

	private void StartAllPlayablesAfterNodeRun()
	{
		if (IsStartEntitiesPlayableSupportAfterNodeRun && PlayablesProxy != null)
		{
			for (int i = 0; i < PlayablesProxy.Count; i++)
			{
				StoryPlayableEntityBinding sourceEntity = GetSourceEntity(i);
				StoryPlayableEntityBinding targetEntity = GetTargetEntity(i);
				GetWorldStageSupport(sourceEntity)?.Start();
				GetWorldStageSupport(targetEntity)?.Start();
			}
		}
	}

	public override void CleanWhileStoryAllEnd()
	{
		base.CleanWhileStoryAllEnd();
		if (mPlayables == null)
		{
			return;
		}
		if (mPlayables != null && IsStopAllPlayableWhenStoryFinish)
		{
			foreach (MyPlayable mPlayable in mPlayables)
			{
				mPlayable.Stop(immediate: true);
			}
		}
		mPlayables.Clear();
	}
}

}
