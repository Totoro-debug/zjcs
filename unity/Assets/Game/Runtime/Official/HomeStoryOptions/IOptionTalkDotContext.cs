// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
public interface IOptionTalkDotContext
{
	EOptionTalkItemDotType DotType { get; }

	void SetCount(int count);

	int GetCount();
}

}
