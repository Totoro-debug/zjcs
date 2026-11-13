// Migrated selected original; see story-mask-runtime-source-manifest.json.
using System;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.StoryMasks
{
using Object = UnityEngine.Object;
using UnityEngine;

public class UIBloomBuiltInGroupRegister : MonoBehaviour
{
	public UIBloomGroup UIBloomGroup;

	public UIBloomBuiltGroupPriority Priority;

	public IStoryMaskBloomRegistry UIBloomManager { get; set; }

	protected virtual void OnEnable()
	{
		if ((Object)(object)UIBloomGroup != (Object)null)
		{
			UIBloomManager?.RegisterBuiltInGroup(UIBloomGroup, Priority);
		}
	}

	protected virtual void OnDisable()
	{
		if ((Object)(object)UIBloomGroup != (Object)null)
		{
			UIBloomManager?.UnregisterBuiltInGroup(UIBloomGroup, Priority);
		}
	}
}

}
