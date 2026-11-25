// Selected 903_156948 client controllers; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.Map13MiniMap;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using HangUpInfo = Zjcs.Official.HomeCartWindowsUI.HomeCartHangUpInfo;
using Player = Zjcs.Official.HomeCartWindowsUI.IHomeCartWindowPlayer;
using ItemAwardIcon = Zjcs.Official.HomeAwardUI.HomeAwardIcon;
using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;
using ItemType = Zjcs.Official.HomeAwardUI.ItemType;
using AwardData = Zjcs.Official.HomeAwardUI.AwardData;
namespace Zjcs.Official.HomeCartWindowsUI
{
public class HangUpCartItemDetailDialog : MonoBehaviour
{
    public IHomeCartWindowServices Services { protected get; set; }
	[SerializeField]
	private FurnitureOutputInfo mFurnitureOutputInfo;

	private int mHangUpId;

	private Canvas mCanvas;

	private CanvasScaler mCanvasScaler;
    private IDisposable mPointerHook;
    private int mOpenFrame;

	private void OnEnable()
	{
		mOpenFrame = Time.frameCount;
		mPointerHook = Services?.SubscribeGlobalPointerDown(OnGameObjectPointerDown);
	}

	private void OnDisable()
	{
		mPointerHook?.Dispose();
		mPointerHook = null;
	}

	public void Init(int hangUpId, Vector3 mousePosition)
	{
		mFurnitureOutputInfo.ShowInfo(hangUpId, Services.Player.GetHangUpLevel(hangUpId));
		mHangUpId = hangUpId;
		SetPos(mousePosition);
	}

	private void SetPos(Vector3 mousePosition)
	{
		if ((Object)mCanvas == (Object)null)
		{
			mCanvas = this.GetComponentInParent<Canvas>();
			mCanvasScaler = this.GetComponentInParent<CanvasScaler>();
		}
		RectTransform val = (RectTransform)mFurnitureOutputInfo.transform;
		Transform transform = this.transform;
		Vector2 pos = default;
		Camera uiCam = mCanvas != null && mCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? (mCanvas.worldCamera ?? Services.GetUiCamera()) : null;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, (Vector2)mousePosition, uiCam, out pos);
		val.anchoredPosition = HomeCartRectUtils.ClampRectPosInScreenByCorner(screenSize: HomeCartRectUtils.GetRealCanvasSize(mCanvas, mCanvasScaler, out var _), rectTransform: val, pos: pos);
	}

	private void OnGameObjectPointerDown(Vector2 screenPos)
	{
		if (Time.frameCount == mOpenFrame) return;
		Camera uiCam = mCanvas != null && mCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? (mCanvas.worldCamera ?? Services.GetUiCamera()) : null;
		if (!HomeCartRectUtils.CheckIfClickInsideUI((RectTransform)mFurnitureOutputInfo.transform, screenPos, uiCam))
		{
			Services.Close(this.gameObject);
		}
	}
}
}
