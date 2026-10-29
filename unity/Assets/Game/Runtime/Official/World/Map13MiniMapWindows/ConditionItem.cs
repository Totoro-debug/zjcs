using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
public class ConditionItem : MonoBehaviour
{
	[SerializeField]
	private MultiLanguageText mDesc;

	[SerializeField]
	private StateSwitcher<bool> mStateSwitcher;

	public ConditionItem SetDesc(string key, params object[] desc)
	{
		mDesc.SetKey(key, desc);
		return this;
	}

	public ConditionItem SetMet(bool isMet)
	{
		mStateSwitcher.SetState(isMet);
		return this;
	}
}

}
