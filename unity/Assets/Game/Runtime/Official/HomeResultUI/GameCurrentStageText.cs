// Original selected HomeUI result notification; service/type bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeResultUI
{

public class GameCurrentStageText : GameUIBase
{
	public IHomeResultServices Services { private get; set; }
	public MultiLanguageText myText;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (mFightRoot.InFight)
		{
			ShowStageText(myGame.Component.StageId);
		}
	}

	public void ShowStageText(int stageId)
	{
		IHomeResultStage stageInfo = Services.StageInfoParser.Get(stageId);
		if (stageInfo.Type == StageType.DemonBattlefield)
		{
			StagePartInfoDemonBattlefield partInfo = stageInfo.GetPartInfo<StagePartInfoDemonBattlefield>();
			if (partInfo != null)
			{
				string textFormated = Services.GetTextFormated(LanguageUtils.GetStageDemonBattlefieldNameKey(partInfo.EntryIndex), partInfo.Layer);
				myText.Format(textFormated);
			}
		}
		else if (stageInfo.Type == StageType.VoidFissure)
		{
			StagePartInfoVoidFissure partInfo2 = stageInfo.GetPartInfo<StagePartInfoVoidFissure>();
			if (partInfo2 != null)
			{
				string text = Services.GetText(LanguageUtils.GetVoidFissureNameKey(partInfo2.Layer));
				myText.Format(text);
			}
		}
		else
		{
			myText.Format(Services.GetText(LanguageUtils.GetStageNameKey(stageId)));
		}
	}
}
}
