// Original client component; see first-promotion status runtime source receipt.
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
namespace Zjcs.Official.BattleGridItemEntities
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MyFightStatusDamageReduceAbsolute : MyFightStatusBase<BattleStatusDamageReduceAbsoluteViewData>, IFightGetUsePlayable
{
	[Tooltip("多次数类型，每次次数减少时的效果变动，按照MaxCount的顺序显示")]
	public List<GameObject> MutilPlayablePrefabs;

	[Tooltip("多次数类型切换效果时的过渡，如盾破碎")]
	public GameObject MutilFadeOutPlayablePrefab;

	[Tooltip("Playable-护盾用完时播放")]
	public GameObject RunOutPlayable;

	[Tooltip("护盾用完时播放的Playable是否在Destory时停止")]
	public bool RunOutStopOnDestory = true;

	private MyPlayable mCurPlayable;

	private MyPlayable mRunOutPlayable;

	public IMyPlayableManager mMyPlayableManager { get; set; }

	protected GameObject ApplyEntity => Component.ApplyObject;

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		if (Component.MaximumCount >= 0)
		{
			MutilPlayNextOne(Component.MaximumCount);
		}
		Component.OnUpdateTriggerCount += new Action<int>(OnUpdateTriggerCount);
	}

	public override void OnEnd(object userData)
	{
		Component.OnUpdateTriggerCount -= new Action<int>(OnUpdateTriggerCount);
		if ((Object)(object)mCurPlayable != (Object)null)
		{
			mCurPlayable.Stop();
		}
		if ((Object)(object)mRunOutPlayable != (Object)null && RunOutStopOnDestory)
		{
			mRunOutPlayable.Stop();
			mRunOutPlayable.OnStop -= new Action<MyPlayable>(OnRunOutPlayableStop);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if ((Object)(object)mRunOutPlayable != (Object)null && RunOutStopOnDestory)
		{
			OnRunOutPlayableStop(mRunOutPlayable);
		}
	}

	private void OnUpdateTriggerCount(int triggerCount)
	{
		if (triggerCount == 0)
		{
			return;
		}
		int num = Component.MaximumCount - triggerCount;
		if ((Object)(object)RunOutPlayable != (Object)null && num == 0)
		{
			GameObject gameObject = ApplyEntity;
			GameObject gameObject2 = ApplyEntity;
			mRunOutPlayable = mMyPlayableManager.Play(RunOutPlayable, gameObject, gameObject2);
			mRunOutPlayable.OnStop += new Action<MyPlayable>(OnRunOutPlayableStop);
		}
		if (Component.MaximumCount >= 0)
		{
			if ((Object)(object)mCurPlayable != (Object)null)
			{
				mCurPlayable.Stop();
			}
			if ((Object)(object)MutilFadeOutPlayablePrefab != (Object)null)
			{
				mMyPlayableManager.Play(MutilFadeOutPlayablePrefab, ApplyEntity, ApplyEntity);
			}
			MutilPlayNextOne(num);
		}
	}

	private void MutilPlayNextOne(int remainCount)
	{
		int num = remainCount - 1;
		if (num < MutilPlayablePrefabs.Count && num >= 0)
		{
			GameObject prefab = MutilPlayablePrefabs[num];
			mCurPlayable = mMyPlayableManager.Play(prefab, ApplyEntity, ApplyEntity);
		}
	}

	public void OnRunOutPlayableStop(MyPlayable runOutPlayable)
	{
		runOutPlayable.Stop();
		runOutPlayable.OnStop -= new Action<MyPlayable>(OnRunOutPlayableStop);
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables == null)
		{
			return;
		}
		if (MutilPlayablePrefabs != null)
		{
			foreach (GameObject mutilPlayablePrefab in MutilPlayablePrefabs)
			{
				playables.Add(mutilPlayablePrefab);
			}
		}
		if ((Object)(object)MutilFadeOutPlayablePrefab != (Object)null)
		{
			playables.Add(MutilFadeOutPlayablePrefab);
		}
		if ((Object)(object)RunOutPlayable != (Object)null)
		{
			playables.Add(RunOutPlayable);
		}
	}
}
}
