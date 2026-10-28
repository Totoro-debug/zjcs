// Migrated original grid presentation/query source; see Map13 grid-view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace Zjcs.Official.World.Map13EntityGridViews
{
[Serializable]
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

	private Map13GridTransformData mGridTransformComponent;

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
		mGridTransformComponent = GetComponent<Map13GridTransformData>();
		mGridTransformComponent.PosUpdatedEvent += (IGridTransformPosUpdatedEventReceiver)this;
	}

	protected override void Unload()
	{
		mGridTransformComponent.PosUpdatedEvent -= (IGridTransformPosUpdatedEventReceiver)this;
	}

	void IGridTransformPosUpdatedEventReceiver.GridTransform_OnPosUpdated(Map13GridTransformData transform, Pos2d prePos)
	{
		if (UpdatePosImmediately)
		{
			Map13MovementAgent component = ((Component)mOwner.transform.parent).GetComponent<Map13MovementAgent>();
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
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return mGridTransformComponent.GridToPosition(grid);
	}

	public Pos2d PositionToGrid(Vector3 pos)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return mGridTransformComponent.PositionToGrid(pos);
	}

	public void SetPositionByGrid(Pos2d grid)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		SetPosition(GridToPosition(grid));
	}

	public void SetRotationByDirection(Direction dir)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		SetRotation(dir.DirToRotation());
	}

	public void SetScaleByFlipX(bool flipX)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)mFlipTransform != (Object)null)
		{
			Vector3 localScale = mFlipTransform.localScale;
			if (flipX == (localScale.x > 0f))
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
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (mECTransformView != null)
		{
			return mECTransformView.Position;
		}
		return mTransform.localPosition;
	}

	public static bool? GetFlipX(Vector3 fromPos, Vector3 toPos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (toPos.x == fromPos.x)
		{
			return null;
		}
		return toPos.x < fromPos.x;
	}

	public void SetPosition(Vector3 pos)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		return new Vector3(1f * (float)grid.X, 1f * (float)grid.Y, 0f);
	}
}

}
