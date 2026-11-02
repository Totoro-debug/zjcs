// Migrated current official application source; see status playable runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryAttachment;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
public class ApplyEffectByPriority : ApplyEnableOnPlay
{
	[Tooltip("优先级Id")]
	[SerializeField]
	private string mPriorityID = "";

	[Tooltip("优先级高的覆盖优先级低的")]
	[SerializeField]
	private int mPriority;

	[Tooltip("Attach对象")]
	[SerializeField]
	public AttachPosRotCfg myAttach;

	[Tooltip("当比其优先级高的Playable播放结束后，该Playable是否恢复")]
	[SerializeField]
	private bool mAutoResume;

	private GameObject mPlayableAttachCache;

	private List<ApplyEffectByPriority> mLowerPriorityListCache = new List<ApplyEffectByPriority>();

	private List<ApplyEffectByPriority> mLowerPriorityCandidatesCache = new List<ApplyEffectByPriority>();

	public string PriorityID => mPriorityID;

	public int Priority => mPriority;

	public AttachPosRotCfg Attach => myAttach;

	public bool AutoResume => mAutoResume;

	public IMyPlayableCenter MyPlayableCenter { get; set; }

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		base.PlayImpl(playable, applyAttr);
		ProcessEffect();
	}

	protected override void StopImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		ProcessOtherPausePlayable();
		base.StopImpl(playable, applyAttr);
	}

	private void ProcessEffect()
	{
		if (MyPlayableCenter == null || myAttach == null || string.IsNullOrEmpty(mPriorityID))
		{
			return;
		}
		GameObject attachToGo = AttachPosCfg.GetAttachToGo(myPlayable, myAttach.AttachTo);
		if ((Object)(object)attachToGo == (Object)null)
		{
			return;
		}
		mPlayableAttachCache = attachToGo;
		ApplyEffectByPriority applyEffectByPriority = null;
		mLowerPriorityListCache.Clear();
		foreach (OrderedFastList<MyPlayable> allPlayable in MyPlayableCenter.AllPlayables)
		{
			if (allPlayable == null)
			{
				continue;
			}
			foreach (MyPlayable item in allPlayable)
			{
				if ((Object)(object)item == (Object)null || (Object)(object)item == (Object)(object)myPlayable || item.myStatus == MyPlayable.PlayableStatus.Stop)
				{
					continue;
				}
				ApplyEffectByPriority component = ((Component)item).GetComponent<ApplyEffectByPriority>();
				if (!((Object)(object)component == (Object)null) && !((Object)(object)AttachPosCfg.GetAttachToGo(item, component.Attach.AttachTo) != (Object)(object)attachToGo) && component.PriorityID.Equals(mPriorityID) && component.Priority != mPriority)
				{
					if ((Object)(object)applyEffectByPriority == (Object)null || component.Priority > applyEffectByPriority.Priority)
					{
						applyEffectByPriority = component;
					}
					if (component.Priority < mPriority)
					{
						mLowerPriorityListCache.Add(component);
					}
				}
			}
		}
		if ((Object)(object)applyEffectByPriority == (Object)null)
		{
			return;
		}
		if (applyEffectByPriority.Priority > mPriority)
		{
			OnInterruptedByHigherPriority();
		}
		else
		{
			foreach (ApplyEffectByPriority item2 in mLowerPriorityListCache)
			{
				if ((Object)(object)item2 != (Object)null)
				{
					item2.OnInterruptedByHigherPriority();
				}
			}
		}
		mLowerPriorityListCache.Clear();
	}

	private void ProcessOtherPausePlayable()
	{
		if (MyPlayableCenter == null || myAttach == null || string.IsNullOrEmpty(mPriorityID))
		{
			return;
		}
		GameObject val = mPlayableAttachCache;
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		int num = int.MinValue;
		mLowerPriorityCandidatesCache.Clear();
		foreach (OrderedFastList<MyPlayable> allPlayable in MyPlayableCenter.AllPlayables)
		{
			if (allPlayable == null)
			{
				continue;
			}
			foreach (MyPlayable item in allPlayable)
			{
				if ((Object)(object)item == (Object)null || (Object)(object)item == (Object)(object)myPlayable || item.myStatus == MyPlayable.PlayableStatus.Stop)
				{
					continue;
				}
				ApplyEffectByPriority component = ((Component)item).GetComponent<ApplyEffectByPriority>();
				if (!((Object)(object)component == (Object)null) && !((Object)(object)AttachPosCfg.GetAttachToGo(item, component.Attach.AttachTo) != (Object)(object)val) && component.PriorityID.Equals(mPriorityID))
				{
					if (component.Priority == mPriority)
					{
						return;
					}
					if (component.Priority > num)
					{
						num = component.Priority;
					}
					if (component.Priority != mPriority && component.Priority < mPriority && component.AutoResume)
					{
						mLowerPriorityCandidatesCache.Add(component);
					}
				}
			}
		}
		if (mLowerPriorityCandidatesCache.Count == 0)
		{
			return;
		}
		int num2 = int.MinValue;
		for (int i = 0; i < mLowerPriorityCandidatesCache.Count; i++)
		{
			ApplyEffectByPriority applyEffectByPriority = mLowerPriorityCandidatesCache[i];
			if ((Object)(object)applyEffectByPriority != (Object)null && applyEffectByPriority.Priority > num2)
			{
				num2 = applyEffectByPriority.Priority;
			}
		}
		if (num2 == int.MinValue || num > num2)
		{
			return;
		}
		List<ApplyEffectByPriority> list = new List<ApplyEffectByPriority>();
		for (int j = 0; j < mLowerPriorityCandidatesCache.Count; j++)
		{
			ApplyEffectByPriority applyEffectByPriority2 = mLowerPriorityCandidatesCache[j];
			if ((Object)(object)applyEffectByPriority2 != (Object)null && applyEffectByPriority2.Priority == num2)
			{
				list.Add(applyEffectByPriority2);
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		for (int k = 0; k < list.Count; k++)
		{
			ApplyEffectByPriority applyEffectByPriority3 = list[k];
			if ((Object)(object)applyEffectByPriority3 != (Object)null)
			{
				applyEffectByPriority3.ResumeOnOtherHigherPriorityStop();
			}
		}
		mLowerPriorityCandidatesCache.Clear();
	}

	public void OnInterruptedByHigherPriority()
	{
		if (mAutoResume)
		{
			PausePlayable(pause: true);
		}
		else
		{
			myPlayable.Stop();
		}
	}

	public void ResumeOnOtherHigherPriorityStop()
	{
		if (mAutoResume)
		{
			PausePlayable(pause: false);
		}
	}

	private void PausePlayable(bool pause)
	{
		Component val = (((Object)(object)myPlayable != (Object)null) ? myPlayable.myApplyComponent : null);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		GameObject gameObject = val.gameObject;
		if ((Object)(object)gameObject == (Object)null)
		{
			return;
		}
		if (pause)
		{
			if (gameObject.activeSelf)
			{
				gameObject.SetActive(false);
			}
		}
		else if (!gameObject.activeSelf)
		{
			gameObject.SetActive(true);
		}
	}
}

}
