// Migrated selected original source; see skill movement runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
using System.Collections.Generic;
using UnityEngine;

public class MyFightActionSkillMove : MyActionBase<FightActionSkillMoveComponent, FightActionSkillMoveComponentInfo>, IFightGetUsePlayable
{
	[Tooltip("移动Playable，source为移动者，target为目标点")]
	public GameObject myPlayablePrefab;

	[Tooltip("碰撞Playable，source为移动者，target为碰撞目标点")]
	public GameObject BlockPlayablePrefab;

	private MyPlayable mMovePlayable;

	private GameObject mTargetGo;

	private Dictionary<string, object> mParam;

	private int mCurrentPathIndex;

	private bool mMoving;

	private float mMoveDuration;

	private float mMoveTimer;

	private MyPlayable mBlockedPlayable;

	public IMyPlayableManager mMyPlayableManager { get; set; }
	public Transform EntitySlot { get; set; }

	// The battle host advances this same original timer at presentation boundaries.
	public bool ManualTiming { get; set; }
	public float RemainingMoveTime => mMoving ? Mathf.Max(0f, mMoveDuration - mMoveTimer) : float.PositiveInfinity;

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		mCurrentPathIndex = 0;
		PlayMovePlayable();
	}

	private void Update() { if (!ManualTiming) Advance(Time.deltaTime); }

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

	public override void OnEnd(object userData)
	{
		StopMove();
		StopBlockedPlayable();
		ResetGridTransform();
		mParam?.Clear();
		mCurrentPathIndex = 0;
		base.OnEnd(userData);
	}

	public override void OnImmediateToEnd()
	{
		ResetGridTransform();
		base.OnImmediateToEnd();
	}

	private void ResetGridTransform()
	{
		if ((Object)(object)myApplyEntity != (Object)null)
		{
			MyGridTransform myGridTransform = myApplyEntity.myGridTransform;
			myGridTransform.ResetPosition();
			myGridTransform.ResetRotation();
			myGridTransform.ResetScale();
		}
	}

	private void PlayMovePlayable(bool immediate = false)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)myPlayablePrefab == (Object)null || (Object)(object)myApplyEntity == (Object)null)
		{
			return;
		}
		List<Pos2d> path = Component.Path;
		if (mCurrentPathIndex >= path.Count)
		{
			StopMove();
			TryPlayBlockedPlayable(immediate);
			return;
		}
		Pos2d nowPos = GetNowPos();
		Pos2d nextMovePos = GetNextMovePos(immediate);
		DoMoveSourceTurn(path, mCurrentPathIndex, nowPos, nextMovePos);
		Vector3 targetPos = myApplyEntity.myGridTransform.GridToPosition(nextMovePos);
		GameObject moveTargetGo = GetMoveTargetGo(targetPos);
		Dictionary<string, object> playableParam = GetPlayableParam();
		mMovePlayable = mMyPlayableManager.Play(myPlayablePrefab, ((Component)myApplyEntity).gameObject, moveTargetGo, 1f, null, playableParam);
		if (immediate)
		{
			mCurrentPathIndex = path.Count;
		}
		else
		{
			mCurrentPathIndex++;
		}
		Vector3 localPosition = ((Component)myApplyEntity).transform.localPosition;
		localPosition.z = 0f;
		Vector3 val = localPosition - moveTargetGo.transform.localPosition;
		float magnitude = val.magnitude;
		float duration = Component.GetDuration(magnitude);
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

	private void DoMoveSourceTurn(List<Pos2d> path, int index, Pos2d startPos, Pos2d targetPos)
	{
		MyGridTransform myGridTransform = myApplyEntity.myGridTransform;
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
		if ((Object)(object)BlockPlayablePrefab == (Object)null)
		{
			return false;
		}
		if (!Component.BlockedPos.HasValue)
		{
			return false;
		}
		return true;
	}

	private void TryPlayBlockedPlayable(bool immediate)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (CheckBlockedPlayablePlayCondition(immediate))
		{
			Pos2d value = Component.BlockedPos.Value;
			Vector3 targetPos = myApplyEntity.myGridTransform.GridToPosition(value);
			GameObject moveTargetGo = GetMoveTargetGo(targetPos);
			float speed = Component.GetMoveSpeed() / 2f;
			mBlockedPlayable = mMyPlayableManager.Play(BlockPlayablePrefab, ((Component)myApplyEntity).gameObject, moveTargetGo, speed, null, GetPlayableParam());
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

	private GameObject GetMoveTargetGo(Vector3 targetPos)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected Obj, but got Unknown
		if ((Object)(object)mTargetGo == (Object)null)
		{
			string text = "tmp_skill_move_go";
			mTargetGo = new GameObject(text);
			mTargetGo.transform.SetParent(EntitySlot);
		}
		mTargetGo.transform.localPosition = targetPos;
		return mTargetGo;
	}

	private Pos2d GetNowPos()
	{
		return myApplyEntity.myGridTransform.GetClientGrid();
	}

	private Pos2d GetNextMovePos(bool immediate = false)
	{
		if (immediate)
		{
			return Component.Path[Component.Path.Count - 1];
		}
		return Component.Path[mCurrentPathIndex];
	}

	private Dictionary<string, object> GetPlayableParam()
	{
		if (mParam == null)
		{
			mParam = new Dictionary<string, object> { { "ParamSource2TargetDirection", Component.Direction } };
		}
		else
		{
			mParam["ParamSource2TargetDirection"] = Component.Direction;
		}
		return mParam;
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)myPlayablePrefab != (Object)null)
			{
				playables.Add(myPlayablePrefab);
			}
			if ((Object)(object)BlockPlayablePrefab != (Object)null)
			{
				playables.Add(BlockPlayablePrefab);
			}
		}
	}
}

}
