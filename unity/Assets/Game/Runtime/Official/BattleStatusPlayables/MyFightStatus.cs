// Migrated official status application source; see status view runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
public class MyFightStatus : MonoBehaviour
{
	[Tooltip("开始playable，source和target都是被上状态的对象")]
	public GameObject myPlayablePrefab;

	[Tooltip("只要有叠层就播放的playable，开始和叠层数更新时都会播放。source和target都是被上状态的对象")]
	public GameObject StackedPlayablePrefab;

	[Tooltip("叠层数相对应才会播放的Playable，source和target都是被上状态的对象")]
	public List<StackCountPlayableMap> StackPlayableMap;

	[Tooltip("是否在玩家头上显示图标")]
	public bool ShowSpriteAbovePlayer = true;

	[Tooltip("触发状态时播放的playable，如每次被伤害等（PS.目前只有MyStatusDamageSkill组件会触发，需要其他状态需联系程序）。source和target都是被上状态的对象")]
	public GameObject InvokePlayable;

	[Tooltip("需要根据叠层数量变更表现的playable，source和target都是被上状态的对象。")]
	public GameObject StackPlayable;

	[Tooltip("Master产生了target,是否在此过程中是否需要playable;source是master,target是被上状态的对象。")]
	public GameObject MasterCastPlayable;

	[Tooltip("是否显示图标的时候,数字是显示剩余回合数")]
	public bool IsIconShowStatusRoundReplaceStack;

	[Tooltip("使用自定义叠层数量显示图标")]
	public bool IsIconShowCustomCount;

	private MyFightStatusAgent mStatusAgent;

	private int? mShareId;

	private bool mShowView;

	private MyPlayable mPlayable;

	private MyPlayable mStackedPlayable;

	private MyPlayableStackPlay mSpecialStactedPlay;

	private MyPlayable mStackPlayable;

	private MyPlayable mMasterCastPlayable;

	private Dictionary<string, object> mStatusPlayableParams;

	private int mIconShowCustomCount;

	private int mCurrentStackConfigIndex = -1;

	public IFightStatusPlayableContext Context { get; private set; }

	public IFightStatusPlayableState Component => Context.State;

	public GameObject ApplyEntity => Context.Owner;

	public int IconShowCustomCount
	{
		get
		{
			return mIconShowCustomCount;
		}
		set
		{
			int param = mIconShowCustomCount;
			mIconShowCustomCount = value;
			mStatusAgent.OnStatusShowCustomCountUpdate.SafeInvoke(this, param, mIconShowCustomCount);
		}
	}

	private IMyPlayableManager mMyPlayableManager;

	public void Bind(IFightStatusPlayableContext context, IMyPlayableManager manager)
	{
		Context = context ?? throw new ArgumentNullException(nameof(context));
		mMyPlayableManager = manager ?? throw new ArgumentNullException(nameof(manager));
	}

	public void OnStart(bool newEntity)
	{
		if (ApplyEntity != null)
		{
			mStatusAgent = Context.Agent;
			mShareId = Context.ShareId;
			Component.OnStatusStackUpdate += OnStatusStackUpdate;
			Component.OnInvoked += OnStatusInvoked;
			Component.OnForbiddenChanged += OnForbiddenChanged;
			OnForbiddenChanged(Component, Component.Forbidden);
			NotifyStartEvent();
		}
	}

	public void OnEnd()
	{
		StopAllPlayable();
		if (Component != null)
		{
			Component.OnStatusStackUpdate -= OnStatusStackUpdate;
			Component.OnInvoked -= OnStatusInvoked;
			Component.OnForbiddenChanged -= OnForbiddenChanged;
		}
		NotifyEndEvent();
		mStatusAgent = null;
		mShowView = false;
	}

	private void NotifyStartEvent()
	{
		Context.Agent.ApplyStatus(this);
	}

	private void NotifyEndEvent()
	{
		GameObject applyEntity = ApplyEntity;
		if ((Object)(object)applyEntity != (Object)null)
		{
			Context.Agent.EndStatus(this);
		}
	}

