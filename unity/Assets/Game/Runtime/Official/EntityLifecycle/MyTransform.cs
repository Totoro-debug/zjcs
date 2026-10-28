// Complete original client lifecycle class; see entity lifecycle source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityLifecycle
{
public class MyTransform : MyComponent<ECTransformData>
{
	[SerializeField]
	private bool mSetZ;

	public Vector3 Position
	{
		get
		{

			return ((Component)this).transform.localPosition;
		}
		set
		{





			Vector3 val = value;
			if (mSetZ)
			{
				val.z = ZSetUtils.XYToZ(val.x, val.y);
			}
			((Component)this).transform.localPosition = val;
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

			return ((Component)this).transform.localRotation;
		}
		set
		{

			((Component)this).transform.localRotation = value;
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

			return ((Component)this).transform.localScale;
		}
		set
		{

			((Component)this).transform.localScale = value;
			if (OnScaleUpdated != null)
			{
				OnScaleUpdated(this);
			}
		}
	}

	public event Action<MyTransform> OnPosUpdated;

	public event Action<MyTransform> OnRotUpdated;

	public event Action<MyTransform> OnScaleUpdated;

	public override void OnStart(bool newEntity)
	{



		base.OnStart(newEntity);
		if ((Object)(object)myGridTransform == (Object)null)
		{
			Position = Component.Position;
			Rotation = Component.Rotation;
			Scale = Component.Scale;
		}
	}

	public void SetZ(bool isSetZ)
	{
		mSetZ = isSetZ;
	}
}

}
