namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
public class AppearanceAssetManager : IAppearanceAssetManager, IDisposable
{
	private readonly Dictionary<YooAssetTextureLoader, YooAssetTextureLoaderWrap> mUsingTextureLoaderDict = new Dictionary<YooAssetTextureLoader, YooAssetTextureLoaderWrap>();

	private Dictionary<Material, Stack<Material>> mRendererMaterials = new Dictionary<Material, Stack<Material>>();

	private bool mIsValid;

	private int mMaxRendererMaterialCount;

    private IDisposable mLowMemorySubscription;
    private readonly AppearanceAssetManagerConfig Config;
    private readonly Func<Action, IDisposable> subscribeLowMemory;
    public AppearanceAssetManager(AppearanceAssetManagerConfig config, Func<Action, IDisposable> subscribeLowMemory)
    {
        Config = config ? config : throw new ArgumentNullException(nameof(config));
        this.subscribeLowMemory = subscribeLowMemory ?? throw new ArgumentNullException(nameof(subscribeLowMemory));
        Initialize();
    }

	IReadOnlyDictionary<Material, Stack<Material>> IAppearanceAssetManager.RendererMaterials => mRendererMaterials;

	int IAppearanceAssetManager.MaxRendererMaterialCount => mMaxRendererMaterialCount;

	private void Initialize()
	{
		mLowMemorySubscription = subscribeLowMemory(OnLowMemory);
		mIsValid = true;
	}

	public void Dispose()
	{
		mLowMemorySubscription.Dispose();
		Clear();
		mIsValid = false;
	}

	private void OnLowMemory()
	{
		ClearRendererMaterials();
		YooAssetTextureLoader yooAssetTextureLoader = default;
		YooAssetTextureLoaderWrap yooAssetTextureLoaderWrap = default;
		foreach (KeyValuePair<YooAssetTextureLoader, YooAssetTextureLoaderWrap> item in mUsingTextureLoaderDict)
		{
			item.Deconstruct(out yooAssetTextureLoader, out yooAssetTextureLoaderWrap);
			yooAssetTextureLoader.TryUnloadUnusedTextures(ignoreRequestFrame: true);
		}
	}

	public void Update()
	{
		YooAssetTextureLoader yooAssetTextureLoader = default;
		YooAssetTextureLoaderWrap yooAssetTextureLoaderWrap = default;
		foreach (KeyValuePair<YooAssetTextureLoader, YooAssetTextureLoaderWrap> item in mUsingTextureLoaderDict)
		{
			item.Deconstruct(out yooAssetTextureLoader, out yooAssetTextureLoaderWrap);
			yooAssetTextureLoader.TryUnloadUnusedTextures();
		}
	}

	Material IAppearanceAssetManager.GetRendererMaterial(Material originalMaterial)
	{

		if (!mIsValid)
		{
			return null;
		}
		if (!mRendererMaterials.TryGetValue(originalMaterial, out var value) || value.Count <= 0)
		{
			return new Material(originalMaterial);
		}
		return value.Pop();
	}

	void IAppearanceAssetManager.ReturnRendererMaterial(Material material, Material originalMaterial)
	{
		if (mIsValid)
		{
			AppearanceMaterialUtils.ResetRenderMaterial(material, originalMaterial);
			if (!mRendererMaterials.TryGetValue(originalMaterial, out var value))
			{
				value = new Stack<Material>();
				mRendererMaterials.Add(originalMaterial, value);
			}
			value.Push(material);
			if (mRendererMaterials.Count > mMaxRendererMaterialCount)
			{
				mMaxRendererMaterialCount = mRendererMaterials.Count;
			}
		}
	}

	Material IProfessionAppearanceUIAssetManager.GetUIMaterial()
	{
		if (!mIsValid)
		{
			return null;
		}
		return AppearanceMaterialUtils.CreateUIMaterial(Config.UIDyeShader);
	}

	void IProfessionAppearanceUIAssetManager.AddTextureLoaderRef(YooAssetTextureLoader textureLoader, Object go)
	{
		if (!mIsValid)
		{
			return;
		}
		if ((Object)(object)textureLoader == (Object)null)
		{
			Debug.LogWarning("textureLoader is null");
		}
		else
		{
			if (!mUsingTextureLoaderDict.TryGetValue(textureLoader, out var value))
			{
				value = new YooAssetTextureLoaderWrap(textureLoader);
				mUsingTextureLoaderDict.Add(textureLoader, value);
			}
			value.RefCount++;
		}
	}

