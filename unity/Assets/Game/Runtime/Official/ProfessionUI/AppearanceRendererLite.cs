using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
[ExecuteAlways]
public class AppearanceRendererLite : MonoBehaviour, IProfessionAppearanceUnloadable
{
	public Component SkeletonRenderer;

	public LevelOfResolution LevelOfResolution = LevelOfResolution.Low;

	public List<Component> RenderExistingMeshes;

	private bool mInit;

	private Material[] mSkinMaterials;

	private Material[] mMaterials;

	private Material[] mSourceMaterials;

	private YooAssetTextureLoader mTextureLoader;

	private SkeletonRendererTextureLoadHandler mTextureLoadHandler;

	private AppearanceRendererLiteMaterialOverride mMaterialOverride;

	private bool mUnloadTextureImmediately;

	private List<System.IDisposable> mDisposableBag;

    private IProfessionAppearanceUILiteBindings mBindings;
    private IProfessionAppearanceUILiteBindings Bindings => mBindings ?? ProfessionAppearanceUILiteBindings.Required;
    private IAppearanceAssetManager AppearanceAssetManager => (IAppearanceAssetManager)Bindings.AssetManager;
    private IProfessionAppearanceRendererLite RendererGraph => (IProfessionAppearanceRendererLite)SkeletonRenderer;
    public void Configure(IProfessionAppearanceUILiteBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public YooAssetTextureLoader TextureLoader
	{
		get
		{

			if ((Object)(object)mTextureLoader == (Object)null)
			{
				mDisposableBag = default;
				string name = RendererGraph.SkeletonDataName;
                string skeletonPath = Bindings.Config.GetSkeletonPath(name);
                string textureLoaderPath = $"{skeletonPath}/{name}_TextureLoader_{LevelOfResolution}.asset";
				var handle = Bindings.LoadAsset(textureLoaderPath);
                if (mDisposableBag == null) mDisposableBag = new List<System.IDisposable>();
                mDisposableBag.Add(handle);
                mTextureLoader = (YooAssetTextureLoader)handle.Result;
			}
			return mTextureLoader;
		}
	}

	private void Reset()
	{
		SkeletonRenderer = ((Component)this).GetComponent<IProfessionAppearanceRendererLite>() as Component;
	}

	private void Awake()
	{
		mMaterialOverride = ((Component)this).GetComponent<AppearanceRendererLiteMaterialOverride>();
		if (!mInit && TextureLoader && AppearanceAssetManager != null)
		{
			if (mMaterialOverride)
			{
				TryInit();
			}
			else
			{
				UseSharedMaterials();
			}
		}
	}

	private void OnDestroy()
	{
		ReturnSharedMaterials();
		RemoveOverrideMaterials();
		if (mMaterials != null)
		{
			Material[] array = mMaterials;
			for (int i = 0; i < array.Length; i++)
			{
				Object.Destroy((Object)(object)array[i]);
			}
			mMaterials = null;
		}
		mSkinMaterials = null;
		mTextureLoader = null;
		if (mDisposableBag != null) foreach (var handle in mDisposableBag) handle.Dispose();
	}

	public void MarkUnloadTextureImmediately()
	{
		mUnloadTextureImmediately = true;
	}

	public Material[] GetIndependentMaterials()
	{
		TryInit();
		return mMaterials;
	}

	private void TryInit()
	{
		if (mInit)
		{
			return;
		}
		if ((Object)(object)SkeletonRenderer == (Object)null)
		{
			SkeletonRenderer = ((Component)this).GetComponent<IProfessionAppearanceRendererLite>() as Component;
		}
		if ((Object)(object)SkeletonRenderer == (Object)null)
		{
			Debug.LogError((object)"skeletonRenderer == null");
			return;
		}
		RendererGraph.Initialize(false, false);
		ReturnSharedMaterials();
		RendererGraph.UpdateAnimationAndMesh();
		mMaterials = CreateOverrideMaterials();
		if ((Object)(object)TextureLoader != (Object)null)
		{
			mTextureLoadHandler = new SkeletonRendererTextureLoadHandler(AppearanceAssetManager, RendererGraph, TextureLoader, mMaterials);
			if ((Object)(object)mMaterialOverride != (Object)null && RendererGraph.SeparatorSlotCount > 0)
			{
				mTextureLoadHandler.LoadTexturesIfDirty();
			}
		}
		mInit = true;
	}

	private Material[] CreateOverrideMaterials()
	{
		Material[] array = CollectSkinMaterials();
		Material[] array2 = new Material[array.Length];
		Dictionary<Material, Material> customMaterialOverride = RendererGraph.CustomMaterialOverride;
		customMaterialOverride.Clear();
		ResetRenderExistingMeshRuntimeMaterials();
		int i = 0;
		for (int num = array.Length; i < num; i++)
		{
			Material val = array[i];
			AddRenderExistingMeshRuntimeMaterial(val, array2[i] = (customMaterialOverride[val] = CreateOverrideMaterial(val)));
		}
		return array2;
	}

	private void RemoveOverrideMaterials()
	{
		if (!((Object)(object)SkeletonRenderer == (Object)null) && mInit)
		{
			RendererGraph.CustomMaterialOverride.Clear();
			ResetRenderExistingMeshRuntimeMaterials();
			mTextureLoadHandler?.Destroy();
		}
	}

	private void UseSharedMaterials()
	{
		Material[] array = (mSourceMaterials = CollectSkinMaterials());
		Material[] sharedMaterials = new Material[array.Length];
		Dictionary<Material, Material> customMaterialOverride = RendererGraph.CustomMaterialOverride;
		customMaterialOverride.Clear();
		ResetRenderExistingMeshRuntimeMaterials();
		AppearanceAssetManager.GetLoaderSharedMaterials(TextureLoader, array, ref sharedMaterials);
		int i = 0;
		for (int num = array.Length; i < num; i++)
		{
			Material val = array[i];
			Material runtimeMaterial = sharedMaterials[i];
			customMaterialOverride[val] = sharedMaterials[i];
			AddRenderExistingMeshRuntimeMaterial(val, runtimeMaterial);
		}
	}

	private Material[] CollectSkinMaterials()
	{
		return CollectSkinMaterials(RendererGraph.Skeleton.Skin);
	}

	protected virtual Material[] CollectSkinMaterials(IProfessionAppearanceSkin skin)
	{




		if (skin != null)
		{
			if (mSkinMaterials != null)
			{
				return mSkinMaterials;
			}
			List<Material> list = new List<Material>();
            foreach (Material item in RendererGraph.SkinMaterials(skin))
                if (!list.Contains(item)) list.Add(item);
			mSkinMaterials = list.ToArray();
			return mSkinMaterials;
		}
		return ((Renderer)((Component)this).GetComponent<MeshRenderer>()).sharedMaterials;
	}

	private void ReturnSharedMaterials()
	{
		if (mSourceMaterials != null)
		{
			if (AppearanceAssetManager != null && (Object)(object)mTextureLoader != (Object)null)
			{
				AppearanceAssetManager.ReturnLoaderSharedMaterials(mTextureLoader, mSourceMaterials, mUnloadTextureImmediately);
			}
			RendererGraph.CustomMaterialOverride.Clear();
			ResetRenderExistingMeshRuntimeMaterials();
			mSourceMaterials = null;
		}
	}

	private void AddRenderExistingMeshRuntimeMaterial(Material originalMaterial, Material runtimeMaterial)
	{
		if (RenderExistingMeshes == null)
		{
			return;
		}
		foreach (Component component in RenderExistingMeshes)
		{
			((IProfessionAppearanceMaterialReplacement)component).AddRuntimeReplacementMaterial(originalMaterial, runtimeMaterial);
		}
	}

	private void ResetRenderExistingMeshRuntimeMaterials()
	{
		if (RenderExistingMeshes == null)
		{
			return;
		}
		foreach (Component component in RenderExistingMeshes)
		{
			((IProfessionAppearanceMaterialReplacement)component).ResetRuntimeReplacementMaterials();
		}
	}

	private Material CreateOverrideMaterial(Material originalMaterial)
	{




		Material val = new Material(((Object)(object)mMaterialOverride != (Object)null) ? mMaterialOverride.GetOverrideMaterial(originalMaterial) : originalMaterial);
		((Object)val).name = ((Object)val).name + "_instance";
		return val;
	}
}
}
