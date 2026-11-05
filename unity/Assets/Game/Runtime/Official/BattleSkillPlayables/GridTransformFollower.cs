// Original GridTransformFollower algorithm; local grid/configuration/time injection only.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleSkillPlayables
{
public class GridTransformFollower : MonoBehaviour, IGridTransformPosUpdatedEventReceiver, IEventReceiver
{
	private struct MovingData
	{
		public Vector3 Pos { get; private set; }

		public bool Valid { get; private set; }

		public MovingData(Vector3 pos)
		{

			Pos = pos;
			Valid = true;
		}
	}

	private enum EState
	{
		Stop,
		Moving,
		Pause
	}

	[Header("表现配置")]

	[SerializeField]
	private float mMoveGridTime;

	[SerializeField]
	private MyTransform mMyTransform;

	[SerializeField]
	private Transform mModelTransform;

	[SerializeField]
	private GameObject mMoveAnimPlayable;

	[SerializeField]
	private float mStartMovingDelay;

	[Tooltip("小于等于0不进行传送判断")]
	[SerializeField]
	private int mTeleportDistance = 1;

	[Tooltip("是否跟随位置的z值")]
	[SerializeField]
	private bool mFollowPosZ = true;

	[Tooltip("是否使用最新的移动数据进行移动。如宠物跟随时，角色已经移动了两段，这时候宠物再次尝试下一次跟随移动时，True取新的移动数据，False按照顺序移动。")]
	[SerializeField]
	private bool mUseLastMovingData;

	private Transform mFollowTarget;

	private MyPlayable mRunningPlayble;

	private MyTransform mFollowedTargetTransform;

	private float mTempMoveGridTime;

	private Vector3 mStandOffsetX;

	private Queue<MovingData> mMoveQuene;

	private bool mPrepareNext;

	private Vector3 mStartPos;

	private Vector3 mEndPos;

	private float mStartMovingDelayTimer;

	private float mMovingTimer;

	private float mMovingTimeCost;

	private EState mMoveState;

	private ECGridTransformData mECGridTransformData;

	public IMyPlayableManager MyPlayableManager { private get; set; }

	public event Action OnResetPos;

	private void Reset()
	{
		mMyTransform = ((Component)this).GetComponent<MyTransform>();
	}

	private void Awake()
	{








		mMoveQuene = new Queue<MovingData>();
		Vector3 localPosition = mModelTransform.localPosition;
		mStandOffsetX = new Vector3(localPosition.x, 0f, 0f);
		mModelTransform.localPosition = new Vector3(0f, localPosition.y, localPosition.z);
	}

	private void OnDestroy()
	{

		Release();
		mMoveQuene = null;
		mStandOffsetX = default;
	}

	public void Init(Transform followTarget, ECGridTransformData grid)
	{






		mFollowTarget = followTarget;
		SetMoveGridTime(mMoveGridTime);
		mFollowedTargetTransform = followTarget.GetComponent<MyTransform>();
		mStartPos = ((Component)this).transform.localPosition + GetFaceDirStandPosOffsetX();
		mEndPos = mStartPos;
		mECGridTransformData = grid;
		mECGridTransformData.AddPosUpdatedReceiver(this);
		if (mFollowedTargetTransform) mFollowedTargetTransform.OnScaleUpdated += MyTransform_OnScaleUpdated;
		ResetPos();
	}

	public void Release()
	{
		if (mMoveQuene == null) return; // Unity teardown may already have destroyed this follower.




		StopMoving();
		ResetMoveGridTime();
		if (mECGridTransformData != null)
		{
			mECGridTransformData.RemovePosUpdatedReceiver(this);
		}
		if ((Object)(object)mFollowedTargetTransform != (Object)null)
		{
			mFollowedTargetTransform.OnScaleUpdated -= MyTransform_OnScaleUpdated;
		}
		mFollowTarget = null;
		mFollowedTargetTransform = null;
		mStartPos = Vector3.zero;
		mEndPos = Vector3.zero;
	}

	public void SetMoveGridTime(float moveGridTime)
	{
		mTempMoveGridTime = moveGridTime;
	}

	public void ResetMoveGridTime()
	{
		mTempMoveGridTime = mMoveGridTime;
	}

	public void Tick(float deltaTime)
	{




















		if (mMoveState != EState.Moving)
		{
			return;
		}
		mStartMovingDelayTimer += deltaTime;
		if (mStartMovingDelayTimer < mStartMovingDelay)
		{
			return;
		}
		if (mPrepareNext)
		{
			if (!TryGetNextMoveData(out var movingData))
			{
				StopMoving();
				return;
			}
			mPrepareNext = false;
			mStartPos = GetPosition();
			mEndPos = movingData.Pos + ((mMoveQuene.Count <= 0) ? GetFaceDirStandPosOffsetX() : Vector3.zero);
			mMovingTimeCost = mTempMoveGridTime * GetChebyshevDistance((Vector2)mStartPos, (Vector2)mEndPos);
			mMovingTimer = 0f;
			bool flipX = mFollowTarget.localScale.x < 0f;
			bool? flipX2 = ECGridTransformView.GetFlipX(mStartPos, mEndPos);
			if (flipX2.HasValue)
			{
				flipX = flipX2.Value;
			}
			SetFlipX(flipX);
		}
		if (mMovingTimer <= mMovingTimeCost)
		{
			mMovingTimer += deltaTime;
			float num = mMovingTimer / mMovingTimeCost;
			Vector3 position = Vector3.Lerp(mStartPos, mEndPos, num);
			SetPosition(position);
		}
		else if (mMoveQuene.Count <= 0)
		{
			StopMoving();
			SetPosition(mEndPos);
		}
		else
		{
			mPrepareNext = true;
		}
	}

	private void SetMoveState(EState state)
	{
		if (state != mMoveState)
		{
			mMoveState = state;
		}
	}

	private bool TryGetNextMoveData(out MovingData movingData)
	{
		movingData = default;
		if (mMoveQuene.Count == 0)
		{
			return false;
		}
		bool result = false;
		MovingData movingData2 = default;
		while (mMoveQuene.TryDequeue(out movingData2))
		{
			movingData = movingData2;
			result = true;
			if (!mUseLastMovingData)
			{
				break;
			}
		}
		return result;
	}

	private void StopMoving()
	{

		if ((Object)(object)mFollowTarget != (Object)null)
		{
			SetFlipX(mFollowTarget.localScale.x < 0f);
		}
		SetMoveState(EState.Stop);
		mMoveQuene.Clear();
		StopMovePlayable();
	}

	void IGridTransformPosUpdatedEventReceiver.GridTransform_OnPosUpdated(ECGridTransformData transform, Pos2d prePos)
	{



















		if (mTeleportDistance > 0 && Pos2d.Distance(prePos, transform.Pos) > mTeleportDistance)
		{
			ResetPos();
			return;
		}
		Vector3 val = transform.GridToPosition(prePos);
		val = new Vector3(val.x, val.y);
		Vector3 val2 = transform.GridToPosition(transform.Pos);
		val2 = new Vector3(val2.x, val2.y);
		float chebyshevDistance = GetChebyshevDistance((Vector2)val, (Vector2)val2);
		if (mTeleportDistance <= 0 || !(GetChebyshevDistance((Vector2)GetPosition(), (Vector2)val2) < chebyshevDistance))
		{
			MovingData item = new MovingData(val2);
			mMoveQuene.Enqueue(item);
			if (mMoveState == EState.Stop)
			{
				PlayMovePlayable();
				SetMoveState(EState.Moving);
				mPrepareNext = true;
				mStartMovingDelayTimer = 0f;
				mMovingTimer = 0f;
			}
		}
	}

	private void MyTransform_OnScaleUpdated(MyTransform obj)
	{

		if (mMoveState == EState.Stop)
		{
			SetFlipX(obj.Scale.x < 0f);
		}
	}

	private static float GetChebyshevDistance(Vector2 start, Vector2 end)
	{




		return Mathf.Max(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y));
	}

	private Vector3 GetFaceDirStandPosOffsetX()
	{



		return mStandOffsetX * ((mFollowTarget.localScale.x < 0f) ? (-1f) : 1f);
	}

	public void TryNextMove()
	{
		if (mMoveState != EState.Pause && mMoveQuene.Count > 0)
		{
			mPrepareNext = true;
		}
	}

	public void UpdateFlipX()
	{

		float num = mEndPos.x - GetPosition().x;
		if (!FloatUtils.IsZero(num))
		{
			SetFlipX(num < 0f);
		}
	}

	public void ProofreadPos()
	{










		Vector3 val = mECGridTransformData.GridToPosition(mECGridTransformData.Pos);
		val = new Vector3(val.x, val.y);
		if (mMoveQuene.Count == 0 && GetPosition() == val + GetFaceDirStandPosOffsetX())
		{
			SetMoveState(EState.Stop);
			StopMovePlayable();
			return;
		}
		MovingData item = new MovingData(val);
		mMoveQuene.Enqueue(item);
		UpdateFlipX();
		PlayMovePlayable();
		SetMoveState(EState.Moving);
		mPrepareNext = true;
		mStartMovingDelayTimer = 0f;
		mMovingTimer = 0f;
	}

	public void Pause()
	{
		SetMoveState(EState.Pause);
	}

	private void PlayMovePlayable()
	{
		StopMovePlayable();
		mRunningPlayble = MyPlayableManager.Play(mMoveAnimPlayable, ((Component)this).gameObject, ((Component)this).gameObject);
	}

	private void StopMovePlayable()
	{
		if (!((Object)(object)mRunningPlayble == (Object)null))
		{
			mRunningPlayble.Stop();
			mRunningPlayble = null;
		}
	}

	private Vector3 GetPosition()
	{


		if ((Object)(object)mMyTransform != (Object)null)
		{
			return mMyTransform.Position;
		}
		return ((Component)this).transform.localPosition;
	}

	private void SetPosition(Vector3 position)
	{







		Vector3 val = position;
		if (!mFollowPosZ)
		{
			Vector3 position2 = GetPosition();
			val.z = position2.z;
		}
		if ((Object)(object)mMyTransform != (Object)null)
		{
			mMyTransform.Position = val;
		}
		else
		{
			((Component)this).transform.localPosition = val;
		}
	}

	private void ResetPos()
	{








		StopMoving();
		Vector3 val = mECGridTransformData.GridToPosition(mECGridTransformData.Pos);
		SetPosition(new Vector3(val.x, val.y) + GetFaceDirStandPosOffsetX());
		SetFlipX(mFollowTarget.localScale.x < 0f);
		OnResetPos?.Invoke();
	}

	private void SetFlipX(bool flipX)
	{




		if ((Object)(object)mMyTransform != (Object)null)
		{
			mMyTransform.Scale = GetScaleByFlipX(flipX, mMyTransform.Scale);
		}
		else
		{
			((Component)this).transform.localScale = GetScaleByFlipX(flipX, ((Component)this).transform.localScale);
		}
	}

	private static Vector3 GetScaleByFlipX(bool flipX, Vector3 scale)
	{

		scale.x = ((!flipX) ? 1 : (-1));
		return scale;
	}

    // Explicit local configuration replaces serialized prefab injection only.
    public void Configure(float moveGridTime, Transform model, GameObject movePlayable,
        float startDelay, int teleportDistance, bool followPosZ, bool useLastMovingData)
    {
        mMoveGridTime = moveGridTime; mModelTransform = model; mMoveAnimPlayable = movePlayable;
        mMyTransform = transform.GetComponent<MyTransform>();
        mStartMovingDelay = startDelay; mTeleportDistance = teleportDistance;
        mFollowPosZ = followPosZ; mUseLastMovingData = useLastMovingData;
    }

    public void NotifyTargetScaleUpdated()
    {
        if (mMoveState == EState.Stop) SetFlipX(mFollowTarget.localScale.x < 0f);
    }
}
}
