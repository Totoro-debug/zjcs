namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public class CharacterPartPoolEntry
{
	private const float kPoolItemAliveTime = 20f;

	public Vector3 DefaultScale = Vector3.one;

	public GameObject GoPart { get; private set; }

	private IProfessionAssetOperation<GameObject> AssetHandle { get; set; }

	public string AssetPath { get; private set; }

	public bool InPool { get; private set; }

	public float LeftAliveTime { get; set; }

	public bool Valid
	{
		get
		{
			if ((Object)(object)GoPart != (Object)null && AssetHandle != null)
			{
				return !string.IsNullOrEmpty(AssetPath);
			}
			return false;
		}
	}

	private CharacterPartPoolEntry()
	{

	}

	public CharacterPartPoolEntry(GameObject goPart, string assetPath, IProfessionAssetOperation<GameObject> assetHandle, bool inPool)
	{

		GoPart = goPart;
		AssetPath = assetPath;
		AssetHandle = assetHandle;
		InPool = inPool;
		DefaultScale = goPart.transform.localScale;
		ResetAliveTime();
	}

	public void ResetAliveTime()
	{
		LeftAliveTime = 20f;
	}

	public void ResetLocalScale()
	{

		if ((Object)(object)GoPart != (Object)null)
		{
			GoPart.transform.localScale = DefaultScale;
		}
	}
}

}
