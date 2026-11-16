// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using System;

[Serializable]
public class DialogOptionData : DialogOptionDataBase
{
	private readonly IStoryOptionServices mServices;
	private string mStoryName = "";

	private int mId;

	public string StoryName => mStoryName;

	public int Id => mId;

	public override EOptionTalkItemTextPresentType TextPresentType => EOptionTalkItemTextPresentType.NormalText;

	public DialogOptionData(IStoryOptionServices services, string storyName, int id, params object[] textWrapArgs)
	{
		mServices = services ?? throw new ArgumentNullException(nameof(services));
		SetParameter(storyName, id, textWrapArgs);
	}

	public void SetParameter(string storyName, int id, params object[] textWrapArgs)
	{
		mStoryName = storyName;
		mId = id;
		StoryOptionInfo storyOptionInfo = mServices.GetStoryOptionInfo(storyName, id);
		if (storyOptionInfo == null)
		{
			Debug.LogError("DialogOptionData: original error branch.");
			return;
		}
		string storyName2 = storyOptionInfo.StoryName;
		TextWrap = new StoryTextKeyArgWrap(StoryOptionLanguage.GetStoryOptionTextKey(storyName2, id), textWrapArgs);
		TipWrap = new StoryTextKeyArgWrap(StoryOptionLanguage.GetStoryOptionMinirTextKey(storyName2, id));
		bool isBlackIcon = true;
		if (!string.IsNullOrEmpty(storyOptionInfo.ConditionParams))
		{
			isBlackIcon = !mServices.IsConditionMet(storyOptionInfo.ConditionType, storyOptionInfo.ConditionParams);
		}
		IsBlackIcon = isBlackIcon;
	}

	public override string GetIconPath()
	{
		StoryOptionInfo storyOptionInfo = mServices.GetStoryOptionInfo(mStoryName, mId);
		if (storyOptionInfo == null)
		{
			Debug.LogError("DialogOptionData: original error branch.");
			return "";
		}
		string text = (IsBlackIcon ? storyOptionInfo.BlackIcon : storyOptionInfo.Icon);
		if (string.IsNullOrEmpty(text))
		{
			text = storyOptionInfo.Icon;
		}
		return text;
	}
}

}
