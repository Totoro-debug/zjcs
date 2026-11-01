using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.CombatAppearance
{
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;

public abstract class AppearanceRenderer : AppearanceSpine
{
	[SerializeField]
	private Component mSkeletonRenderer;

	private SkeletonRendererTextureLoadHandler mTextureLoadHandler;

	private readonly Dictionary<Material, Material> mIndependentMaterialDict = new Dictionary<Material, Material>();

	private MeshRenderer mMeshRenderer;

	private bool mInitComponent;

    private IAppearanceAssetManager AppearanceAssetManager => Bindings.AssetManager;
    private IProfessionAppearanceSceneRenderer RendererGraph => (IProfessionAppearanceSceneRenderer)mSkeletonRenderer;

	public AppearanceRendererPartHandler PartHandler => (AppearanceRendererPartHandler)mPartHandler;

	public IReadOnlyDictionary<Material, Material> IndependentMaterialDict => mIndependentMaterialDict;

	private MeshRenderer MeshRenderer => mMeshRenderer ?? (mMeshRenderer = ((Component)mSkeletonRenderer).GetComponent<MeshRenderer>());

	public override YooAssetTextureLoader TextureLoader => mTextureLoader;

	public event Action<IEnumerable<Material>> OnGetUpdatedSharedMaterialsEvent;

	protected override void OnReleaseBeforeSORelease()
	{
		mTextureLoadHandler?.Destroy();
	}

	protected override void InitSkeleton(ScriptableObject skeletonDataAsset)
	{
		try
		{
			mSkeletonRenderer = ((Component)this).gameObject.GetComponent<IProfessionAppearanceSceneRenderer>() as Component;
		}
		catch (Exception exception)
		{
            Debug.LogError(name + " GetComponent,but gameObject is null\n" + exception);
			throw;
		}
		if ((Object)(object)mSkeletonRenderer == (Object)null)
		{
            Debug.LogError("SkeletonRenderer is null! gameObject:" + gameObject.name);
		}
		else
		{
			RendererGraph.skeletonDataAsset = skeletonDataAsset;
			RendererGraph.Initialize(true, false);
			InitComponent();
		}
	}

	private void InitComponent()
	{


		if (!mInitComponent)
		{
			mInitComponent = true;
			RendererGraph.OnGetUpdatedSharedMaterials += OnGetUpdatedSharedMaterials;
			if ((Object)(object)mTextureLoader != (Object)null)
			{
				mTextureLoadHandler = new SkeletonRendererTextureLoadHandler(AppearanceAssetManager, RendererGraph, mTextureLoader, mIndependentMaterialDict.Values);
			}
			OnInitComponent();
		}
	}

	protected virtual void OnInitComponent()
	{




		AppearanceRendererPartHandler partHandler = PartHandler;
		partHandler.OnGetSharedMaterialDelegate += OnGetUpdatedSharedMaterials;
	}

	private void OnGetUpdatedSharedMaterials(Material[] sharedMaterials)
	{
		bool flag = false;
		int i = 0;
		for (int num = sharedMaterials.Length; i < num; i++)
		{
			Material val = sharedMaterials[i];
			if (!((Object)(object)val == (Object)null))
			{
				if (!mIndependentMaterialDict.TryGetValue(val, out var value))
				{
					value = AppearanceAssetManager.GetRendererMaterial(val);
					flag = true;
					mIndependentMaterialDict.Add(val, value);
					mTextureLoadHandler?.TryLoadTextures();
				}
				sharedMaterials[i] = value;
			}
		}
		if (flag)
		{
			OnGetUpdatedSharedMaterialsEvent?.Invoke(mIndependentMaterialDict.Values);
		}
	}

	protected override Material GetMaterial(string skinName)
	{
		SkinTextureConfig skinTextureConfig = GetSkinTextureConfig(skinName);
		if (skinTextureConfig == null)
		{
			return null;
		}
		Material val = default;
		Material val2 = default;
		foreach (KeyValuePair<Material, Material> item in mIndependentMaterialDict)
		{
			val = item.Key; val2 = item.Value;
			Material val3 = val;
			Material result = val2;
			if ((Object)(object)val3.mainTexture == (Object)(object)skinTextureConfig.PlaceHolderTexture)
			{
				return result;
			}
		}
		return null;
	}

	protected override void OnSkinUpdated()
	{
		ClearInstanceMaterials();
		RendererGraph.LateUpdateMesh();
		mTextureLoadHandler.TryLoadTextures();
		base.OnSkinUpdated();
	}

	public override void Release(bool tryUnloadAllAssets)
	{
		ClearInstanceMaterials();
		base.Release(tryUnloadAllAssets);
		if (tryUnloadAllAssets && (Object)(object)MeshRenderer != (Object)null)
		{
			((Renderer)MeshRenderer).sharedMaterials = Array.Empty<Material>();
		}
	}

	private void ClearInstanceMaterials()
	{
		IAppearanceAssetManager appearanceAssetManager = AppearanceAssetManager;
		if (appearanceAssetManager != null)
		{
			Material val = default;
			Material val2 = default;
			foreach (KeyValuePair<Material, Material> item in mIndependentMaterialDict)
			{
				val = item.Key; val2 = item.Value;
				Material originalMaterial = val;
				Material material = val2;
				appearanceAssetManager.ReturnRendererMaterial(material, originalMaterial);
			}
		}
		mTextureLoadHandler?.Clear();
		mIndependentMaterialDict.Clear();
	}
}
}
