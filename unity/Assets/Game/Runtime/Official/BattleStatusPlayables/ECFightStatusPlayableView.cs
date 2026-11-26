// Complete current official status EC view; see its source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
[System.Serializable]
public class ECFightStatusPlayableView : ECViewBase, IFightGetUsePlayable, ICurrentFightStatusPlayableView
{
	[Tooltip("开始playable，source和target都是被上状态的对象")]
	[SerializeField]
	private GameObject mPlayablePrefab;

	[Tooltip("只要有叠层就播放的playable，开始和叠层数更新时都会播放；source和target都是被上状态的对象")]
	[SerializeField]
	private GameObject mStackedPlayable;

	[Tooltip("叠层数相对应才会播放的Playable，source和target都是被上状态的对象")]
	[SerializeField]
	private List<StackCountPlayableMap> mStackPlayableMap;

	[Tooltip("需要根据叠层数量变更表现的playable（解禁/显示时播放），source和target都是被上状态的对象")]
	[SerializeField]
	private GameObject mStackPlayable;

	[Tooltip("Master产生了target,是否在此过程中播放; source是master,target是被上状态的对象")]
	[SerializeField]
	private GameObject mMasterCastPlayable;

	private IFightStatusPlayableState mStatusComponent;

	private MyEntity mStatusRoot;

	private MyFightStatus mStatusShell;

	private MyFightStatusAgent mStatusAgent;

	private int? mShareStack;

	private MyPlayable mPlayable;

	private MyPlayable mStackedPlayableHandle;

	private MyPlayable mStackPlayableHandle;

	private MyPlayable mMasterCastPlayableHandle;

	private MyPlayableStackPlay mSpecialStackedPlay;

	private Dictionary<string, object> mPlayableParams;

	private IDisposable mOnStatusForbiddenChangedDisposable;

	private bool mShowView;

	private int mCurrentStackConfigIndex = -1;

	public IMyPlayableManager MyPlayableManager { get; set; }

	private MyEntity ApplyEntity
	{
		get
		{
			EnsureRefs();
			if ((Object)(object)mStatusRoot == (Object)null || mStatusComponent == null || mStatusShell?.Context == null)
			{
				return null;
			}
			return mStatusShell.Context.Owner ? mStatusShell.Context.Owner.GetComponent<MyEntity>() : null;
		}
	}

	protected override void Load()
	{
		base.Load();
		EnsureRefs();
		SubscribeEvents();
		OnForbiddenChanged(mStatusComponent, mStatusComponent != null && mStatusComponent.Forbidden);
	}

	protected override void Unload()
	{
		UnsubscribeEvents();
		StopAll();
		mStatusComponent = null;
		mStatusRoot = null;
		mStatusShell = null;
		mStatusAgent = null;
		mShareStack = null;
		mPlayableParams = null;
		mStackedPlayableHandle = null;
		mShowView = false;
		mCurrentStackConfigIndex = -1;
		base.Unload();
	}

	public void PlayStartPlayable()
	{
		if (!((Object)(object)mPlayablePrefab == (Object)null))
		{
			MyEntity applyEntity = ApplyEntity;
			if (!((Object)(object)applyEntity == (Object)null))
			{
				GameObject gameObject = ((Component)applyEntity).gameObject;
				mPlayable = MyPlayableManager.Play(mPlayablePrefab, gameObject, gameObject, 1f, null, GetStatusPlayableParams());
			}
		}
	}

	public void StopStartPlayable()
	{
		if (!((Object)(object)mPlayable == (Object)null))
		{
			mPlayable.Stop();
			mPlayable = null;
		}
	}

	public void PlayStackedPlayable()
	{
		if ((Object)(object)mStackedPlayable == (Object)null || !CanStackCount())
		{
			return;
		}
		MyEntity applyEntity = ApplyEntity;
		if (!((Object)(object)applyEntity == (Object)null))
		{
			GameObject gameObject = ((Component)applyEntity).gameObject;
			mStackedPlayableHandle = MyPlayableManager.Play(mStackedPlayable, gameObject, gameObject);
			if ((Object)(object)mStackedPlayableHandle != (Object)null)
			{
				mStackedPlayableHandle.OnStop += new Action<MyPlayable>(OnStackedPlayableStop);
			}
		}
	}