	private void OnStatusStackUpdate(IFightStatusPlayableState status, int fromCount, int toCount)
	{
		if (Component.CanStackCount)
		{
			if ((Object)(object)mStackedPlayable == (Object)null)
			{
				PlayStackedPlayable();
			}
			RefreshStackCountPlayable(toCount);
		}
	}

	private void RefreshStackCountPlayable(int toCount)
	{
		if (!mShareId.HasValue)
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
		if (!mShareId.HasValue)
		{
			yield break;
		}
		int shareId = mShareId.Value;
		foreach (MyFightStatus myStatus in mStatusAgent.MyStatuses)
		{
			int? component = myStatus.Context.ShareId;
			if (!(!component.HasValue) && component.Value == shareId && !((Object)(object)myStatus == (Object)(object)this))
			{
				yield return myStatus;
			}
		}
	}

	private void RefreshStackCountPlayable()
	{
		if (!mShareId.HasValue)
		{
			return;
		}
		MyFightStatus myFightStatus = this;
		int num = Component.StackedCount;
		int shareId = mShareId.Value;
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
		myFightStatus.RunStackCountPlayable(value2);
	}

	private void StopOldDisplay(MyFightStatus oldDisplayStatus)
	{
		if ((Object)(object)oldDisplayStatus != (Object)null)
		{
			oldDisplayStatus.StopSpecialStackedPlayable(0);
		}
	}

	private void OnStatusInvoked(IFightStatusPlayableState status)
	{
		if ((Object)(object)InvokePlayable != (Object)null)
		{
			GameObject gameObject = ApplyEntity;
			mMyPlayableManager.Play(InvokePlayable, gameObject, gameObject);
		}
	}

	private void OnForbiddenChanged(IFightStatusPlayableState status, bool preForbidden)
	{
		if (Component.Forbidden)
		{
			if (mShowView)
			{
				StopAllPlayable();
				mShowView = false;
			}
		}
		else if (!mShowView)
		{
			PlayAllPlayable();
			mShowView = true;
		}
	}

