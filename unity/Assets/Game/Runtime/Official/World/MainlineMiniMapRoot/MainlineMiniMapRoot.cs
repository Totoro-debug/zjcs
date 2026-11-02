// Migrated selected original MiniMap root; see mainline-minimap-root source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using GAction = Zjcs.Official.StoryPlayables.GAction;
using NativeCameraUtils = Zjcs.Official.World.NativeCamera.CameraUtils;
using Object = UnityEngine.Object;

namespace Zjcs.Official.World.Map13MiniMap
{
public abstract class MainlineMiniMapRoot : MiniMapManagedContext, IMiniMapClickContext
{
	public GAction OnShowFullScreen = new GAction();

	public GAction OnHideFullScreen = new GAction();

	[SerializeField, FormerlySerializedAs("MapClassId")]
	private int mMapClassId;
	public override int MapClassId => mMapClassId;

	public MiniMapCharacterManager Character;

	public MiniMapNonMaskableManager NonMaskableManager;

	public MiniMapKeepSizeHandler KeepSizeHandler;

	public MiniMapResourcePointManager ResourcePointManager;

	[SerializeField]
	private MiniMapMarkClick mMarkClick;

	public MonoDictionary<GroundType, Tile> GroundTileDict;

	[Tooltip("Mini地图分界线父节点")]
	public Transform MiniMapJunctionLineSlot;

	[Tooltip("Mini地图分界线Prefab")]
	public SpriteRenderer MiniMapJunctionLinePrefab;

	public Transform RegionGroundSlot;

	public Material RegionGroundMaterial;

	[SerializeField, FormerlySerializedAs("IsFullScreen")]
	private bool mIsFullScreen;
	public override bool IsFullScreen => mIsFullScreen;

	private IFullScrMiniMapStateListener[] mFullScrMiniMapStateListeners;

	private MiniMapSettings.MiniMapSetting mMiniMapSetting;

	private MapCharacterViewRangeHandler mCharacterViewRangeHandler;

	private MapAllViewRangeHandler mAllViewRangeHandler;

	protected abstract MiniMapSettings Settings { get; }
	protected abstract int MinimapConfigId { get; }
	protected abstract int MapViewRangeExtraSize { get; }
	protected abstract MiniMapResourcePointContext ResourcePointContext { get; }

	public OfflineMiniMapMarkManager OfflineMiniMapMarkManager { get; private set; }

	public override MiniMapSettings.MiniMapSetting MiniMapSetting
	{
		get
		{
			if (mMiniMapSetting == null)
			{
				mMiniMapSetting = Settings.GetMinimapSetting(MinimapConfigId);
				if (mMiniMapSetting == null)
				{
					Debug.LogError("MiniMapSettings这个SO资源中缺少MinimapConfigId[" + MinimapConfigId + "]的配置");
				}
			}
			return mMiniMapSetting;
		}
	}

	protected abstract IMiniMapCharacterManager MapCharacters { get; }
	public override MiniMapKeepSizeHandler SizingHandler => KeepSizeHandler;
	public override GAction ShowFullScreenAction => OnShowFullScreen;
	public override GAction HideFullScreenAction => OnHideFullScreen;
	Object IMiniMapClickContext.MyGridVisibilityAgent => VisibilityAgent;

	private IMapViewRangeHandler mViewRangeHandler;
	public override IMapViewRangeHandler ViewRangeHandler => mViewRangeHandler;

	public event Action OnWantToCloseFullScreen;

	public override event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;

	private void Awake()
	{
		OfflineMiniMapMarkManager = ((Component)this).GetComponentInChildren<OfflineMiniMapMarkManager>();
		mFullScrMiniMapStateListeners = ((Component)this).GetComponentsInChildren<IFullScrMiniMapStateListener>();
		ResourcePointManager.Init(ResourcePointContext);
		InitViewRange();
	}

	private void OnDestroy()
	{
		ReleaseViewRange();
		mIsFullScreen = false;
		mFullScrMiniMapStateListeners = null;
	}

	public void OnShowFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		mIsFullScreen = true;
		ViewRange_OnShowFullScreenMiniMap(cameraController);
		if (MiniMapSetting != null)
		{
			cameraController.mySizeMin = MiniMapSetting.CameraControllerSetting.MinCameraOrthographicSize;
			cameraController.mySizeMax = MiniMapSetting.CameraControllerSetting.MaxCameraOrthographicSize;
		}
		cameraController.SetCameraOrthographicSize((cameraController.mySizeMin + cameraController.mySizeMax) / 2f);
		IFullScrMiniMapStateListener[] array = mFullScrMiniMapStateListeners;
		for (int i = 0; i < array.Length; i++)
		{
			array[i]?.OnShowFullScreenMiniMap(cameraController);
		}
		SetMiniMapCameraPos(cameraController);
		OnShowFullScreen.Invoke();
	}

