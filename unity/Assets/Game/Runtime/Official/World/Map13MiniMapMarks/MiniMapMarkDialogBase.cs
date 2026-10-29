using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public abstract class MiniMapMarkDialogBase : MonoBehaviour, IWindowOperator
{
	public MultiLanguageText myTitle;

	public MapDialogIconPreviewSlot myIconSlot;

	public MultiLanguageText myMarkDesc;

	[SerializeField]
	private Image mBgIcon;

	protected MiniMapMarkUIBase miniMapMark;

	private MiniMapMarkDisposableBag mDisposableBag;

	public GameObject Operator => ((Component)this).gameObject;

	private MiniMapMarkContext ResourceLoader => miniMapMark.MiniMap;

	protected virtual void Awake()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		mDisposableBag = default;
	}

	protected virtual void OnDestroy()
	{
		mDisposableBag.Dispose();
	}

	public virtual void Init(MiniMapMarkUIBase mark)
	{
		miniMapMark = mark;
		if ((Object)(object)myTitle != (Object)null)
		{
			myTitle.SetKey(miniMapMark.Mark.GetMarkNameKey());
		}
		if (myIconSlot != null)
		{
			RefreshIconSlot();
		}
		if ((Object)(object)myMarkDesc != (Object)null)
		{
			MiniMapMarkTextKeyArg markDescTextArgKey = miniMapMark.Mark.GetMarkDescTextArgKey();
			myMarkDesc.SetKey(markDescTextArgKey.Key, markDescTextArgKey.Args);
		}
		if ((Object)(object)mBgIcon != (Object)null)
		{
			RefreshBg();
		}
	}

	protected void RefreshIconSlot()
	{
		string dialogIconPrefabPath = miniMapMark.Mark.GetDialogIconPrefabPath();
		if (!string.IsNullOrEmpty(dialogIconPrefabPath))
		{
			GameObject result = MiniMapMarkDisposable.AddTo<IMiniMapDescriptionAssetLease<GameObject>>(ResourceLoader.LoadPrefab(dialogIconPrefabPath), ref mDisposableBag).Result;
			MiniMapMarkUIUtils.DestroyChildren(myIconSlot.PreviewSlot);
			myIconSlot.MiniMap = miniMapMark.MiniMap;
			IMapEntityDialogIconHandler[] components = myIconSlot.CreateMapEntityIcon(result).GetComponents<IMapEntityDialogIconHandler>();
			miniMapMark.Mark.InitDialogIconHandler(components);
		}
	}

	private void RefreshBg()
	{
		int mapClassId = miniMapMark.MiniMap.MapClassId;
		string areaTag = miniMapMark.MiniMap.GetAreaTag(mapClassId, miniMapMark.GridPos);
		MiniMapMarkDisplayInfo mapUIDisplayInfo = miniMapMark.MiniMap.GetMapUIDisplay(mapClassId, areaTag);
		if (mapUIDisplayInfo != null)
		{
			mBgIcon.sprite = MiniMapMarkDisposable.AddTo<IMiniMapDescriptionAssetLease<Sprite>>(ResourceLoader.LoadSprite(mapUIDisplayInfo.MapBgPath), ref mDisposableBag).Result;
		}
	}

	public virtual void Close()
	{
		miniMapMark.MiniMap.CloseWindow(((Component)this).gameObject);
	}
}

}
