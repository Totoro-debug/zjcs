namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public interface IAppearanceAssetManager : IProfessionAppearanceUIAssetManager
{
	int MaxRendererMaterialCount { get; }

	IReadOnlyDictionary<Material, Stack<Material>> RendererMaterials { get; }

	Material GetRendererMaterial(Material originalMaterial);

	void ReturnRendererMaterial(Material material, Material originalMaterial);

	void GetLoaderSharedMaterials(YooAssetTextureLoader textureLoader, Material[] sourceMaterials, ref Material[] sharedMaterials);

	void ReturnLoaderSharedMaterials(YooAssetTextureLoader textureLoader, Material[] sourceMaterials, bool unloadTextureImmediately);
}

}