	void IProfessionAppearanceUIAssetManager.RemoveTextureLoaderRef(YooAssetTextureLoader textureLoader, Object go)
	{
		if (!mIsValid)
		{
			return;
		}
		YooAssetTextureLoaderWrap value;
		if ((Object)(object)textureLoader == (Object)null)
		{
			Debug.LogWarning("textureLoader is null");
		}
		else if (mUsingTextureLoaderDict.TryGetValue(textureLoader, out value))
		{
			value.RefCount--;
			if (value.RefCount <= 0)
			{
				textureLoader.TryUnloadUnusedTextures(ignoreRequestFrame: true);
				mUsingTextureLoaderDict.Remove(textureLoader);
			}
		}
	}

	void IAppearanceAssetManager.GetLoaderSharedMaterials(YooAssetTextureLoader textureLoader, Material[] sourceMaterials, ref Material[] sharedMaterials)
	{

		if (!mIsValid)
		{
			return;
		}
		((IAppearanceAssetManager)this).AddTextureLoaderRef(textureLoader, (Object)null);
		if (!mUsingTextureLoaderDict.TryGetValue(textureLoader, out var value))
		{
			return;
		}
		textureLoader.TryInit();
		SharedLoadedMaterialData[] sharedLoadedMaterialItems = value.SharedLoadedMaterialItems;
		int i = 0;
		for (int num = sharedMaterials.Length; i < num; i++)
		{
			Material val = sourceMaterials[i];
			Texture mainTexture = val.mainTexture;
			if (!textureLoader.PlaceholderTextureIndexDict.TryGetValue(mainTexture, out var value2))
			{
				continue;
			}
			SharedLoadedMaterialData sharedLoadedMaterialData = sharedLoadedMaterialItems[value2];
			Material val2 = sharedLoadedMaterialData.SharedMaterial;
			if (sharedLoadedMaterialData.MaterialRefCount == 0)
			{
				if ((Object)(object)val2 == (Object)null)
				{
					val2 = new Material(val);
					((Object)val2).name = $"{((Object)val2).name}_{textureLoader.LevelOfResolution}";
					sharedLoadedMaterialData.SharedMaterial = val2;
				}
				Material val3 = null;
				textureLoader.RequestLoadMaterialTextures(val2, ref val3);
				textureLoader.AddLoadedTextureRef(value2);
			}
			sharedMaterials[i] = val2;
			sharedLoadedMaterialData.MaterialRefCount++;
		}
	}

	void IAppearanceAssetManager.ReturnLoaderSharedMaterials(YooAssetTextureLoader textureLoader, Material[] sourceMaterials, bool unloadTextureImmediately)
	{
		if (!mIsValid || !mUsingTextureLoaderDict.TryGetValue(textureLoader, out var value))
		{
			return;
		}
		SharedLoadedMaterialData[] sharedLoadedMaterialItems = value.SharedLoadedMaterialItems;
		foreach (Material val in sourceMaterials)
		{
			if (!textureLoader.PlaceholderTextureIndexDict.TryGetValue(val.mainTexture, out var value2))
			{
				continue;
			}
			SharedLoadedMaterialData sharedLoadedMaterialData = sharedLoadedMaterialItems[value2];
			sharedLoadedMaterialData.MaterialRefCount--;
			if (sharedLoadedMaterialData.MaterialRefCount <= 0)
			{
				Material sharedMaterial = sharedLoadedMaterialData.SharedMaterial;
				if (!((Object)(object)sharedMaterial == (Object)null))
				{
					textureLoader.RemoveLoadedTextureRef(value2);
					sharedMaterial.mainTexture = null;
					UnityEngine.Object.Destroy((Object)(object)sharedMaterial);
					sharedLoadedMaterialData.SharedMaterial = null;
				}
			}
		}
		textureLoader.TryUnloadUnusedTextures(unloadTextureImmediately);
		((IAppearanceAssetManager)this).RemoveTextureLoaderRef(textureLoader, (Object)null);
	}

	private void ClearRendererMaterials()
	{
		Material val = default;
		Stack<Material> stack = default;
		foreach (KeyValuePair<Material, Stack<Material>> mRendererMaterial in mRendererMaterials)
		{
			mRendererMaterial.Deconstruct(out val, out stack);
			Stack<Material> materials = stack;
			ClearMaterials(materials);
		}
	}

	private void ClearMaterials(Stack<Material> materials)
	{
		int i = 0;
		for (int count = materials.Count; i < count; i++)
		{
			UnityEngine.Object.Destroy((Object)(object)materials.Pop());
		}
	}

	private void Clear()
	{
		ClearRendererMaterials();
		mRendererMaterials = null;
	}
}

}
