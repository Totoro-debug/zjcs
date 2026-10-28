// Migrated complete original application class; see entity transform receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using AttachAgent = Zjcs.Official.StoryAttachment.AttachAgent;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityTransformViews
{
public class ECTransformView : ECViewBase
{
	[SerializeField]
	private bool mSetZ;

	private Transform mTransform;

	public Vector3 Position
	{
		get
		{
			return mTransform.localPosition;
		}
		set
		{
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
			return mTransform.localRotation;
		}
		set
		{
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
			return mTransform.localScale;
		}
		set
		{
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
		mTransform = ((Component)((Component)mOwner.transform).GetComponentInParent<ECViewEntityBinding>(true)).transform;
		if (GetView<ECGridTransformView>() == null)
		{
			ECTransformData component = GetComponent<ECTransformData>();
			Position = component.Position;
			Rotation = component.Rotation;
			Scale = component.Scale;
		}
	}
}

}
