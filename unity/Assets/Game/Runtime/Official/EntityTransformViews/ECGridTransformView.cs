// Migrated complete original application class; see entity transform receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using AttachAgent = Zjcs.Official.StoryAttachment.AttachAgent;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityTransformViews
{
public class ECGridTransformView : ECViewBase, IGridTransformPosUpdatedEventReceiver, IEventReceiver
{
	[SerializeField]
	private bool mUpdatePosImmediately;

	[SerializeField]
	private bool mSetZ;

	[SerializeField]
	private float mOffsetZ;

	[SerializeField]
	private float mOffsetZPerX = 0.001f;

	[SerializeField]
	private Transform mFlipTransform;

	[Tooltip("客户端表现相关的Entity的Bounds配置")]
	[SerializeField]
	private List<Pos2d> mClientOverrideEntityDimensionBounds;

	private ECGridTransformData mGridTransformComponent;

	private Transform mTransform;

	private ECTransformView mECTransformView;

	public bool UpdatePosImmediately => mUpdatePosImmediately;

	public bool SetZ => mSetZ;

	public float OffsetZ => mOffsetZ;

	public Transform FlipTransform => mFlipTransform;

	public List<Pos2d> ClientOverrideEntityDimensionBounds => mClientOverrideEntityDimensionBounds;

	public event Action<ECGridTransformView, Pos2d> OnPosUpdated;

	protected override void Load()
	{
		mTransform = ((Component)((Component)mOwner.transform).GetComponentInParent<ECViewEntityBinding>(true)).transform;
		mGridTransformComponent = GetComponent<ECGridTransformData>();
		mGridTransformComponent.AddPosUpdatedReceiver(this);
	}

	protected override void Unload()
	{
		mGridTransformComponent.RemovePosUpdatedReceiver(this);
	}

	void IGridTransformPosUpdatedEventReceiver.GridTransform_OnPosUpdated(ECGridTransformData transform, Pos2d prePos)
	{
		if (UpdatePosImmediately)
		{
			ECViewMoveAgent component = ((Component)mOwner.transform.parent).GetComponent<ECViewMoveAgent>();
			if ((Object)(object)component != (Object)null && component.IsMoving)
			{
				component.Stop();
			}
			SetPositionByGrid(mGridTransformComponent.Pos);
		}
		try
		{
			OnPosUpdated?.Invoke(this, prePos);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	public Vector3 GridToPosition(Pos2d grid)
	{
		return mGridTransformComponent.GridToPosition(grid);
	}

	public Pos2d PositionToGrid(Vector3 pos)
	{
		return mGridTransformComponent.PositionToGrid(pos);
	}

	public void SetPositionByGrid(Pos2d grid)
	{
		SetPosition(GridToPosition(grid));
	}

	public void SetRotationByDirection(Direction dir)
	{
		SetRotation(dir.DirToRotation());
	}

	public void SetScaleByFlipX(bool flipX)
	{
		if ((Object)(object)mFlipTransform != (Object)null)
		{
			Vector3 localScale = mFlipTransform.localScale;
			if (flipX == localScale.x > 0f)
			{
				localScale.x *= -1f;
				mFlipTransform.localScale = localScale;
			}
		}
		else
		{
			Vector3 scale = (Vector3)(flipX ? new Vector3(-1f, 1f, 1f) : Vector3.one);
			SetScale(scale);
		}
	}

	public void ResetPosition()
	{
		SetPositionByGrid(mGridTransformComponent.Pos);
	}

	public void ResetRotation()
	{
		SetRotationByDirection(mGridTransformComponent.Direction);
	}

	public void ResetScale()
	{
		SetScaleByFlipX(mGridTransformComponent.FlipX);
	}

	public Vector3 GetPosition()
	{
		if (mECTransformView != null)
		{
			return mECTransformView.Position;
		}
		return mTransform.localPosition;
	}

	public static bool? GetFlipX(Vector3 fromPos, Vector3 toPos)
	{
		if (toPos.x == fromPos.x)
		{
			return null;
		}
		return toPos.x < fromPos.x;
	}

	public void SetPosition(Vector3 pos)
	{
		if (mSetZ)
		{
			float num = pos.x - mGridTransformComponent.Info.RealCenter.x;
			pos.z = ZSetUtils.YToZ(pos.y - mGridTransformComponent.Info.RealCenter.y) + mOffsetZ + mOffsetZPerX * num;
		}
		if (mECTransformView != null)
		{
			mECTransformView.Position = pos;
		}
		else
		{
			mTransform.localPosition = pos;
		}
	}

	private void SetRotation(Quaternion rot)
	{
		if (mECTransformView != null)
		{
			mECTransformView.Rotation = rot;
		}
		else
		{
			mTransform.localRotation = rot;
		}
	}

	private void SetScale(Vector3 scale)
	{
		if (mECTransformView != null)
		{
			mECTransformView.Scale = scale;
		}
		else
		{
			mTransform.localScale = scale;
		}
	}

	public List<Pos2d> GetClientChooseBodyRange()
	{
		if (ClientOverrideEntityDimensionBounds == null || ClientOverrideEntityDimensionBounds.Count == 0)
		{
			return mGridTransformComponent.Info.GetBodyRange();
		}
		return ClientOverrideEntityDimensionBounds;
	}

	public static Vector3 GridToVector3(Pos2d grid)
	{
		return new Vector3(1f * (float)grid.X, 1f * (float)grid.Y, 0f);
	}
}

}
