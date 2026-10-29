using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using MapUIBase=Zjcs.Official.World.NativeMap13Labels.MapUIBase;
using Zjcs.Official.World.NativeTerrain.Common;
[RequireComponent(typeof(MiniMapMarkManager))]
public class MiniMapMarkClick : MapUIBase, IFullScrMiniMapStateListener
{
	public struct OnClickEventData : System.IEquatable<OnClickEventData>
    {
        public Pos2d TargetPos { get; set; }
        public OnClickEventData(Pos2d targetPos){TargetPos=targetPos;}
        public void Deconstruct(out Pos2d targetPos){targetPos=TargetPos;}
        public bool Equals(OnClickEventData other)=>System.Collections.Generic.EqualityComparer<Pos2d>.Default.Equals(TargetPos,other.TargetPos);
        public override bool Equals(object value)=>value is OnClickEventData other&&Equals(other);
        public override int GetHashCode()=>System.Collections.Generic.EqualityComparer<Pos2d>.Default.GetHashCode(TargetPos);
        public static bool operator ==(OnClickEventData left,OnClickEventData right)=>left.Equals(right);
        public static bool operator !=(OnClickEventData left,OnClickEventData right)=>!left.Equals(right);
        public override string ToString()=>"OnClickEventData { TargetPos = "+TargetPos.ToString()+" }";
    }

	public MiniMapManagedContext MiniMap;

	[SerializeField]
	private SpriteRenderer mCameraRectFrameSprite;

	private IMiniMapMark[] MiniMapMarkUIManagers;

	private UITouchInput mTouchInput;

	private MiniMapCameraController mCameraController;

	private int mMinimapLayer;

	private MiniMapManagedContext MapRootManagerProxy => MiniMap;

	public IMiniMapClickInput mMapInput { get; set; }

	public IMiniMapClickCameraControl CameraControlManager { get; set; }

	public IMiniMapClickPublisher OnClickEventPublisher { get; set; }

	private void Awake()
	{
		MiniMapMarkUIManagers = ((Component)this).GetComponents<IMiniMapMark>();
		mMinimapLayer = 1 << LayerMask.NameToLayer("MiniMap");
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		((Component)mCameraRectFrameSprite).gameObject.SetActive(MapRootManagerProxy.CurrentMapClassId == MiniMap.MapClassId);
	}

	public void OnShowFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		mCameraController = cameraController;
		mTouchInput = mCameraController.myInput;
		if ((Object)(object)mTouchInput != (Object)null)
		{
			mTouchInput.OnClick += TouchInput_OnClick;
		}
		if ((Object)(object)Camera.main != (Object)null && ((Component)mCameraRectFrameSprite).gameObject.activeSelf)
		{
			float num = Camera.main.orthographicSize * 2f;
			mCameraRectFrameSprite.size = new Vector2(num * Camera.main.aspect, num);
			TrySetCameraSpriteTo(((Component)Camera.main).transform.position, setMainCameraPos: false);
		}
	}

	public void OnHideFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		if ((Object)(object)mTouchInput != (Object)null)
		{
			mTouchInput.OnClick -= TouchInput_OnClick;
		}
		mTouchInput = null;
	}

	public bool TrySetCameraPos(Transform cameraTsf)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		if (!((Behaviour)this).enabled || (Object)(object)mCameraRectFrameSprite == (Object)null || !((Component)mCameraRectFrameSprite).gameObject.activeSelf)
		{
			return false;
		}
		Vector3 position = ((Component)mCameraRectFrameSprite).transform.position;
		cameraTsf.position = new Vector3(position.x, position.y, cameraTsf.position.z);
		return true;
	}

	public MiniMapMarkUIBase GetMark(Pos2d pos)
	{
		MiniMapMarkUIBase data = null;
		IMiniMapMark[] miniMapMarkUIManagers = MiniMapMarkUIManagers;
		foreach (IMiniMapMark miniMapMark in miniMapMarkUIManagers)
		{
			if (miniMapMark != null && miniMapMark.TryGetMark(pos, out data))
			{
				return data;
			}
		}
		return data;
	}

	private void TouchInput_OnClick(Vector3 screenPos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = ScreenPosToMapWorldClickPos(screenPos);
		Pos2d pos2d = MiniMapClickRules.MapPosToGrid(MiniMap, MiniMap.MapClassId, val);
		OnClickEventPublisher.Publish(new OnClickEventData(pos2d));
		MiniMapMarkUIBase mark = GetMark(pos2d);
		if ((Object)(object)mark == (Object)null || !mark.CanInteractive)
		{
			TrySetCameraSpriteTo(val);
			return;
		}
		int num = mMapInput.CanTeamClickPosCheck(MiniMap.MapClassId, pos2d);
		if (num != 0)
		{
			MiniMap.ShowError(num);
		}
		else
		{
			mark.OpenDialog();
		}
	}

	private Vector3 ScreenPosToMapWorldClickPos(Vector3 screenPos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		return TryScreenToWorldWithRaycast(screenPos) - ((Component)this).transform.position;
	}

	private Vector3 TryScreenToWorldWithRaycast(Vector3 screenPos)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		RaycastHit val = default;
		if (Physics.Raycast(mCameraController.MiniMapCamera.ScreenPointToRay(screenPos), out val, float.MaxValue, mMinimapLayer))
		{
			return ((Component)val.collider).transform.position;
		}
		return mCameraController.MiniMapCamera.ViewportToWorldPoint(Camera.main.ScreenToViewportPoint(screenPos));
	}

	private void TrySetCameraSpriteTo(Vector3 localPosition, bool setMainCameraPos = true)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		if (((Component)mCameraRectFrameSprite).gameObject.activeSelf && !((Object)(object)((IMiniMapClickContext)MiniMap).MyGridVisibilityAgent == (Object)null))
		{
			((Component)mCameraRectFrameSprite).transform.localPosition = new Vector3(localPosition.x, localPosition.y, ((Component)mCameraRectFrameSprite).transform.localPosition.z);
			if (setMainCameraPos)
			{
				CameraControlManager.ControlBrain.SetViewPos(localPosition);
			}
		}
	}
}

}