	public void StopStackedPlayable()
	{
		if (!((Object)(object)mStackedPlayableHandle == (Object)null))
		{
			mStackedPlayableHandle.OnStop -= new Action<MyPlayable>(OnStackedPlayableStop);
			mStackedPlayableHandle.Stop();
			mStackedPlayableHandle = null;
		}
	}

	private void OnStackedPlayableStop(MyPlayable playable)
	{
		if ((Object)(object)playable != (Object)null)
		{
			playable.OnStop -= new Action<MyPlayable>(OnStackedPlayableStop);
		}
		if ((Object)(object)mStackedPlayableHandle == (Object)(object)playable)
		{
			mStackedPlayableHandle = null;
		}
	}

	public void PlayStackCountSegment(StackCountPlayableMap config, int stackCount)
	{
		if (CanStackCount())
		{
			if (mSpecialStackedPlay == null)
			{
				mSpecialStackedPlay = new MyPlayableStackPlay(MyPlayableManager);
			}
			MyEntity applyEntity = ApplyEntity;
			if (!((Object)(object)applyEntity == (Object)null))
			{
				GameObject gameObject = ((Component)applyEntity).gameObject;
				mSpecialStackedPlay.Play(stackCount, config.BeginPlayable, config.PlayablePrefab, config.EndPlayable, gameObject, gameObject);
			}
		}
	}

	public void StopStackCountSegment(int stackCount)
	{
		MyEntity applyEntity = ApplyEntity;
		GameObject val = (((Object)(object)applyEntity != (Object)null) ? ((Component)applyEntity).gameObject : null);
		if (mSpecialStackedPlay == null || (Object)(object)val == (Object)null)
		{
			mSpecialStackedPlay = null;
			mCurrentStackConfigIndex = -1;
		}
		else
		{
			mSpecialStackedPlay.Stop(stackCount, val, val);
			mSpecialStackedPlay = null;
			mCurrentStackConfigIndex = -1;
		}
	}

	public void PlayStackPlayable()
	{
		if (!((Object)(object)mStackPlayable == (Object)null))
		{
			MyEntity applyEntity = ApplyEntity;
			if (!((Object)(object)applyEntity == (Object)null))
			{
				GameObject gameObject = ((Component)applyEntity).gameObject;
				mStackPlayableHandle = MyPlayableManager.Play(mStackPlayable, gameObject, gameObject, 1f, null, GetStatusPlayableParams());
			}
		}
	}

	public void StopStackPlayable()
	{
		if (!((Object)(object)mStackPlayableHandle == (Object)null))
		{
			mStackPlayableHandle.Stop();
			mStackPlayableHandle = null;
		}
	}

	public void PlayMasterCastPlayable()
	{
		if ((Object)(object)mMasterCastPlayable == (Object)null)
		{
			return;
		}
		MyEntity applyEntity = ApplyEntity;
		if ((Object)(object)applyEntity == (Object)null)
		{
			return;
		}
		GameObject master = mStatusShell.Context.Master;
		if (master)
		{
			mMasterCastPlayableHandle = MyPlayableManager.Play(mMasterCastPlayable, master, ((Component)applyEntity).gameObject);
		}
	}

	public void StopMasterCastPlayable()
	{
		if (!((Object)(object)mMasterCastPlayableHandle == (Object)null))
		{
			mMasterCastPlayableHandle.Stop();
			mMasterCastPlayableHandle = null;
		}
	}

	public void StopAll()
	{
		StopStartPlayable();
		StopStackedPlayable();
		StopStackCountSegment(0);
		StopStackPlayable();
		StopMasterCastPlayable();
	}

