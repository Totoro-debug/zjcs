// Complete original visible-range implementation; see Map13 minimap view source receipt.
using System;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.EntityLifecycle;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
public class MapCharacterViewRangeHandler : MapViewRangeHandlerBase
{
	private MyGridTransform mMeGridTrans;

	private IMiniMapCharacterManager mMapMyCharacterMgr;

	private Pos2d mViewRange;

	public override GAction<IMapViewRangeHandler> OnVisibleRangeUpdate { get; set; } = new GAction<IMapViewRangeHandler>();

	protected override Pos2d ViewRange => mViewRange;

	public MapCharacterViewRangeHandler(IMiniMapCharacterManager mapMyCharacterMgr, Pos2d characterViewRange)
	{
		mMapMyCharacterMgr = mapMyCharacterMgr;
		mViewRange = new Pos2d(Mathf.CeilToInt((float)characterViewRange.X / 2f), Mathf.CeilToInt((float)characterViewRange.Y / 2f));
		mMapMyCharacterMgr.OnMeMapControllerUpdated += new Action<IMiniMapCharacterController, IMiniMapCharacterController>(Instance_OnMeCharacterUpdated);
		Instance_OnMeCharacterUpdated(null, mMapMyCharacterMgr.MyMeUiCharacter);
	}

	public override void Dispose()
	{
		mMapMyCharacterMgr.OnMeMapControllerUpdated -= new Action<IMiniMapCharacterController, IMiniMapCharacterController>(Instance_OnMeCharacterUpdated);
		if ((Object)(object)mMeGridTrans != (Object)null)
		{
			mMeGridTrans.OnPosUpdated -= OnMePosUpdate;
			mMeGridTrans = null;
		}
	}

	private void Instance_OnMeCharacterUpdated(IMiniMapCharacterController preCtrl, IMiniMapCharacterController curCtrl)
	{
		mMeGridTrans = null;
		if (preCtrl != null)
		{
			preCtrl.MyGridTransform.OnPosUpdated -= OnMePosUpdate;
		}
		if (curCtrl != null)
		{
			mMeGridTrans = curCtrl.MyGridTransform;
			mMeGridTrans.OnPosUpdated += OnMePosUpdate;
			OnMePosUpdate(mMeGridTrans, mMeGridTrans.Pos);
		}
	}

	private void OnMePosUpdate(MyGridTransform gridTrans, Pos2d prePos)
	{
		if (!(mCurGridPos == gridTrans.Pos))
		{
			mCurGridPos = gridTrans.Pos;
			CalcVisiablePosList(mCurGridPos, mVisiablePosList);
			OnVisibleRangeUpdate?.Invoke(this);
		}
	}
}

}
