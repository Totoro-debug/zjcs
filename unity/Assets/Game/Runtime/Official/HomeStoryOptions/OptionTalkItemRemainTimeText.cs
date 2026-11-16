// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

public class OptionTalkItemRemainTimeText : OptionTalkItemBaseText
{
	[SerializeField]
	private WorldShowRemainTimeInfo ShowRemainTimeCtrl;

	[SerializeField]
	private GameObject OptionItem;

	public override void RefreshText(DialogOptionDataBase data)
	{
		DialogOptionRemainTimeData dialogOptionRemainTimeData = data as DialogOptionRemainTimeData;
		ShowRemainTimeCtrl.Clock = Services.Now;
		ShowRemainTimeCtrl.Init(dialogOptionRemainTimeData.EndTime, 1f, timeFormat: dialogOptionRemainTimeData.TimeSpanFormat, key: dialogOptionRemainTimeData.CustomKey, onEnd: OnTimeEnd);
		base.RefreshText(data);
	}

	private void OnTimeEnd()
	{
		OptionItem.SetActive(false);
	}
}

}
