namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public struct PartEntry<T> where T : class, IAppearancePart
{
	private IProfessionAppearancePartAsset mHandle;

	public T Part { get; }

	public PartEntry(T part, IProfessionAppearancePartAsset handle)
	{
		Part = part;
		mHandle = handle;
	}

	public void Release(bool tryUnloadAllAssets = false)
	{
		if (Part != null)
		{
			Object.Destroy((Object)(object)Part.GameObject);
		}
		mHandle?.Release(tryUnloadAllAssets);
	}
}

}