	public void OnHideFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		ViewRange_OnHideFullScreenMiniMap(cameraController);
		IFullScrMiniMapStateListener[] array = mFullScrMiniMapStateListeners;
		for (int i = 0; i < array.Length; i++)
		{
			array[i]?.OnHideFullScreenMiniMap(cameraController);
		}
		mIsFullScreen = false;
		OnHideFullScreen.Invoke();
	}

	public void SetActive(bool isActive)
	{
		((Component)this).gameObject.SetActive(isActive);
	}

	public override void FireToCloseFullScreen()
	{
		OnWantToCloseFullScreen?.Invoke();
	}

	public MiniMapMarkUIBase GetMark(Pos2d pos)
	{
		return mMarkClick.GetMark(pos);
	}

	private void SetMiniMapCameraPos(MiniMapCameraController cameraController)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		Transform transform = ((Component)cameraController.MiniMapCamera).transform;
		if ((!((Object)(object)mMarkClick != (Object)null) || !mMarkClick.TrySetCameraPos(transform)) && (Object)(object)Character != (Object)null)
		{
			Character.TrySetCameraPos(transform);
		}
		transform.position = cameraController.myInput.Data.ClampPos(transform.position);
	}

	public override bool IsEntityInViewRange(IMiniMapSpawnEntity ent)
	{
		return ViewRangeHandler.IsEntityInViewRange(new EntityBody(ent.GridTransform));
	}

	public override bool IsRangeInViewRange(Pos2d minPos, Pos2d maxPos)
	{
		return ViewRangeHandler.IsRangeInViewRange(minPos, maxPos);
	}

	private void InitViewRange()
	{
		UpdateByCharacterViewRangeHandler();
	}

	private void ReleaseViewRange()
	{
		ViewRangeHandler.OnVisibleRangeUpdate -= new Action<IMapViewRangeHandler>(FireOnVisibleRangeUpdate);
		ViewRangeHandler.Dispose();
		mCharacterViewRangeHandler?.Dispose();
		mAllViewRangeHandler?.Dispose();
		mViewRangeHandler = null;
		mCharacterViewRangeHandler = null;
		mAllViewRangeHandler = null;
	}

	private void ViewRange_OnShowFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		UpdateByAllViewRangeHandler();
	}

	private void ViewRange_OnHideFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		UpdateByCharacterViewRangeHandler();
	}

	private void UpdateByCharacterViewRangeHandler()
	{
		if (mCharacterViewRangeHandler == null)
		{
			Pos2d pos2d = CalcClientViewRange(Character.UiCamera, MapViewRangeExtraSize);
			int num = Mathf.Max(pos2d.X, pos2d.Y);
			pos2d = new Pos2d(num, num);
			mCharacterViewRangeHandler = new MapCharacterViewRangeHandler(MapCharacters, pos2d);
		}
		if (ViewRangeHandler != null)
		{
			ViewRangeHandler.OnVisibleRangeUpdate -= new Action<IMapViewRangeHandler>(FireOnVisibleRangeUpdate);
		}
		mViewRangeHandler = mCharacterViewRangeHandler;
		ViewRangeHandler.OnVisibleRangeUpdate += new Action<IMapViewRangeHandler>(FireOnVisibleRangeUpdate);
		FireOnVisibleRangeUpdate(ViewRangeHandler);
	}

	private void UpdateByAllViewRangeHandler()
	{
		if (mAllViewRangeHandler == null)
		{
			mAllViewRangeHandler = new MapAllViewRangeHandler();
		}
		if (ViewRangeHandler != null)
		{
			ViewRangeHandler.OnVisibleRangeUpdate -= new Action<IMapViewRangeHandler>(FireOnVisibleRangeUpdate);
		}
		mViewRangeHandler = mAllViewRangeHandler;
		ViewRangeHandler.OnVisibleRangeUpdate += new Action<IMapViewRangeHandler>(FireOnVisibleRangeUpdate);
		FireOnVisibleRangeUpdate(ViewRangeHandler);
	}

	private void FireOnVisibleRangeUpdate(IMapViewRangeHandler handler)
	{
		OnVisibleRangeUpdate?.Invoke(handler);
	}


    // Original CameraUtils camera overload; the existing native camera module
    // supplies the unchanged orthographic bounds calculation.
    private static Pos2d CalcClientViewRange(Camera camera, int extraRange)
    {
        Bounds bounds = NativeCameraUtils.GetOrthographicCameraViewBounds(camera);
        Vector3 size = bounds.size;
        return new Pos2d((int)Math.Ceiling(size.x / 1f) + extraRange,
            (int)Math.Ceiling(size.y / 1f) + extraRange);
    }

    private sealed class EntityBody : IMiniMapEntityBody
    {
        private readonly IMiniMapMarkGrid grid;
        public EntityBody(IMiniMapMarkGrid grid) { this.grid = grid; }
        public Pos2d Pos => grid.Pos;
        public IEnumerable<MiniMapBodyOffset> BodyRangeInfos
        {
            get
            {
                foreach (var body in grid.Info.BodyRangeInfos)
                    yield return new MiniMapBodyOffset { Pos = body.Pos };
            }
        }
    }
}
}
