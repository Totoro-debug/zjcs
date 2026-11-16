// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using System;

[Serializable]
public class DialogOptionRemainTimeData : DialogOptionData
{
	public override EOptionTalkItemTextPresentType TextPresentType => EOptionTalkItemTextPresentType.RemainTimeText;

	public DateTime EndTime { get; private set; }

	public WorldTimeSpanFormatType TimeSpanFormat { get; private set; }

	public string CustomKey { get; private set; }

	public DialogOptionRemainTimeData(IStoryOptionServices services, string storyName, int id, DateTime endTime)
		: base(services, storyName, id)
	{
		EndTime = endTime;
		TimeSpanFormat = WorldTimeSpanFormatType.Detail;
		CustomKey = TextWrap.Key;
	}
}

}
