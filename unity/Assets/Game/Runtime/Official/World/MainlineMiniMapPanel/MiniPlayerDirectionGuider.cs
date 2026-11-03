using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{

public class MiniPlayerDirectionGuider : MiniDirectionGuiderBase, Zjcs.Official.EntityTransformViews.IGridTransformPosUpdatedEventReceiver, Zjcs.Official.EntityViews.IEventReceiver
{
	private Zjcs.Official.EntityTransformViews.ECGridTransformData mGridTransformComponent;

	protected override void OnEnableAndInited()
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		base.OnEnableAndInited();
		mGridTransformComponent = mapController.MyGridTransform.Component;
		mGridTransformComponent.AddPosUpdatedReceiver(this);
		SetPosition(Context.GetWorldPosition(mapController));
		mIsMapPosInit = true;
	}

	protected override void OnDisableAndInited()
	{
		if (mGridTransformComponent != null)
		{
			mGridTransformComponent.RemovePosUpdatedReceiver(this);
		}
		base.OnDisableAndInited();
	}

	protected override void Update()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (mIsUpdateGuider)
		{
			SetPosition(Context.GetWorldPosition(mapController));
			base.Update();
		}
	}

	void Zjcs.Official.EntityTransformViews.IGridTransformPosUpdatedEventReceiver.GridTransform_OnPosUpdated(Zjcs.Official.EntityTransformViews.ECGridTransformData transform, Pos2d prePos)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		SetPosition(Context.GetWorldPosition(mapController));
		UpdateDirectionGuide(mPosition);
	}

	private void SetPosition(Vector3 pos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		mPosition = pos + mMiniMapTransform.position;
	}
}
}
