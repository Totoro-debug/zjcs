// Migrated original grid presentation/query source; see Map13 grid-view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using UnityEngine;


namespace Zjcs.Official.World.Map13EntityGridViews
{
[Serializable]
public class ECTransformView : ECViewBase
{
	[SerializeField]
	private bool mSetZ;

	private Transform mTransform;

	public Vector3 Position
	{
		get
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return mTransform.localPosition;
		}
		set
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			Vector3 val = value;
			if (mSetZ)
			{
				val.z = ZSetUtils.YToZ(val.y);
			}
			mTransform.localPosition = val;
			if (OnPosUpdated != null)
			{
				OnPosUpdated(this);
			}
		}
	}

	public Quaternion Rotation
	{
		get
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return mTransform.localRotation;
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			mTransform.localRotation = value;
			if (OnRotUpdated != null)
			{
				OnRotUpdated(this);
			}
		}
	}

	public Vector3 Scale
	{
		get
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return mTransform.localScale;
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			mTransform.localScale = value;
			if (OnScaleUpdated != null)
			{
				OnScaleUpdated(this);
			}
		}
	}

	public event Action<ECTransformView> OnPosUpdated;

	public event Action<ECTransformView> OnRotUpdated;

	public event Action<ECTransformView> OnScaleUpdated;

	protected override void Load()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		mTransform = ((Component)((Component)mOwner.transform).GetComponentInParent<ECViewEntityBinding>(true)).transform;
		if (GetView<ECGridTransformView>() == null)
		{
			Map13TransformData component = GetComponent<Map13TransformData>();
			Position = component.Position;
			Rotation = component.Rotation;
			Scale = component.Scale;
		}
	}
}

}
