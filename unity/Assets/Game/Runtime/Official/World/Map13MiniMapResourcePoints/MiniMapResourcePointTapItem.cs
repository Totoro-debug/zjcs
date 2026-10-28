using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
[ExecuteInEditMode]
public class MiniMapResourcePointTapItem : MonoBehaviour
{
	[SerializeField]
	private SpriteRenderer mSprite;

	private IMiniMapResourcePointBoard mPointBoard;

	private string mIconPath;

	private MiniMapResourcePointDisposableBag mMiniMapResourcePointDisposableBag;

	private MiniMapResourcePointContext ResourceLoader => ((MiniMapResourcePointBoard)mPointBoard).Context;

	public void Init(IMiniMapResourcePointBoard pointBoard)
	{
		mPointBoard = pointBoard;
	}

	private void OnEnable()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		mMiniMapResourcePointDisposableBag = default;
		string iconPath = mPointBoard.GetIconPath();
		if (!(iconPath == mIconPath))
		{
			mIconPath = iconPath;
			mSprite.sprite = MiniMapResourcePointDisposable.AddTo<IResourcePointAssetLease<Sprite>>(ResourceLoader.LoadSprite(mIconPath), ref mMiniMapResourcePointDisposableBag).Result;
		}
	}

	private void OnDisable()
	{
		mMiniMapResourcePointDisposableBag.Dispose();
	}
}

}