	private void SubscribeEvents()
	{
		if (mStatusComponent != null)
		{
			EnsureRefs();
			mStatusComponent.OnStatusStackUpdate += new Action<IFightStatusPlayableState, int, int>(OnStatusStackUpdate);
			mStatusComponent.OnForbiddenChanged += OnForbiddenChanged;
			mOnStatusForbiddenChangedDisposable = new CurrentStatusViewSubscription(() => mStatusComponent.OnForbiddenChanged -= OnForbiddenChanged);
		}
	}

	private void UnsubscribeEvents()
	{
		if (mStatusComponent != null)
		{
			mStatusComponent.OnStatusStackUpdate -= new Action<IFightStatusPlayableState, int, int>(OnStatusStackUpdate);
		}
		mOnStatusForbiddenChangedDisposable?.Dispose();
		mOnStatusForbiddenChangedDisposable = null;
	}

	private void OnStatusStackUpdate(IFightStatusPlayableState status, int fromCount, int toCount)
	{
		if (CanStackCount())
		{
			if ((Object)(object)mStackedPlayableHandle == (Object)null)
			{
				PlayStackedPlayable();
			}
			RefreshStackCountPlayable(toCount);
		}
	}

	private void OnForbiddenChanged(IFightStatusPlayableState status, bool preForbidden)
	{
		if (mStatusComponent == null)
		{
			return;
		}
		if (mStatusComponent.Forbidden)
		{
			if (mShowView)
			{
				StopAll();
				mShowView = false;
			}
		}
		else if (!mShowView)
		{
			PlayStartPlayable();
			PlayStackedPlayable();
			RefreshStackCountPlayable(mStatusComponent.StackedCount);
			PlayStackPlayable();
			PlayMasterCastPlayable();
			mShowView = true;
		}
	}

	private void RefreshStackCountPlayable(int toCount)
	{
		EnsureRefs();
		if (!mShareStack.HasValue)
		{
			RunStackCountPlayable(toCount);
		}
		else
		{
			RefreshStackCountPlayable();
		}
	}

	private IEnumerable<MyFightStatus> GetSameShareStackStatuses()
	{
		EnsureRefs();
		if (!mShareStack.HasValue || (Object)(object)mStatusAgent == (Object)null || (Object)(object)mStatusShell == (Object)null)
		{
			yield break;
		}
		int shareId = mShareStack.Value;
		foreach (MyFightStatus myStatus in mStatusAgent.MyStatuses)
		{
			if (!((Object)(object)myStatus == (Object)null) && !((Object)(object)myStatus == (Object)(object)mStatusShell))
			{
				int? component = myStatus.Context.ShareId;
				if (component.HasValue && component.Value == shareId)
				{
					yield return myStatus;
				}
			}
		}
	}

	private void RefreshStackCountPlayable()
	{
		EnsureRefs();
		if (!mShareStack.HasValue || (Object)(object)mStatusAgent == (Object)null || (Object)(object)mStatusShell == (Object)null || mStatusComponent == null)
		{
			return;
		}
		MyFightStatus myFightStatus = mStatusShell;
		int num = mStatusComponent.StackedCount;
		int shareId = mShareStack.Value;
		foreach (MyFightStatus sameShareStackStatus in GetSameShareStackStatuses())
		{
			int stackedCount = sameShareStackStatus.Component.StackedCount;
			if (stackedCount > num)
			{
				num = stackedCount;
				myFightStatus = sameShareStackStatus;
			}
		}
		mStatusAgent.MyDisplayFightStatusEachShareStatus.TryGetValue(shareId, out var value);
		if (num <= 0)
		{
			if ((Object)(object)value != (Object)null)
			{
				StopOldDisplay(value);
				mStatusAgent.MyDisplayFightStatusEachShareStatus.Remove(shareId);
			}
			return;
		}
		if ((Object)(object)myFightStatus != (Object)(object)value)
		{
			StopOldDisplay(value);
			mStatusAgent.MyDisplayFightStatusEachShareStatus[shareId] = myFightStatus;
		}
		mStatusAgent.StatusToShareStack.TryGetValue(shareId, out var value2);
		CurrentStatusViewLookup.Get(myFightStatus)?.RunStackCountPlayable(value2);
	}

