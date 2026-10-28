// Complete original client lifecycle class; see entity lifecycle source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityLifecycle
{
public class MyGridTransform : MyComponent<ECGridTransformData, ECGridTransformInfo>, IGridTransformPosUpdatedEventReceiver, IEventReceiver
{
	private static readonly Vector3 kFlipXScale = new Vector3(-1f, 1f, 1f);

	private static readonly Vector3 kNotFlipXScale = Vector3.one;

	public bool UpdatePosImmediately;

	[SerializeField]
	private bool mSetZ;

	[SerializeField]
	private float mOffsetZ;

	[SerializeField]
	private Transform mFlipTransform;

	[Tooltip("客户端表现相关的Entity的Bounds配置")]
	public List<Pos2d> ClientOverrideEntityDimensionBounds;

	private EntityGridGroundData mEntityGridGroundData;

	private ECGridTransformView mECGridTransformView;

	public EntityGridGroundData GridGround
	{
		get
		{
			if (mEntityGridGroundData == null)
			{
				mEntityGridGroundData = myContainer.MyRootEntity.Entity.GetComponent<EntityGridGroundData>();
			}
			return mEntityGridGroundData;
		}
	}

	public Pos2d Pos => Component.Pos;

	public Direction Direction => Component.Direction;

	public bool FlipX => Component.FlipX;

	public bool UpdatePosImmediatelyProxy
	{
		get
		{
			if (mECGridTransformView != null)
			{
				return mECGridTransformView.UpdatePosImmediately;
			}
			return UpdatePosImmediately;
		}
	}

	private bool mSetZProxy
	{
		get
		{
			if (mECGridTransformView != null)
			{
				return mECGridTransformView.SetZ;
			}
			return mSetZ;
		}
	}

	private float mOffsetZProxy
	{
		get
		{
			if (mECGridTransformView != null)
			{
				return mECGridTransformView.OffsetZ;
			}
			return mOffsetZ;
		}
	}

	private Transform mFlipTransformProxy
	{
		get
		{
			if (mECGridTransformView != null)
			{
				return mECGridTransformView.FlipTransform;
			}
			return mFlipTransform;
		}
	}

	public List<Pos2d> ClientOverrideEntityDimensionBoundsProxy
	{
		get
		{
			if (mECGridTransformView != null)
			{
				return mECGridTransformView.ClientOverrideEntityDimensionBounds;
			}
			return ClientOverrideEntityDimensionBounds;
		}
	}

	public event Action<MyGridTransform, Pos2d> OnPosUpdated;

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		ECViewDriver componentInChildren = ((Component)this).GetComponentInChildren<ECViewDriver>(true);
		mECGridTransformView = (((Object)(object)componentInChildren != (Object)null) ? componentInChildren.GetView<ECGridTransformView>() : null);
		SetPositionByGrid(Pos);
		SetRotationByDirection(Direction);
		SetScaleByFlipX(FlipX);
		Component.AddPosUpdatedReceiver(this);
	}

	public override void OnEnd(object userData)
	{
		base.OnEnd(userData);
		if (Component != null)
		{
			Component.RemovePosUpdatedReceiver(this);
		}
	}

	public void GridTransform_OnPosUpdated(ECGridTransformData transform, Pos2d prePos)
	{


		if (UpdatePosImmediatelyProxy)
		{
			ECViewMoveAgent component = ((Component)myEntity).GetComponent<ECViewMoveAgent>();
			if ((Object)(object)component != (Object)null && component.IsMoving)
			{
				component.Stop();
			}
			SetPositionByGrid(Pos);
		}
		try
		{
			OnPosUpdated?.Invoke(this, prePos);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogException(ex);
		}
	}

	public Vector3 GetLogicPosition()
	{

		return GridToPosition(Pos);
	}

	public Pos2d GetClientGrid()
	{

		return PositionToGrid(GetPosition());
	}

	public Vector3 GridToPosition(Pos2d grid)
	{

		return Component.GridToPosition(grid);
	}

	public Pos2d PositionToGrid(Vector3 pos)
	{

		return Component.PositionToGrid(pos);
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








		if ((Object)(object)mFlipTransformProxy != (Object)null)
		{
			Vector3 localScale = mFlipTransformProxy.localScale;
			if (flipX == localScale.x > 0f)
			{
				localScale.x *= -1f;
				mFlipTransformProxy.localScale = localScale;
			}
		}
		else
		{
			Vector3 scale = (flipX ? kFlipXScale : kNotFlipXScale);
			SetScale(scale);
		}
	}

	public bool GetFlipXByScale()
	{

		if (myTransform.Scale.x == kFlipXScale.x)
		{
			return true;
		}
		return false;
	}

	public bool GetVisualFlipX()
	{


		if ((Object)(object)mFlipTransformProxy != (Object)null)
		{
			return mFlipTransformProxy.localScale.x < 0f;
		}
		return myTransform.Scale.x < 0f;
	}

	public void ResetPosition()
	{
		SetPositionByGrid(Component.Pos);
	}

	public void ResetRotation()
	{
		SetRotationByDirection(Component.Direction);
	}

	public void ResetScale()
	{
		SetScaleByFlipX(Component.FlipX);
	}

	public Vector3 GetPosition()
	{


		if ((Object)(object)myTransform != (Object)null)
		{
			return myTransform.Position;
		}
		return ((Component)this).transform.localPosition;
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




		if (mSetZProxy)
		{
			float x = pos.x - Info.RealCenter.x;
			pos.z = ZSetUtils.XYToZ(x, pos.y - Info.RealCenter.y) + mOffsetZProxy;
		}
		if ((Object)(object)myTransform != (Object)null)
		{
			myTransform.Position = pos;
		}
		else
		{
			((Component)this).transform.localPosition = pos;
		}
	}

	private void SetRotation(Quaternion rot)
	{


		if ((Object)(object)myTransform != (Object)null)
		{
			myTransform.Rotation = rot;
		}
		else
		{
			((Component)this).transform.localRotation = rot;
		}
	}

	private void SetScale(Vector3 scale)
	{


		if ((Object)(object)myTransform != (Object)null)
		{
			myTransform.Scale = scale;
		}
		else
		{
			((Component)this).transform.localScale = scale;
		}
	}

	public List<Pos2d> GetClientChooseBodyRange()
	{
		if (ClientOverrideEntityDimensionBoundsProxy == null || ClientOverrideEntityDimensionBoundsProxy.Count == 0)
		{
			return Info.GetBodyRange();
		}
		return ClientOverrideEntityDimensionBoundsProxy;
	}

	public static Vector3 GridToVector3(Pos2d grid)
	{

		return new Vector3(1f * (float)grid.X, 1f * (float)grid.Y, 0f);
	}

	static MyGridTransform()
	{




	}
}

}
