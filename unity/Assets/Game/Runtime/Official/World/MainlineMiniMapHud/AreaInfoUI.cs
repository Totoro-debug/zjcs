// Selected original HUD path; see mainline-minimap-hud-code-source-manifest.json.
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using IGridTransformPosUpdatedEventReceiver = Zjcs.Official.EntityTransformViews.IGridTransformPosUpdatedEventReceiver;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
namespace Zjcs.Official.World.Map13MiniMap
{
public class AreaInfoUI : MapMeCharacterUIBase, IGridTransformPosUpdatedEventReceiver, IEventReceiver
{
	public MultiLanguageText MyRegionInfoText;

	public MultiLanguageText MyRegionProgress;

	public Sprite MyRegionChangeNotifyIcon;

	private ECGridTransformData mGridTransformComponent;

	private int mRegion = -1;

	private MiniMapSpawnDisposableBag mDisposableBag;

	private MiniMapHudLanguageKey mRegionTextArg;

	public MiniMapHudContext Context { get; set; }


	protected override void OnEnableAndInited()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		mDisposableBag = default;
		mGridTransformComponent = myMapController.MyGridTransform.Component;
		mGridTransformComponent.AddPosUpdatedReceiver(this);
		UpdateContent(myMapController.MyGridTransform.Pos);
		Context.Root.LabelServices.Player.OnWorldMapProgressUpdate += MyMap_OnProgressUpdate;
		RefreshProgress();
	}

	protected override void OnDisableAndInited()
	{
		mDisposableBag.Dispose();
		if (mGridTransformComponent != null)
		{
			mGridTransformComponent.RemovePosUpdatedReceiver(this);
		}
		if (Context.Root.LabelServices.Player != null)
		{
			Context.Root.LabelServices.Player.OnWorldMapProgressUpdate -= MyMap_OnProgressUpdate;
		}
	}

	void IGridTransformPosUpdatedEventReceiver.GridTransform_OnPosUpdated(ECGridTransformData transform, Pos2d prePos)
	{
		UpdateContent(transform.Pos);
	}

	private void MyMap_OnProgressUpdate(int mapClassId, List<int> regionProgresses)
	{
		if (mapClassId == Context.CurrentMapClassId)
		{
			RefreshProgress();
		}
	}

	private void UpdateContent(Pos2d pos)
	{
		MiniMapHudLanguageKey textKeyArgWrap = new MiniMapHudLanguageKey("map_pos_info", pos.X, pos.Y);
		IMiniMapManagedAreaAgent areaAgent = Context.Root.ManagedAreaAgent;
		int classId = Context.Root.MapClassId;
		int region = areaAgent.GridToRegion(classId, pos);
		if (region == mRegion)
		{
			FormatRegionInfo(textKeyArgWrap);
			return;
		}
		mRegion = region;
		RefreshProgress();
		mRegionTextArg = new MiniMapHudLanguageKey(MiniMapHudLanguage.GetRegionNameKey(classId, region));
		FormatRegionInfo(textKeyArgWrap);
		string text = Context.GetMapGoddessHead(classId);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		var spriteHandle = Context.Root.LoadSprite(text);
		mDisposableBag.Add(spriteHandle);
		Sprite sprite = spriteHandle.Result;
		if (!((Object)(object)sprite == (Object)null))
		{
			Context.PopupMapNotify((IMiniMapHudNotify go) =>
			{
				string regionNameKey = MiniMapHudLanguage.GetRegionNameKey(Context.Root.MapClassId, region);
				go.AddData(sprite, regionNameKey);
			});
		}
	}

	private void RefreshProgress()
	{
		int currentMapClassId = Context.CurrentMapClassId;
		List<int> worldMapProgresses = Context.Root.GetWorldMapProgresses(currentMapClassId);
		if (worldMapProgresses != null && mRegion >= 0 && mRegion < worldMapProgresses.Count)
		{
			int progress = worldMapProgresses[mRegion];
			string text = (Context.Root.LabelServices.Data.CalcWorldMapRegionProgress(currentMapClassId, mRegion, progress) / 100).ToString();
			MyRegionProgress.Format(text);
		}
	}

    // Supply the local language service without introducing a global singleton.
    private void FormatRegionInfo(MiniMapHudLanguageKey position)
    {
        mRegionTextArg.Formatter = Context.FormatText;
        position.Formatter = Context.FormatText;
        MyRegionInfoText.Format(mRegionTextArg, position);
    }
}
}