	private static void StopOldDisplay(MyFightStatus oldDisplayStatus)
	{
		if (!((Object)(object)oldDisplayStatus == (Object)null))
		{
			CurrentStatusViewLookup.Get(oldDisplayStatus)?.StopStackCountSegment(0);
		}
	}

	public void RunStackCountPlayable(int stackCount)
	{
		if (mStackPlayableMap == null || mStackPlayableMap.Count == 0)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < mStackPlayableMap.Count; i++)
		{
			if (mStackPlayableMap[i].Match(stackCount))
			{
				num = i;
				break;
			}
		}
		if (num != mCurrentStackConfigIndex)
		{
			if (num < 0)
			{
				StopStackCountSegment(stackCount);
				return;
			}
			mCurrentStackConfigIndex = num;
			PlayStackCountSegment(mStackPlayableMap[num], stackCount);
		}
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables == null)
		{
			return;
		}
		if ((Object)(object)mPlayablePrefab != (Object)null)
		{
			playables.Add(mPlayablePrefab);
		}
		if ((Object)(object)mStackedPlayable != (Object)null)
		{
			playables.Add(mStackedPlayable);
		}
		if (mStackPlayableMap != null)
		{
			foreach (StackCountPlayableMap item in mStackPlayableMap)
			{
				if ((Object)(object)item.PlayablePrefab != (Object)null)
				{
					playables.Add(item.PlayablePrefab);
				}
				if ((Object)(object)item.BeginPlayable != (Object)null)
				{
					playables.Add(item.BeginPlayable);
				}
				if ((Object)(object)item.EndPlayable != (Object)null)
				{
					playables.Add(item.EndPlayable);
				}
			}
		}
		if ((Object)(object)mStackPlayable != (Object)null)
		{
			playables.Add(mStackPlayable);
		}
		if ((Object)(object)mMasterCastPlayable != (Object)null)
		{
			playables.Add(mMasterCastPlayable);
		}
	}

	private void EnsureRefs()
	{
		if ((Object)(object)mStatusRoot == (Object)null)
		{
			mStatusRoot = ((Component)mOwner.transform).GetComponentInParent<MyEntity>(true);
		}
		if (mStatusComponent == null && (Object)(object)mStatusRoot != (Object)null)
		{
			mStatusComponent = mStatusRoot.Entity.GetComponent<IFightStatusPlayableState>();
		}
		if ((Object)(object)mStatusShell == (Object)null)
		{
			mStatusShell = ((Component)mOwner.transform).GetComponentInParent<MyFightStatus>(true);
		}
		if (!mShareStack.HasValue && (Object)(object)mStatusRoot != (Object)null)
		{
			mShareStack = mStatusShell.Context.ShareId;
		}
		if ((Object)(object)mStatusAgent == (Object)null && (Object)(object)mStatusRoot != (Object)null && mStatusComponent != null && mStatusShell?.Context != null)
		{
			MyEntity entity = mStatusShell.Context.Owner ? mStatusShell.Context.Owner.GetComponent<MyEntity>() : null;
			if ((Object)(object)entity != (Object)null)
			{
				mStatusAgent = ((Component)entity).GetComponent<MyFightStatusAgent>();
			}
		}
	}

	private bool CanStackCount()
	{
		EnsureRefs();
		if (mStatusComponent != null)
		{
			return mStatusComponent.CanStackCount;
		}
		return false;
	}

	private Dictionary<string, object> GetStatusPlayableParams()
	{
		if (mPlayableParams == null)
		{
			mPlayableParams = new Dictionary<string, object> { { "Status", mStatusShell } };
		}
		return mPlayableParams;
	}
}

}
