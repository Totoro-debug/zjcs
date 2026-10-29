namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public class MiniMapEntryMarkUI : MiniMapDisplayMarkUI
{
	public override MiniMapMarkType MarkType => MiniMapMarkType.Entry;

	public override bool CanInteractive => true;

	public override void OpenDialog()
	{
		if (!Mark.InteractPossible())
		{
			MiniMap.ShowError(2116);
		}
		else
		{
			MiniMap.Popup<MiniMapEntryMarkDialog>(InitDialog);
		}
	}
}

}
