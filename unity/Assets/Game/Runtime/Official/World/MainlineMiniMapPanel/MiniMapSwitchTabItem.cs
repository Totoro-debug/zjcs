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

public class MiniMapSwitchTabItem : MonoBehaviour
{
	public Image MapImage;

	public Button Button;

	public StateSwitcher<bool> SelectedSwitcher;

	public MultiLanguageText MapNameText;

	private int mMapClassId;

	private event Action<int> mOnClick;

	private void Awake()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		((UnityEvent)Button.onClick).AddListener((UnityAction)OnClick);
	}

	private void OnDestroy()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		((UnityEvent)Button.onClick).RemoveListener((UnityAction)OnClick);
	}

	public void Init(int mapClassId, int currentMapClassId, Sprite sprite, Action<int> onClick)
	{
		mMapClassId = mapClassId;
		MapImage.sprite = sprite;
		mOnClick = onClick;
		SetTitle(mMapClassId);
		OnMiniMapChange(currentMapClassId);
	}

	private void OnClick()
	{
		if (mMapClassId >= 0)
		{
			mOnClick?.Invoke(mMapClassId);
		}
	}

	public void OnMiniMapChange(int mapClassId)
	{
		bool state = mMapClassId == mapClassId;
		SelectedSwitcher.SetState(state);
	}

	private void SetTitle(int mapClassId)
	{
		MapNameText.SetKey(MiniMapPanelLanguage.GetMapNameKey(mapClassId));
	}
}
}
