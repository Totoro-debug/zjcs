// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using System;

[Serializable]
public abstract class DialogOptionDataBase
{

	public StoryTextKeyArgWrap TextWrap;

	public StoryTextKeyArgWrap TipWrap;

	public IOptionTalkDotContext DotContext;

	public bool IsBlackIcon = true;

	public abstract EOptionTalkItemTextPresentType TextPresentType { get; }

	public abstract string GetIconPath();
}

}
