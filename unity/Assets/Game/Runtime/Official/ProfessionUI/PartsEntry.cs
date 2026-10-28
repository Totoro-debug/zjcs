namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class PartsEntry
{
	public List<string> SeparatorSlots;

	public List<IAppearancePart> Parts { get; }

	public List<IProfessionAppearancePartAsset> Handles { get; }

	public PartsEntry(int partCapacity = 1)
	{
		Parts = new List<IAppearancePart>(partCapacity);
		Handles = new List<IProfessionAppearancePartAsset>(1);
	}

	public void Release(bool tryUnloadAllAssets = false)
	{
		IProfessionAppearanceUnloadable appearanceUILite;
		foreach (IAppearancePart part in Parts)
		{
			if (part != null)
			{
				if (part is IAppearanceSkeletonPart appearanceSkeletonPart && tryUnloadAllAssets && appearanceSkeletonPart.SkeletonTarget.TryGetComponent<IProfessionAppearanceUnloadable>(out appearanceUILite))
				{
					appearanceUILite.MarkUnloadTextureImmediately();
				}
				Object.Destroy((Object)(object)part.GameObject);
			}
		}
		Parts.Clear();
		SeparatorSlots = null;
		foreach (IProfessionAppearancePartAsset handle in Handles)
		{
			handle?.Release(tryUnloadAllAssets);
		}
		Handles.Clear();
	}
}

}
