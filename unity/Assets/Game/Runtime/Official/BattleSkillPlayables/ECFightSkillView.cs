// Current official EC view; explicit entity/DI boundaries are recorded in its source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleSkillPlayables
{
[System.Serializable]
public class ECFightSkillView : ECViewBase, IFightGetUsePlayable
{
	[Tooltip("释放者对技能目标的播放效果")]
	[SerializeField]
	private GameObject mPlayablePrefab;

	[Tooltip("技能上方向playable")]
	[SerializeField]
	private GameObject mUpPlayablePrefab;

	[Tooltip("技能下方向playable")]
	[SerializeField]
	private GameObject mDownPlayablePrefab;

	[Tooltip("技能左方向playable")]
	[SerializeField]
	private GameObject mLeftPlayablePrefab;

	[Tooltip("技能右方向playable")]
	[SerializeField]
	private GameObject mRightPlayablePrefab;

	[Tooltip("显示技能重播")]
	[SerializeField]
	private bool mShowSkillReplay = true;

	[Tooltip("是否优先使用位置作为播放终点")]
	[SerializeField]
	private bool mPreferredPosToPlayable = true;

	private FightSkillComponent mSkillComponent;

	private MyPlayable mPlayable;

	private IDisposable mInterruptSubscription;

	private MyEntity mSkillRoot;

	private GameObject mSkillPlayableTargetGo;

	public IMyPlayableManager MyPlayableManager { get; set; }
	public IFightSkillViewServices ViewServices { get; set; }

	public bool ShowSkillReplay => mShowSkillReplay;

	protected override void Load()
	{
		base.Load();
		mSkillComponent = GetComponent<FightSkillComponent>();
		if (mSkillComponent == null)
		{
			return;
		}
		MyEntity componentInParent = ((Component)mOwner.transform).GetComponentInParent<MyEntity>(true);
		if ((Object)(object)componentInParent == (Object)null)
		{
			return;
		}
		mSkillRoot = componentInParent;
		MyContainer myContainer = componentInParent.myContainer;
		if ((Object)(object)myContainer == (Object)null)
		{
			return;
		}
		MyEntity entity = ViewServices.GetEntity(mSkillComponent.SrcEntityId);
		if (!((Object)(object)entity == (Object)null))
		{
			DoSourceTurn(entity);
			if (!mSkillComponent.Interrupted)
			{
				mSkillComponent.OnInterruped += OnSkillInterrupted;
				mInterruptSubscription = new CurrentFightViewSubscription(() => mSkillComponent.OnInterruped -= OnSkillInterrupted);
				PlayPlayable(componentInParent, entity);
			}
		}
	}

	protected override void Unload()
	{
		mInterruptSubscription?.Dispose();
		mInterruptSubscription = null;
		mSkillComponent = null;
		mSkillRoot = null;
		mPlayable = null;
		base.Unload();
	}

	private void DoSourceTurn(MyEntity source)
	{
		if (mSkillComponent.Info.FlipSource)
		{
			MyGridTransform component = ((Component)source).GetComponent<MyGridTransform>();
			if ((Object)(object)component != (Object)null)
			{
				component.ResetScale();
			}
		}
	}

	private void OnSkillInterrupted(FightSkillComponent skill)
	{
		if ((Object)(object)mPlayable != (Object)null)
		{
			mPlayable.Stop();
		}
	}

	private void PlayPlayable(MyEntity skillRoot, MyEntity source)
	{
		GameObject playablePrefab = GetPlayablePrefab();
		if (!((Object)(object)playablePrefab == (Object)null))
		{
			Dictionary<string, object> param = new Dictionary<string, object> {
			{
				"Skill",
				((Component)skillRoot).gameObject
			} };
			mPlayable = MyPlayableManager.Play(playablePrefab, ((Component)source).gameObject, GetPlayableTargetGo(), 1f, null, param);
		}
	}

	public GameObject GetPlayableTargetGo()
	{
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Expected Obj, but got Unknown
		if (mSkillComponent == null || (Object)(object)mSkillRoot == (Object)null)
		{
			return null;
		}
		MyEntity entity = ViewServices.GetEntity(mSkillComponent.TargetId);
		if (!mPreferredPosToPlayable && mSkillComponent.Info.IsEntityTargetType() && (Object)(object)entity != (Object)null)
		{
			return ((Component)entity).gameObject;
		}
		IFightSkillViewServices component = ViewServices;
		if (component == null)
		{
			return null;
		}
		if ((Object)(object)mSkillPlayableTargetGo == (Object)null)
		{
			mSkillPlayableTargetGo = new GameObject($"skill_{mSkillComponent.Entity.ClassId}_target");
			mReleaseCancellationToken.Register(DestroyPlayableTargetGo);
		}
		Transform parent = ((Component)mSkillRoot).transform.parent;
		mSkillPlayableTargetGo.transform.SetParent(parent);
		mSkillPlayableTargetGo.transform.position = parent.TransformPoint(component.GridToPos(mSkillComponent.TargetPos));
		return mSkillPlayableTargetGo;
	}

	private void DestroyPlayableTargetGo()
	{
		if (!((Object)(object)mSkillPlayableTargetGo == (Object)null))
		{
			Object.Destroy((Object)(object)mSkillPlayableTargetGo);
			mSkillPlayableTargetGo = null;
		}
	}

	private GameObject GetPlayablePrefab()
	{
		GameObject directionPlayable = GetDirectionPlayable(out var tag);
		if ((Object)(object)directionPlayable == (Object)null)
		{
			tag = "myPlayablePrefab";
			directionPlayable = mPlayablePrefab;
		}
		BattlePlayableOverrideView eCPlayableOverrideView = (((Object)(object)mSkillRoot != (Object)null) ? CurrentFightViewLookup.GetOverride(mSkillRoot) : null);
		if (eCPlayableOverrideView == null)
		{
			return directionPlayable;
		}
		return eCPlayableOverrideView.Resolve(tag, directionPlayable);
	}

	private GameObject GetDirectionPlayable(out string tag)
	{
		tag = null;
		ECGridTransformData component = GetComponent<ECGridTransformData>();
		if (component == null)
		{
			return null;
		}
		switch (component.Direction)
		{
		case Direction.Up:
			tag = "UpPlayablePrefab";
			return mUpPlayablePrefab;
		case Direction.Down:
			tag = "DownPlayablePrefab";
			return mDownPlayablePrefab;
		case Direction.Left:
			tag = "LeftPlayablePrefab";
			return mLeftPlayablePrefab;
		case Direction.Right:
			tag = "RightPlayablePrefab";
			return mRightPlayablePrefab;
		default:
			return null;
		}
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)mPlayablePrefab != (Object)null)
			{
				playables.Add(mPlayablePrefab);
			}
			if ((Object)(object)mUpPlayablePrefab != (Object)null)
			{
				playables.Add(mUpPlayablePrefab);
			}
			if ((Object)(object)mDownPlayablePrefab != (Object)null)
			{
				playables.Add(mDownPlayablePrefab);
			}
			if ((Object)(object)mLeftPlayablePrefab != (Object)null)
			{
				playables.Add(mLeftPlayablePrefab);
			}
			if ((Object)(object)mRightPlayablePrefab != (Object)null)
			{
				playables.Add(mRightPlayablePrefab);
			}
		}
	}
}

}
