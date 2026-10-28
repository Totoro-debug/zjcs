namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class AppearanceUI : AppearanceSpine, IAppearanceAnimComponent
{
	private const float kHugeSize = 10000f;

	[SerializeField]
	private Component mSkeletonGraphic;

	[SerializeField]
	private YooAssetTextureLoader mTextureLoader;

	private RectTransform mSkeletonGraphicRect;

	public Action<Material[]> ReplaceMaterialsAfterLoad;

	private readonly Dictionary<Texture, Material> mIndependentMaterialDict = new Dictionary<Texture, Material>();

	private Dictionary<string, Dictionary<int, HsvChange>> mDyeCacheDict;

	private bool mSkinApplyDirty;

	private bool mDyeDirty;

	private bool mInitComponent;

	private bool mHasAddMeshCheckCallback;

	private StringBuilder mErrorStringBuilder;

    private IProfessionAppearanceUIBindings mUIBindings;
    private IProfessionAppearanceUIBindings UIBindings => mUIBindings ?? ProfessionAppearanceUIBindings.Required;
    private IProfessionAppearanceUIAssetManager AppearanceAssetManager => UIBindings.AssetManager;
    private IProfessionAppearanceUIGraphic GraphicGraph => (IProfessionAppearanceUIGraphic)mSkeletonGraphic;
    public void ConfigureUI(IProfessionAppearanceUIBindings bindings) { mUIBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

	public Component PartHandler => mPartHandler;

	private RectTransform SkeletonGraphicRect => mSkeletonGraphicRect ?? (mSkeletonGraphicRect = ((Component)mSkeletonGraphic).GetComponent<RectTransform>());

	public IProfessionAppearanceAnimationState AnimState => GraphicGraph.AnimationState;

	public override YooAssetTextureLoader TextureLoader => mTextureLoader;

	public IReadOnlyDictionary<Texture, Material> IndependentMaterialDict => mIndependentMaterialDict;

	private StringBuilder ErrorStringBuilder => mErrorStringBuilder ?? (mErrorStringBuilder = new StringBuilder());

	protected override void Awake()
	{

		SkeletonGraphicRect.sizeDelta = Vector2.zero;
	}

	private void OnCheckMultipleMeshesValid(int meshCount, Mesh[] meshes, Material[] _, Texture[] __)
	{
		if (meshCount == 0 || GraphicGraph.canvasRenderers == null)
		{
			return;
		}
		int count = GraphicGraph.canvasRenderers.Count;
		try
		{
			for (int i = 0; i < meshCount; i++)
			{
				Mesh val = meshes[i];
				if (!((Object)(object)val == (Object)null))
				{
					CanvasRenderer val2 = null;
					if (i < count)
					{
						val2 = GraphicGraph.canvasRenderers[i];
					}
					if ((Object)(object)val2 != (Object)null)
					{
						ClearIfMeshInvalid(val, val2);
					}
				}
			}
		}
		catch (Exception ex)
		{
            Debug.LogError("AppearanceUI OnCheckMultipleMeshesValid 检查角色Mesh时发生异常，Exception:" + ex.Message + "\n" + ex.StackTrace);
		}
	}

	private void ClearIfMeshInvalid(Mesh mesh, CanvasRenderer canvasRenderer)
	{
		if ((Object)(object)mesh == (Object)null || (Object)(object)canvasRenderer == (Object)null || CheckMeshValid(mesh))
		{
			return;
		}
		canvasRenderer.SetMesh((Mesh)null);
		ErrorStringBuilder.Append("Skins: [ ");
		if (Skins != null)
		{
			foreach (string skin in Skins)
			{
				ErrorStringBuilder.Append(skin);
				ErrorStringBuilder.Append(", ");
			}
		}
		ErrorStringBuilder.Append("] CurAnimationName:[ ");
		if (AnimState != null)
		{
			IProfessionAppearanceTrack current2 = AnimState.GetCurrent(0);
			if (current2 != null && current2.Animation != null)
			{
				ErrorStringBuilder.Append(current2.Animation.Name);
			}
		}
		ErrorStringBuilder.Append(" ]");
        Debug.LogError("AppearanceUI ClearIfMeshInvalid 网格已经清空，角色动画上下文信息：" + ErrorStringBuilder);
		ErrorStringBuilder.Clear();
	}

	private bool CheckMeshValid(Mesh mesh)
	{

		if ((Object)(object)mesh == (Object)null)
		{
			return false;
		}
		Bounds bounds = mesh.bounds;
		Vector3 size = bounds.size;
		if (size.x > 10000f || size.y > 10000f || size.z > 10000f)
		{
			return false;
		}
		return true;
	}

	protected override void OnDestroy()
	{

		base.OnDestroy();
		if ((Object)(object)mSkeletonGraphic != (Object)null && mHasAddMeshCheckCallback)
		{
			GraphicGraph.AssignMeshOverrideMultipleRenderers -= OnCheckMultipleMeshesValid;
			mHasAddMeshCheckCallback = false;
		}
		if (mTextureLoader)
		{
			AppearanceAssetManager?.RemoveTextureLoaderRef(mTextureLoader, (Object)(object)this);
		}
	}

	protected override void InitSkeleton(ScriptableObject skeletonDataAsset)
	{
		InitComponent();
		GraphicGraph.skeletonDataAsset = skeletonDataAsset;
		if (!string.IsNullOrEmpty(GraphicGraph.startingAnimation))
		{
			UIBindings.TryLazyLoadSpineAnimation(GraphicGraph, GraphicGraph.startingAnimation, out var _);
		}
		GraphicGraph.Initialize(true);
	}

	private void InitComponent()
	{

		if (!mInitComponent)
		{
			if (!mSkeletonGraphic)
			{
				mSkeletonGraphic = ((Component)this).GetComponent<IProfessionAppearanceUIGraphic>() as Component;
			}
			GraphicGraph.allowMultipleCanvasRenderers = true;
			GraphicGraph.OnCustomDemandLoading += HandleOnCustomDemandLoading;
			mInitComponent = true;
			if (mTextureLoader)
			{
				AppearanceAssetManager.AddTextureLoaderRef(mTextureLoader, (Object)(object)this);
			}
			if ((Object)(object)mSkeletonGraphic != (Object)null && !mHasAddMeshCheckCallback)
			{
				GraphicGraph.AssignMeshOverrideMultipleRenderers += OnCheckMultipleMeshesValid;
				mHasAddMeshCheckCallback = true;
			}
		}
	}

	public override void SetSkins(List<string> skins)
	{

		if (AppearanceMaterialUtils.IsMaskable(mSkeletonGraphic) && AppearanceMaterialUtils.GetStencilValue(((Component)mSkeletonGraphic).transform) == 0)
		{
			((Graphic)mSkeletonGraphic).RegisterDirtyMaterialCallback((UnityAction)OnDirtyMaterialCallback);
			SetupSkin(skins);
			DirtyApplySkin();
		}
		else
		{
			base.SetSkins(skins);
		}
	}

	public override void SetDye(string skinName, Dictionary<int, HsvChange> hsvMap)
	{
		if (mSkinApplyDirty)
		{
			if (mDyeCacheDict == null)
			{
				mDyeCacheDict = new Dictionary<string, Dictionary<int, HsvChange>>();
			}
			mDyeCacheDict[skinName] = hsvMap;
			DirtyDye();
		}
		else
		{
			base.SetDye(skinName, hsvMap);
		}
	}

	private void OnDirtyMaterialCallback()
	{

		if (AppearanceMaterialUtils.GetStencilValue(((Component)mSkeletonGraphic).transform) == 0)
		{
            Debug.Log(name + " MaterialCallback but stencil still is 0");
			return;
		}
		((Graphic)mSkeletonGraphic).UnregisterDirtyMaterialCallback((UnityAction)OnDirtyMaterialCallback);
		if (mSkinApplyDirty)
		{
			mSkinApplyDirty = false;
			ApplySkin();
		}
		if (!mDyeDirty)
		{
			return;
		}
		mDyeDirty = false;
		string text = default;
		Dictionary<int, HsvChange> dictionary = default;
		foreach (KeyValuePair<string, Dictionary<int, HsvChange>> item in mDyeCacheDict)
		{
			text = item.Key; dictionary = item.Value;
			string skinName = text;
			Dictionary<int, HsvChange> hsvMap = dictionary;
			base.SetDye(skinName, hsvMap);
		}
	}

	private void DirtyApplySkin()
	{
		mSkinApplyDirty = true;
	}

	private void DirtyDye()
	{
		mDyeDirty = true;
	}

	private void HandleOnCustomDemandLoading(IProfessionAppearanceUIGraphic skeletonGraphic)
	{
		if (!skeletonGraphic.allowMultipleCanvasRenderers || skeletonGraphic.Skeleton.Skin == null)
		{
			return;
		}
		Material[] items = skeletonGraphic.MaterialsMultipleCanvasRenderers;
		Texture[] items2 = skeletonGraphic.TexturesMultipleCanvasRenderers;
		YooAssetTextureLoader yooAssetTextureLoader = mTextureLoader;
		if (!((Object)(object)yooAssetTextureLoader != (Object)null))
		{
			return;
		}
		yooAssetTextureLoader.TryInit();
		int i = 0;
		for (int num = items.Length; i < num; i++)
		{
			if ((Object)(object)items[i] == (Object)null)
			{
				continue;
			}
			Material val = (items[i] = OnDemandLoadingReplaceMaterial(items[i], items2[i], out var isCreate));
			if (isCreate)
			{
				if (!((Object)(object)val == (Object)null))
				{
					Material val2 = null;
					yooAssetTextureLoader.RequestLoadMaterialTextures(val, ref val2);
					yooAssetTextureLoader.AddLoadedTextureRef(items2[i]);
					items2[i] = val.mainTexture;
				}
			}
			else
			{
				items2[i] = val.mainTexture;
			}
		}
		ReplaceMaterialsAfterLoad?.Invoke(items);
	}

	private Material OnDemandLoadingReplaceMaterial(Material material, Texture texture, out bool isCreate)
	{
		isCreate = !mIndependentMaterialDict.TryGetValue(texture, out var value);
		IProfessionAppearanceUIAssetManager appearanceAssetManager = AppearanceAssetManager;
		if (appearanceAssetManager == null)
		{
			return null;
		}
		if (isCreate)
		{
			if ((Object)(object)mTextureLoader == (Object)null)
			{
				return value;
			}
			if (mTextureLoader.PlaceholderTextureIndexDict.ContainsKey(texture))
			{
				Material uIMaterial = appearanceAssetManager.GetUIMaterial();
				AppearanceMaterialUtils.InitUIMaterial(uIMaterial, texture, material);
				value = uIMaterial;
				mIndependentMaterialDict.Add(texture, value);
			}
		}
		return value;
	}

	protected override Material GetMaterial(string skinName)
	{
		SkinTextureConfig skinTextureConfig = GetSkinTextureConfig(skinName);
		if (skinTextureConfig == null)
		{
			return null;
		}
		mIndependentMaterialDict.TryGetValue(skinTextureConfig.PlaceHolderTexture, out var value);
		return value;
	}

	protected override void OnSkinUpdated()
	{
		GraphicGraph.ApplyAnimation();
		GraphicGraph.SyncSubmeshGraphicsWithCanvasRenderers();
		GraphicGraph.MatchRectTransformWithBounds();
		base.OnSkinUpdated();
	}

	public void PlayAnimation(string animationName, bool loop)
	{
		UIBindings.PlayAnimation(GraphicGraph, animationName, loop);
	}

	public override void Release(bool tryUnloadAllAssets)
	{

		Texture val = default;
		Material val2 = default;
		foreach (KeyValuePair<Texture, Material> item in mIndependentMaterialDict)
		{
			val = item.Key; val2 = item.Value;
			Material val3 = val2;
			mTextureLoader.RemoveLoadedTextureRef(val3.mainTexture);
			Object.Destroy((Object)(object)val3);
		}
		mIndependentMaterialDict.Clear();
		base.Release(tryUnloadAllAssets);
		SkeletonGraphicRect.sizeDelta = Vector2.zero;
		mSkinApplyDirty = false;
		mDyeDirty = false;
		mDyeCacheDict?.Clear();
		if ((Object)(object)mSkeletonGraphic != (Object)null)
		{
			((Graphic)mSkeletonGraphic).UnregisterDirtyMaterialCallback((UnityAction)OnDirtyMaterialCallback);
		}
	}
}

}
