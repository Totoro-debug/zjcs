namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "AppearanceSystem/AssetTextureLoader", fileName = "YooAssetTextureLoader")]
public class YooAssetTextureLoader : AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>
{
	public LevelOfResolution LevelOfResolution;

	public string PlaceholderPath;

	public string TargetTexturePath;

	public string DyeTexturePath;

	public override Texture GetAlreadyLoadedTexture(int materialIndex, out Texture dyeTexture)
	{
		YooAssetRequest textureRequest = loadedDataAtMaterial[materialIndex].textureRequest;
		dyeTexture = textureRequest.DyeTextureHandle.Result;
		return textureRequest.MainTextureHandle.Result;
	}

	public override void CreateTextureRequest(int materialIndex, Material materialToUpdate, Action<Texture> onTextureLoaded)
	{
		loadedDataAtMaterial[materialIndex].textureRequest.MainTextureHandle.Load(placeholderMap[materialIndex].targetTextureReference);
		Texture result = loadedDataAtMaterial[materialIndex].textureRequest.MainTextureHandle.Result;
		if ((Object)(object)materialToUpdate != (Object)null)
		{
			materialToUpdate.mainTexture = result;
		}
		if (placeholderMap[materialIndex].dyeTextureReference.AssetReference != null)
		{
			loadedDataAtMaterial[materialIndex].textureRequest.DyeTextureHandle.Load(placeholderMap[materialIndex].dyeTextureReference);
			Texture result2 = loadedDataAtMaterial[materialIndex].textureRequest.DyeTextureHandle.Result;
			if ((Object)(object)result2 != (Object)null || (Object)(object)materialToUpdate != (Object)null)
			{
				materialToUpdate.SetTexture(AppearanceMaterialUtils.sDyeTexId, result2);
			}
		}
		onTextureLoaded?.Invoke(result);
	}

	protected override void RequestUnloadMaterial(Material material)
	{
		base.RequestUnloadMaterial(material);
		material.SetTexture(AppearanceMaterialUtils.sDyeTexId, (Texture)null);
	}
}

}
