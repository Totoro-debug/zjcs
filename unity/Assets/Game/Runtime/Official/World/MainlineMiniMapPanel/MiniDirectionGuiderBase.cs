using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{

public class MiniDirectionGuiderBase : UIBase
{
	[SerializeField]
	protected Button mGuiderBtn;

	[SerializeField]
	protected Transform mGuideIcon;

	protected MiniMapCameraController mCameraController;

	protected MiniMapResourcePointManager mResourcePointManager;

	protected MiniMapFullScreenPanel mFullScreenPanel;

	protected Camera mCamera;

	protected IMiniMapCharacterController mapController;

	protected IMiniMapDirectionGround mMyGridGround;

	protected Transform mMiniMapTransform;

	protected RectTransform mRootRectTransform;

	protected Vector3 mPosition;

	protected bool mIsUpdateGuider;

	protected bool mIsMapPosInit;

	private bool mIsActive = true;

	public MiniMapDirectionContext Context { get; set; }


	protected virtual void Awake()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		((UnityEvent)mGuiderBtn.onClick).AddListener((UnityAction)HandleGuideBtnOnClick);
	}

	protected override void Start()
	{
		base.Start();
		Refresh();
		if (Context.HasMapRoot && Context.Characters.MyMeUiCharacter != null)
		{
			DoInit();
			return;
		}
		Context.Characters.OnMeMapControllerUpdated += new Action<IMiniMapCharacterController, IMiniMapCharacterController>(Instance_OnMapControllerInited);
		Context.OnInitialized += new Action(Instance_OnInited);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Context.Characters.OnMeMapControllerUpdated -= new Action<IMiniMapCharacterController, IMiniMapCharacterController>(Instance_OnMapControllerInited);
		Context.OnInitialized -= new Action(Instance_OnInited);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if ((Object)(object)mCameraController != (Object)null)
		{
			((UnityEvent<Vector2>)mCameraController.myInput.OnPosChanged).AddListener((UnityAction<Vector2>)DragController_OnFocusPosUpdated);
		}
		if ((Object)(object)mFullScreenPanel != (Object)null)
		{
			MiniMapFullScreenPanel miniMapFullScreenPanel = mFullScreenPanel;
			miniMapFullScreenPanel.OnMiniMapChange = (Action<int>)Delegate.Combine(miniMapFullScreenPanel.OnMiniMapChange, new Action<int>(OnMiniMapChange));
			OnMiniMapChange(mFullScreenPanel.CurrentMapClassId);
		}
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		if ((Object)(object)mCameraController != (Object)null)
		{
			((UnityEvent<Vector2>)mCameraController.myInput.OnPosChanged).RemoveListener((UnityAction<Vector2>)DragController_OnFocusPosUpdated);
		}
		if ((Object)(object)mFullScreenPanel != (Object)null)
		{
			MiniMapFullScreenPanel miniMapFullScreenPanel = mFullScreenPanel;
			miniMapFullScreenPanel.OnMiniMapChange = (Action<int>)Delegate.Remove(miniMapFullScreenPanel.OnMiniMapChange, new Action<int>(OnMiniMapChange));
		}
	}

	protected virtual void Update()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if (mIsUpdateGuider)
		{
			mIsUpdateGuider = false;
			UpdateDirectionGuide(mPosition, mIsMapPosInit);
		}
	}

	private void Instance_OnInited()
	{
		DoInit();
	}

	private void Instance_OnMapControllerInited(IMiniMapCharacterController preCtrl, IMiniMapCharacterController curCtrl)
	{
		DoInit();
	}

	private void DoInit()
	{
		if (Context.HasMapRoot && Context.Characters.MyMeUiCharacter != null && !_Inited)
		{
			InitWrapper(() =>
			{
				mapController = Context.Characters.MyMeUiCharacter;
				mMiniMapTransform = ((Component)Context.FullScreen.CurrentMiniMap).transform;
				mMyGridGround = Context.Ground;
				mResourcePointManager = Context.FullScreen.CurrentMiniMap.ResourcePointManager;
			});
		}
	}

	private void DragController_OnFocusPosUpdated(Vector2 vec)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (mIsMapPosInit)
		{
			UpdateDirectionGuide(mPosition, mIsMapPosInit);
		}
	}

	private void OnMiniMapChange(int mapClassId)
	{
		SetActive(Context.FullScreen.CurrentMapClassId == mapClassId);
	}

	private void Refresh()
	{
		SetGuiderBtnActive(value: false);
		mRootRectTransform = ((Component)((Component)this).GetComponentInParent<Zjcs.Official.WorldUI.WorldSafeArea>()).gameObject.GetComponent<RectTransform>();
		mCameraController = ((Component)this).GetComponentInParent<MiniMapCameraController>();
		mFullScreenPanel = ((Component)this).GetComponentInParent<MiniMapFullScreenPanel>();
		mCamera = mCameraController.MiniMapCamera;
	}

	protected void UpdateDirectionGuide(Vector3 position, bool isPosInit = true)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (!isPosInit || IsInView(position))
		{
			SetGuiderBtnActive(value: false);
			mIsUpdateGuider = false;
		}
		else
		{
			SetGuiderBtnActive(value: true);
			SetPositionAndAngles(position);
			mIsUpdateGuider = true;
		}
	}

	private bool IsInView(Vector3 worldPos)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = (Vector2)mCamera.WorldToViewportPoint(worldPos);
		if (val.x >= 0f && val.x <= 1f && val.y >= 0f && val.y <= 1f)
		{
			return true;
		}
		return false;
	}

	private void SetPositionAndAngles(Vector3 position)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = (Vector2)mCamera.WorldToViewportPoint(((Component)mCamera).transform.position);
		Vector2 val2 = (Vector2)mCamera.WorldToViewportPoint(position);
		float num = (float)(Math.Atan2(val.y - val2.y, val.x - val2.x) * 180.0 / Math.PI);
		Vector2 guiderPosition = DirectionGuiderUtils.GetGuiderPosition(val, val2, num);
		Vector2 val3 = new Vector2(Mathf.Clamp(guiderPosition.x, 0f, 1f), Mathf.Clamp(guiderPosition.y, 0f, 1f));
		float num2 = ((Component)mGuiderBtn).GetComponent<RectTransform>().sizeDelta.x * 0.75f;
		float num3 = ((Component)mGuiderBtn).GetComponent<RectTransform>().sizeDelta.y * 0.75f;
		Rect rect = mRootRectTransform.rect;
		float width = rect.width;
		rect = mRootRectTransform.rect;
		float height = rect.height;
		float num4 = Mathf.Clamp(val3.x * width, num2, width - num2);
		float num5 = Mathf.Clamp(val3.y * height, num3, height - num3);
		((Component)mGuiderBtn).transform.localPosition = new Vector3(num4, num5, 0f);
		mGuideIcon.localEulerAngles = new Vector3(0f, 0f, num + 90f);
	}

	private void HandleGuideBtnOnClick()
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		Transform transform = ((Component)mCameraController.MiniMapCamera).transform;
		transform.position = new Vector3(mPosition.x, mPosition.y, transform.position.z);
	}

	protected void SetGuiderBtnActive(bool value)
	{
		((Component)mGuiderBtn).gameObject.SetActive(mIsActive & value);
	}

	protected void SetActive(bool value)
	{
		mIsActive = value;
		mIsUpdateGuider = true;
	}
}
}
