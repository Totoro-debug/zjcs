using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
public abstract class CountRedDotBase : MonoBehaviour
{
	[SerializeField]
	protected MultiLanguageText mCountLanguage;

	public abstract void SetCount(long count);
}

}
