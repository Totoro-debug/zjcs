namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public abstract class AppearanceOnDemandTextureLoader<TargetReference, TextureRequest> : ScriptableObject where TargetReference : ITargetTextureReference where TextureRequest : IOnDemandRequest
{
    public ScriptableObject atlasAsset;
    public ScriptableObject skeletonDataAsset;

	[Serializable]
	public struct PlaceholderTextureMapping
	{
		public Texture placeholderTexture;

		public TargetReference targetTextureReference;

		public TargetReference dyeTextureReference;
	}

	public struct MaterialOnDemandData
	{
		public int lastFrameRequested;

		public int referenceCounter;

		public TextureRequest textureRequest;
	}

	public int maxPlaceholderSize = 4;

	public float unloadAfterSecondsUnused = 60f;

	public PlaceholderTextureMapping[] placeholderMap;

	protected MaterialOnDemandData[] loadedDataAtMaterial;

	private Dictionary<Texture, int> mPlaceholderTextureIndexDict;

	public IReadOnlyDictionary<Texture, int> PlaceholderTextureIndexDict => mPlaceholderTextureIndexDict;

	private void Reset()
	{
		Clear(true);
	}

	public virtual void Clear(bool clearAtlasAsset = false)
	{
		if (clearAtlasAsset)
		{
			atlasAsset = null;
		}
		placeholderMap = null;
		mPlaceholderTextureIndexDict = null;
		loadedDataAtMaterial = null;
	}

	public virtual string GetPlaceholderTextureName(string originalTextureName)
	{
		return originalTextureName + "_placeholder";
	}

	public virtual bool AssignPlaceholderTextures(out IEnumerable<Material> modifiedMaterials)
	{
		modifiedMaterials = null;
		return true;
	}

	public virtual bool AssignTargetTextures(out IEnumerable<Material> modifiedMaterials)
	{
		modifiedMaterials = null;
		return true;
	}

	public virtual void BeginCustomTextureLoading()
	{
		TryInit();
	}

	public virtual void EndCustomTextureLoading()
	{
		TryUnloadUnusedTextures();
	}

	public virtual void RequestLoadMaterialTextures(Material material, ref Material overrideMaterial)
	{
		if ((Object)(object)material == (Object)null)
		{
			return;
		}
		Texture mainTexture = material.mainTexture;
		if ((Object)(object)mainTexture == (Object)null)
		{
			return;
		}
		if (mPlaceholderTextureIndexDict.TryGetValue(mainTexture, out var value))
		{
			RequestLoadTexture(material, value, null);
		}
		int num = -1;
		int i = 0;
		for (int num2 = loadedDataAtMaterial.Length; i < num2; i++)
		{
			TextureRequest textureRequest = loadedDataAtMaterial[i].textureRequest;
			if (textureRequest.WasRequested && textureRequest.IsTarget(mainTexture))
			{
				num = i;
				break;
			}
		}
		if (num >= 0)
		{
			loadedDataAtMaterial[num].lastFrameRequested = Time.frameCount;
		}
	}

	public void TryInit()
	{
		if (mPlaceholderTextureIndexDict == null)
		{
			mPlaceholderTextureIndexDict = new Dictionary<Texture, int>(placeholderMap.Length);
			int i = 0;
			for (int num = placeholderMap.Length; i < num; i++)
			{
				PlaceholderTextureMapping placeholderTextureMapping = placeholderMap[i];
				mPlaceholderTextureIndexDict.Add(placeholderTextureMapping.placeholderTexture, i);
			}
		}
		if (loadedDataAtMaterial == null || (loadedDataAtMaterial.Length == 0 && placeholderMap.Length != 0))
		{
			loadedDataAtMaterial = new MaterialOnDemandData[placeholderMap.Length];
			int j = 0;
			for (int num2 = loadedDataAtMaterial.Length; j < num2; j++)
			{
				loadedDataAtMaterial[j].lastFrameRequested = -1;
			}
		}
	}

	public void AddLoadedTextureRef(Texture placeholderTexture)
	{
		if (mPlaceholderTextureIndexDict.TryGetValue(placeholderTexture, out var value))
		{
			loadedDataAtMaterial[value].referenceCounter++;
		}
	}

	public void RemoveLoadedTextureRef(Texture targetTexture)
	{
		int i = 0;
		for (int num = loadedDataAtMaterial.Length; i < num; i++)
		{
			TextureRequest textureRequest = loadedDataAtMaterial[i].textureRequest;
			if (textureRequest.WasRequested && textureRequest.IsTarget(targetTexture))
			{
				loadedDataAtMaterial[i].referenceCounter--;
				break;
			}
		}
	}

	public void AddLoadedTextureRef(int materialIndex)
	{
		loadedDataAtMaterial[materialIndex].referenceCounter++;
	}

	public void RemoveLoadedTextureRef(int materialIndex)
	{
		TextureRequest textureRequest = loadedDataAtMaterial[materialIndex].textureRequest;
		if (textureRequest.WasRequested)
		{
			loadedDataAtMaterial[materialIndex].referenceCounter--;
		}
	}

	public virtual void RequestLoadTexture(Texture placeholderTexture, ref Texture replacementTexture, Action<Texture> onTextureLoaded = null)
	{
		if ((Object)(object)placeholderTexture == (Object)null)
		{
			return;
		}
		if (mPlaceholderTextureIndexDict.TryGetValue(placeholderTexture, out var value))
		{
			Texture val = RequestLoadTexture(null, value, onTextureLoaded);
			if ((Object)(object)val != (Object)null)
			{
				replacementTexture = val;
			}
			TextureRequest textureRequest = loadedDataAtMaterial[value].textureRequest;
			if (textureRequest.WasRequested && textureRequest.IsTarget(placeholderTexture))
			{
				loadedDataAtMaterial[value].lastFrameRequested = Time.frameCount;
			}
		}
	}

	protected virtual Texture RequestLoadTexture(Material material, int materialIndex, Action<Texture> onTextureLoaded)
	{
		loadedDataAtMaterial[materialIndex].lastFrameRequested = Time.frameCount;
		if (loadedDataAtMaterial[materialIndex].textureRequest.WasRequested)
		{
			Texture alreadyLoadedTexture = GetAlreadyLoadedTexture(materialIndex, out var dyeTexture);
			if ((Object)(object)alreadyLoadedTexture != (Object)null)
			{
				if ((Object)(object)material != (Object)null)
				{
					material.mainTexture = alreadyLoadedTexture;
					if ((Object)(object)dyeTexture != (Object)null)
					{
						material.SetTexture(AppearanceMaterialUtils.sDyeTexId, dyeTexture);
					}
				}
				onTextureLoaded?.Invoke(alreadyLoadedTexture);
			}
			return alreadyLoadedTexture;
		}
		CreateTextureRequest(materialIndex, material, onTextureLoaded);
		return null;
	}

	public abstract Texture GetAlreadyLoadedTexture(int materialIndex, out Texture dyeTexture);

	public abstract void CreateTextureRequest(int materialIndex, Material materialToUpdate, Action<Texture> onTextureLoaded);

	public virtual void TryUnloadUnusedTextures(bool ignoreRequestFrame = false)
	{
		if (loadedDataAtMaterial == null)
		{
			return;
		}
		int frameCount = Time.frameCount;
		float smoothDeltaTime = Time.smoothDeltaTime;
		float num = unloadAfterSecondsUnused / smoothDeltaTime;
		int i = 0;
		for (int num2 = loadedDataAtMaterial.Length; i < num2; i++)
		{
			MaterialOnDemandData materialOnDemandData = loadedDataAtMaterial[i];
			TextureRequest textureRequest = materialOnDemandData.textureRequest;
			bool flag = ignoreRequestFrame || (float)(frameCount - materialOnDemandData.lastFrameRequested) > num;
			if ((textureRequest.WasSuccessfullyLoaded && materialOnDemandData.referenceCounter <= 0) & flag)
			{
				RequestUnloadTexture(i);
			}
		}
	}

	protected virtual void RequestUnloadMaterial(Material material)
	{
	}

	public virtual void RequestUnloadTexture(int materialIndex)
	{
		if (materialIndex >= loadedDataAtMaterial.Length)
		{
			return;
		}
		PlaceholderTextureMapping placeholderTextureMapping = placeholderMap[materialIndex];
		if (loadedDataAtMaterial[materialIndex].textureRequest.WasRequested)
		{
			loadedDataAtMaterial[materialIndex].textureRequest.Release();
		}
        if (atlasAsset)
        {
            Material val2 = ProfessionAppearanceTextureBindings.Required.GetAtlasMaterial(atlasAsset, materialIndex);
            if (val2)
            {
                val2.mainTexture = placeholderTextureMapping.placeholderTexture;
                RequestUnloadMaterial(val2);
            }
        }
	}

	public virtual bool HasPlaceholderTexturesAssigned(out List<Material> placeholderMaterials)
	{
		throw new NotImplementedException();
	}

	public virtual bool HasPlaceholderAssigned(Material material)
	{
		throw new NotImplementedException();
	}
}

}
