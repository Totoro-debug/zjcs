// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public class ECVisibleControlerNormalView : ECVisibleControlerViewBase
{
	[Header("通过激活（actived）控制的子对象")]
	[SerializeField]
	private GameObject[] mActivedChildren;

	[Header("通过开关（enabled）控制的子对象")]
	[SerializeField]
	private Behaviour[] mEnabledChildren;

	protected override bool UpdateViewModelNodeShow(bool show)
	{
		bool result = false;
		int num = mActivedChildren?.Length ?? 0;
		for (int i = 0; i < num; i++)
		{
			GameObject val = mActivedChildren[i];
			if ((Object)(object)val != (Object)null && val.activeSelf != show)
			{
				val.SetActive(show);
				result = true;
			}
		}
		int num2 = mEnabledChildren?.Length ?? 0;
		for (int j = 0; j < num2; j++)
		{
			Behaviour val2 = mEnabledChildren[j];
			if ((Object)(object)val2 != (Object)null && val2.enabled != show)
			{
				val2.enabled = show;
				result = true;
			}
		}
		return result;
	}
}

}
