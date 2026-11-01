using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;

public class SkeletonRendererTextureLoadHandler
{
	private readonly YooAssetTextureLoader mTextureLoader;

	private readonly IProfessionAppearanceRendererLite mSkeletonRenderer;

	private readonly IEnumerable<Material> mMaterials;

	private bool mLoadDirty = true;

	private List<Texture> mLoadedTextures;

	private IAppearanceAssetManager mAppearanceAssetManager;

	public SkeletonRendererTextureLoadHandler(IAppearanceAssetManager appearanceAssetManager, IProfessionAppearanceRendererLite skeletonRenderer, YooAssetTextureLoader textureLoader, IEnumerable<Material> materials)
	{


		if (appearanceAssetManager != null && skeletonRenderer != null && skeletonRenderer.Component && textureLoader)
		{
			mAppearanceAssetManager = appearanceAssetManager;
			mSkeletonRenderer = skeletonRenderer;
			mTextureLoader = textureLoader;
			mMaterials = materials;
			mSkeletonRenderer.OnCustomDemandLoading += OnCustomDemandLoading;
			mAppearanceAssetManager.AddTextureLoaderRef(textureLoader, skeletonRenderer.Component);
		}
	}

	private void OnCustomDemandLoading(IProfessionAppearanceRendererLite skeletonRenderer)
	{
		LoadTexturesIfDirty();
	}

	public void LoadTexturesIfDirty()
	{
		if (mLoadDirty)
		{
			TryLoadTextures();
			mLoadDirty = false;
		}
	}

	public void TryLoadTextures()
	{
		mTextureLoader.TryInit();
		if (mLoadedTextures == null)
		{
			mLoadedTextures = new List<Texture>();
		}
		foreach (Material mMaterial in mMaterials)
		{
			Texture mainTexture = mMaterial.mainTexture;
			Material val = null;
			mTextureLoader.RequestLoadMaterialTextures(mMaterial, ref val);
			Texture mainTexture2 = mMaterial.mainTexture;
			if ((Object)(object)mainTexture != (Object)(object)mainTexture2)
			{
				mLoadedTextures.Add(mainTexture2);
				mTextureLoader.AddLoadedTextureRef(mainTexture);
			}
		}
	}

	public void SetDirty()
	{
		mLoadDirty = true;
	}

	public void Clear()
	{
		if (mLoadedTextures == null)
		{
			return;
		}
		foreach (Texture mLoadedTexture in mLoadedTextures)
		{
			mTextureLoader.RemoveLoadedTextureRef(mLoadedTexture);
		}
		mLoadedTextures.Clear();
		SetDirty();
	}

	public void Destroy()
	{
		Clear();
		mAppearanceAssetManager?.RemoveTextureLoaderRef(mTextureLoader, mSkeletonRenderer?.Component);
	}
}
}
