// Current official movement view. The battle host supplies the original update
// delta at presentation boundaries; local action lookup replaces server entities.
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityActions;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.BattleSkillPlayables
{
[System.Serializable]
public class ECFightActionSkillMoveView : ECViewBase, IFightGetUsePlayable, IFightActionSkillMoveView
{
	[Tooltip("移动Playable，source为移动者，target为目标点")]
	[SerializeField]
	private GameObject mPlayablePrefab;

	[Tooltip("碰撞Playable，source为移动者，target为碰撞目标点")]
	[SerializeField]
	private GameObject mBlockPlayablePrefab;

	private FightActionSkillMoveComponent mSkillMoveComponent;

	private MyEntity mRoot;

	private MyEntity mApplyEntity;

	private MyPlayable mMovePlayable;

	private MyPlayable mBlockedPlayable;

	private GameObject mTargetGo;

	private Dictionary<string, object> mParam;

	private int mCurrentPathIndex;

	private bool mMoving;

	private float mMoveDuration;

	private float mMoveTimer;

	public IMyPlayableManager MyPlayableManager { get; set; }
	public IMyActionEntityLookup EntityLookup { get; set; }
	public Transform EntitySlot { get; set; }
	public GameObject BlockPlayablePrefab => mBlockPlayablePrefab;
	public float RemainingMoveTime => mMoving ? Mathf.Max(0f, mMoveDuration - mMoveTimer) : float.PositiveInfinity;
	public bool BlockedPlaybackPending => mMoving || mBlockedPlayable && mBlockedPlayable.myStatus != MyPlayable.PlayableStatus.Stop;

	protected override void Load()
	{
		base.Load();
		mCurrentPathIndex = 0;
		PlayMovePlayable();
	}

	protected override void Unload()
	{
		StopMove();
		StopBlockedPlayable();
		ResetGridTransform();
		mParam?.Clear();
		mCurrentPathIndex = 0;
		mApplyEntity = null;
		mSkillMoveComponent = null;
		mRoot = null;
		base.Unload();
	}

	public void Advance(float deltaTime)
	{
		if (mMoving)
		{
			mMoveTimer += deltaTime;
			if (mMoveTimer >= mMoveDuration)
			{
				OnSingleMoveEnded();
			}
		}
	}

	private void PlayMovePlayable(bool immediate = false)
	{
		MyEntity myEntity = ResolveApplyEntity();
		if ((Object)(object)mPlayablePrefab == (Object)null || (Object)(object)myEntity == (Object)null)
		{
			return;
		}
		FightActionSkillMoveComponent fightActionSkillMoveComponent = ResolveComponent();
		if (fightActionSkillMoveComponent == null)
		{
			return;
		}
		List<Pos2d> path = fightActionSkillMoveComponent.Path;
		if (mCurrentPathIndex >= path.Count)
		{
			StopMove();
			TryPlayBlockedPlayable(immediate);
			return;
		}
		Pos2d nowPos = GetNowPos(myEntity);
		Pos2d nextMovePos = GetNextMovePos(path, immediate);
		DoMoveSourceTurn(myEntity, path, mCurrentPathIndex, nowPos, nextMovePos);
		Vector3 gridPosition = myEntity.myGridTransform.GridToPosition(nextMovePos);
		GameObject moveTargetGo = GetMoveTargetGo(gridPosition);
		Dictionary<string, object> playableParam = GetPlayableParam(fightActionSkillMoveComponent);
		mMovePlayable = MyPlayableManager.Play(mPlayablePrefab, ((Component)myEntity).gameObject, moveTargetGo, 1f, null, playableParam);
		if (immediate)
		{
			mCurrentPathIndex = path.Count;
		}
		else
		{
			mCurrentPathIndex++;
		}
		Vector3 localPosition = ((Component)myEntity).transform.localPosition;
		localPosition.z = 0f;
		Vector3 val = localPosition - moveTargetGo.transform.localPosition;
		float magnitude = val.magnitude;
		float duration = fightActionSkillMoveComponent.GetDuration(magnitude);
		if (immediate)
		{
			OnSingleMoveEnded();
			return;
		}
		StopMoveTimer();
		SetSingleMoveTimer(duration);
	}

	private void OnSingleMoveEnded()
	{
		StopMove();
		PlayMovePlayable();
	}

	private void DoMoveSourceTurn(MyEntity applyEntity, List<Pos2d> path, int index, Pos2d startPos, Pos2d targetPos)
	{
		MyGridTransform myGridTransform = applyEntity.myGridTransform;
		if (index == path.Count - 1)
		{
			myGridTransform.ResetScale();
			return;
		}
		bool scaleByFlipX = FightMovementViewMath.CalcFlipX(startPos, targetPos, myGridTransform.FlipX);
		myGridTransform.SetScaleByFlipX(scaleByFlipX);
	}

	private void SetSingleMoveTimer(float time)
	{
		mMoveDuration = time;
		mMoving = true;
	}

	private void StopMove()
	{
		if ((Object)(object)mMovePlayable != (Object)null)
		{
			mMovePlayable.Stop();
			mMovePlayable = null;
		}
		StopMoveTimer();
	}

	private void StopMoveTimer()
	{
		mMoving = false;
		mMoveDuration = 0f;
		mMoveTimer = 0f;
	}

	private bool CheckBlockedPlayablePlayCondition(bool immediate)
	{
		if (immediate)
		{
			return false;
		}
		if ((Object)(object)mBlockPlayablePrefab == (Object)null)
		{
			return false;
		}
		FightActionSkillMoveComponent fightActionSkillMoveComponent = ResolveComponent();
		if (fightActionSkillMoveComponent == null || !fightActionSkillMoveComponent.BlockedPos.HasValue)
		{
			return false;
		}
		return true;
	}

	private void TryPlayBlockedPlayable(bool immediate)
	{
		if (CheckBlockedPlayablePlayCondition(immediate))
		{
			MyEntity myEntity = ResolveApplyEntity();
			FightActionSkillMoveComponent fightActionSkillMoveComponent = ResolveComponent();
			if (!((Object)(object)myEntity == (Object)null) && fightActionSkillMoveComponent != null && fightActionSkillMoveComponent.BlockedPos.HasValue)
			{
				Pos2d value = fightActionSkillMoveComponent.BlockedPos.Value;
				Vector3 gridPosition = myEntity.myGridTransform.GridToPosition(value);
				GameObject moveTargetGo = GetMoveTargetGo(gridPosition);
				float speed = fightActionSkillMoveComponent.GetMoveSpeed() / 2f;
				mBlockedPlayable = MyPlayableManager.Play(mBlockPlayablePrefab, ((Component)myEntity).gameObject, moveTargetGo, speed, null, GetPlayableParam(fightActionSkillMoveComponent));
			}
		}
	}

	private void StopBlockedPlayable()
	{
		if ((Object)(object)mBlockedPlayable != (Object)null)
		{
			mBlockedPlayable.Stop();
			mBlockedPlayable = null;
		}
	}

	private GameObject GetMoveTargetGo(Vector3 gridPosition)
	{
		if ((Object)(object)mTargetGo == (Object)null)
		{
			string text = "tmp_skill_move_go";
			mTargetGo = new GameObject(text);
			mTargetGo.transform.SetParent(mOwner.transform, false);
		}
		MyEntity myEntity = ResolveApplyEntity();
		if ((Object)(object)myEntity != (Object)null && (Object)(object)EntitySlot != (Object)null)
		{
			mTargetGo.transform.position = EntitySlot.TransformPoint(gridPosition);
		}
		else
		{
			mTargetGo.transform.position = gridPosition;
		}
		return mTargetGo;
	}

	private Pos2d GetNowPos(MyEntity applyEntity)
	{
		return applyEntity.myGridTransform.GetClientGrid();
	}

	private Pos2d GetNextMovePos(List<Pos2d> path, bool immediate = false)
	{
		if (immediate)
		{
			return path[path.Count - 1];
		}
		return path[mCurrentPathIndex];
	}

	private Dictionary<string, object> GetPlayableParam(FightActionSkillMoveComponent component)
	{
		if (mParam == null)
		{
			mParam = new Dictionary<string, object> { { "ParamSource2TargetDirection", component.Direction } };
		}
		else
		{
			mParam["ParamSource2TargetDirection"] = component.Direction;
		}
		// Local collision receipt identity; original playable direction is unchanged.
		var root = ResolveRoot();
		if (root) mParam["BlockedAction"] = root.gameObject;
		return mParam;
	}

	private void ResetGridTransform()
	{
		MyEntity myEntity = ResolveApplyEntity();
		if (!((Object)(object)myEntity == (Object)null))
		{
			MyGridTransform myGridTransform = myEntity.myGridTransform;
			myGridTransform.ResetPosition();
			myGridTransform.ResetRotation();
			myGridTransform.ResetScale();
		}
	}

	private MyEntity ResolveRoot()
	{
		if ((Object)(object)mRoot == (Object)null)
		{
			mRoot = mOwner.GetComponentInParent<MyEntity>(true);
		}
		return mRoot;
	}

	private FightActionSkillMoveComponent ResolveComponent()
	{
		if (mSkillMoveComponent == null)
		{
			MyEntity myEntity = ResolveRoot();
			if ((Object)(object)myEntity != (Object)null && myEntity.Entity != null)
			{
				mSkillMoveComponent = myEntity.Entity.GetComponent<FightActionSkillMoveComponent>();
			}
		}
		return mSkillMoveComponent;
	}

	private MyEntity ResolveApplyEntity()
	{
		if ((Object)(object)mApplyEntity == (Object)null)
		{
			var action = GetComponent<ActionComponentData>();
			if (action != null) mApplyEntity = EntityLookup.GetEntity(action.ApplyId);
		}
		return mApplyEntity;
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)mPlayablePrefab != (Object)null)
			{
				playables.Add(mPlayablePrefab);
			}
			if ((Object)(object)mBlockPlayablePrefab != (Object)null)
			{
				playables.Add(mBlockPlayablePrefab);
			}
		}
	}
}

}
