// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using System;

public abstract class OptionBaseItem : UIBase
{
	public abstract void Init(DialogOptionDataBase data, int index, Action<int> onClicked);

	public void OnGameObjectPoolClear()
	{
	}

	public void OnGameObjectPoolReset()
	{
	}
}

}
