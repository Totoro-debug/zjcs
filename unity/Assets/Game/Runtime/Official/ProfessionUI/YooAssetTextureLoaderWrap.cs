namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;

public class YooAssetTextureLoaderWrap
{
	public YooAssetTextureLoader TextureLoader;

	public int RefCount;

	public SharedLoadedMaterialData[] SharedLoadedMaterialItems;

	public YooAssetTextureLoaderWrap(YooAssetTextureLoader textureLoader)
	{
		TextureLoader = textureLoader;
		SharedLoadedMaterialItems = new SharedLoadedMaterialData[textureLoader.placeholderMap.Length];
		int i = 0;
		for (int num = SharedLoadedMaterialItems.Length; i < num; i++)
		{
			SharedLoadedMaterialItems[i] = new SharedLoadedMaterialData();
		}
	}
}

}
