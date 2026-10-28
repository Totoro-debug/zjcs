// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public static class MyEntityViewModelControlerUtils
{
	public static bool DefaultUpdateViewModelNodeShow(Transform transform, bool show)
	{
		bool result = false;
		int childCount = transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = transform.GetChild(i);
			if ((Object)(object)child != (Object)null && ((Component)child).gameObject.activeSelf != show)
			{
				((Component)child).gameObject.SetActive(show);
				result = true;
			}
		}
		return result;
	}
}

}
