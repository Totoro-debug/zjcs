// Selected original HUD path; see mainline-minimap-hud-code-source-manifest.json.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeLabelUI;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
namespace Zjcs.Official.World.Map13MiniMap
{
public class PublicMapBranchLineBtn : MapUIBase
{
	[SerializeField]
	private GameObject mContent;

	[SerializeField]
	private MultiLanguageText mChannelName;

	[SerializeField]
	private Image mSwitchImage;

	[SerializeField]
	private List<MaskableGraphic> mGraphics;

	[SerializeField]
	private Button mButton;

	[Tooltip("当公有地图频道数为0时是否隐藏该按钮")]
	[SerializeField]
	private bool mIsHidenButtonWhenLineCountIsZero;

	[SerializeField]
	private StateSwitcher<PublicMapLineStatus> mStateSwitcher;

	private MiniMapHudMapPartInfo mMapInfo;

	public MiniMapHudContext Context { get; set; }

	protected void Awake()
	{
		mMapInfo = Context.GetMapPartInfo(Context.CurrentMapClassId);
	}

	protected override void Start()
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected Obj, but got Unknown
		base.Start();
		if (mMapInfo == null)
		{
			return;
		}
		if (!mMapInfo.SupportPublicRegion)
		{
			((Component)this).gameObject.SetActive(false);
			return;
		}
		bool flag = IsShowBtn();
		mContent.SetActive(flag);
		if (!flag)
		{
			Context.OnPlayerMapStatusUpdate += OnVisibilitiChange;
		}
		((UnityEvent)mButton.onClick).AddListener((UnityAction)OnClick);
	}

	protected override void OnDestroy()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected Obj, but got Unknown
		base.OnDestroy();
		if (Context.HasMainPlayer)
		{
			Context.OnPlayerMapStatusUpdate -= OnVisibilitiChange;
		}
		((UnityEvent)mButton.onClick).RemoveListener((UnityAction)OnClick);
	}

	private void OnVisibilitiChange(int mapClassId, int region, MapRegionStatus preVisibility, MapRegionStatus visibility)
	{
		if (IsShowBtn())
		{
			mContent.SetActive(true);
			if (Context.HasMainPlayer)
			{
				Context.OnPlayerMapStatusUpdate -= OnVisibilitiChange;
			}
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mContent.SetActive(IsShowBtn());
		RefreshStatus(PublicMapLineStatus.None);
		Context.OnPublicMapLineStatusChanged += new Action<PublicMapLineStatus>(RefreshStatus);
		Context.OnGroupMapIdOrderListChanged += new Action<List<long>>(OnGroupMapIdOrderListUpdate);
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		Context.OnPublicMapLineStatusChanged -= new Action<PublicMapLineStatus>(RefreshStatus);
		Context.OnGroupMapIdOrderListChanged -= new Action<List<long>>(OnGroupMapIdOrderListUpdate);
	}

	private void OnClick()
	{
		foreach (KeyValuePair<long, MiniMapHudFightData> currentGame in Context.CurrentGames)
		{
			if (Context.TryGetPublicContainerId(out var containerId) && currentGame.Value.PlayerMapId == containerId)
			{
				Context.ShowError(4131);
				return;
			}
		}
		Context.PopupPublicMapBranchLineDialog();
	}

	private void RefreshStatus(PublicMapLineStatus status)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		(Color, string) channelUiInfo = MiniMapHudChannelStyle.GetChannelUiInfo(Context.ChannelColors, Context.PublicMapLineStatus);
		mChannelName.Format(Context.GetMapLineIndex());
		foreach (MaskableGraphic mGraphic in mGraphics)
		{
			((Graphic)mGraphic).color = channelUiInfo.Item1;
		}
		mStateSwitcher.SetState(Context.PublicMapLineStatus);
	}

	private void OnGroupMapIdOrderListUpdate(List<long> groupMapIdOrderList)
	{
		mChannelName.Format(Context.GetMapLineIndex());
		mContent.SetActive(IsShowBtn());
	}

	private bool IsShowBtn()
	{
		if (mMapInfo == null)
		{
			return false;
		}
		if (!Context.HasMainPlayer)
		{
			return false;
		}
		if (!Context.MapRegionStatusDict.TryGetValue(mMapInfo.ClassId, out var value))
		{
			return false;
		}
		if (value == null || value.Count <= 0)
		{
			return false;
		}
		if (!value.Contains(MapRegionStatus.Public))
		{
			return false;
		}
		if (mIsHidenButtonWhenLineCountIsZero && Context.GroupMapIdOrderList.Count <= 1)
		{
			return false;
		}
		return true;
	}
}
}