	private void RunStackCountPlayable(int stackCount)
	{
		if (StackPlayableMap == null || StackPlayableMap.Count == 0)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < StackPlayableMap.Count; i++)
		{
			if (StackPlayableMap[i].Match(stackCount))
			{
				num = i;
				break;
			}
		}
		if (num != mCurrentStackConfigIndex)
		{
			if (num < 0)
			{
				StopSpecialStackedPlayable(stackCount);
				mCurrentStackConfigIndex = -1;
			}
			else
			{
				mCurrentStackConfigIndex = num;
				PlaySpecialStackedPlayable(StackPlayableMap[num], stackCount);
			}
		}
	}

	private void OnStackedPlayableStop(MyPlayable playable)
	{
		mStackedPlayable.OnStop -= new Action<MyPlayable>(OnStackedPlayableStop);
		mStackedPlayable = null;
	}

	private void PlayStackPlayable()
	{
		if (!((Object)(object)StackPlayable == (Object)null))
		{
			Dictionary<string, object> statusPlayableParams = GetStatusPlayableParams();
			GameObject gameObject = ApplyEntity;
			mStackPlayable = mMyPlayableManager.Play(StackPlayable, gameObject, gameObject, 1f, null, statusPlayableParams);
		}
	}

	private void StopStackPlayable()
	{
		if ((Object)(object)mStackPlayable != (Object)null)
		{
			mStackPlayable.Stop();
			mStackPlayable = null;
		}
	}

	private void PlayMasterCastPlayable()
	{
		if ((Object)(object)MasterCastPlayable == (Object)null) return;
		GameObject master = Context.Master;
		if (master != null)
			mMasterCastPlayable = mMyPlayableManager.Play(MasterCastPlayable, master, ApplyEntity);
	}

	private void StopMasterCastPlayable()
	{
		if ((Object)(object)mMasterCastPlayable != (Object)null)
		{
			mMasterCastPlayable.Stop();
			mMasterCastPlayable = null;
		}
	}

	private Dictionary<string, object> GetStatusPlayableParams()
	{
		if (mStatusPlayableParams != null)
		{
			return mStatusPlayableParams;
		}
		mStatusPlayableParams = new Dictionary<string, object> { { "Status", this } };
		return mStatusPlayableParams;
	}

	private void PlayAllPlayable()
	{
		PlayPlabable();
		PlayStackedPlayable();
		RefreshStackCountPlayable(Component.StackedCount);
		PlayStackPlayable();
		PlayMasterCastPlayable();
	}

	private void StopAllPlayable()
	{
		StopPlayPlayable();
		StopStackedPlayable();
		StopSpecialStackedPlayable(0);
		StopStackPlayable();
		StopMasterCastPlayable();
	}

	private void PlayPlabable()
	{
		if ((Object)(object)myPlayablePrefab != (Object)null)
		{
			GameObject gameObject = ApplyEntity;
			mPlayable = mMyPlayableManager.Play(myPlayablePrefab, gameObject, gameObject, 1f, null, GetStatusPlayableParams());
		}
	}

	private void StopPlayPlayable()
	{
		if ((Object)(object)mPlayable != (Object)null)
		{
			mPlayable.Stop();
			mPlayable = null;
		}
	}

	private void PlayStackedPlayable()
	{
		if (Component.CanStackCount && (Object)(object)StackedPlayablePrefab != (Object)null)
		{
			GameObject gameObject = ApplyEntity;
			mStackedPlayable = mMyPlayableManager.Play(StackedPlayablePrefab, gameObject, gameObject);
			mStackedPlayable.OnStop += new Action<MyPlayable>(OnStackedPlayableStop);
		}
	}

	private void StopStackedPlayable()
	{
		if (Component.CanStackCount && (Object)(object)mStackedPlayable != (Object)null)
		{
			mStackedPlayable.OnStop -= new Action<MyPlayable>(OnStackedPlayableStop);
			mStackedPlayable.Stop();
			mStackedPlayable = null;
		}
	}

	private void PlaySpecialStackedPlayable(StackCountPlayableMap config, int stackCount)
	{
		if (Component.CanStackCount)
		{
			if (mSpecialStactedPlay == null)
			{
				mSpecialStactedPlay = new MyPlayableStackPlay(mMyPlayableManager);
			}
			GameObject gameObject = ApplyEntity;
			mSpecialStactedPlay.Play(stackCount, config.BeginPlayable, config.PlayablePrefab, config.EndPlayable, gameObject, gameObject);
		}
	}

	private void StopSpecialStackedPlayable(int stackCount)
	{
		if (Component.CanStackCount)
		{
			GameObject gameObject = ApplyEntity;
			mSpecialStactedPlay?.Stop(stackCount, gameObject, gameObject);
			mSpecialStactedPlay = null;
			mCurrentStackConfigIndex = -1;
		}
	}

	public bool CanShowSpriteIcon()
	{
		if (!ShowSpriteAbovePlayer) return false;
		return Context.HasIcon;
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables == null)
		{
			return;
		}
		if ((Object)(object)myPlayablePrefab != (Object)null)
		{
			playables.Add(myPlayablePrefab);
		}
		if ((Object)(object)StackedPlayablePrefab != (Object)null)
		{
			playables.Add(StackedPlayablePrefab);
		}
		if (StackPlayableMap != null)
		{
			foreach (StackCountPlayableMap item in StackPlayableMap)
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
		if ((Object)(object)InvokePlayable != (Object)null)
		{
			playables.Add(InvokePlayable);
		}
		if ((Object)(object)StackPlayable != (Object)null)
		{
			playables.Add(StackPlayable);
		}
		if ((Object)(object)MasterCastPlayable != (Object)null)
		{
			playables.Add(MasterCastPlayable);
		}
	}
}

}
