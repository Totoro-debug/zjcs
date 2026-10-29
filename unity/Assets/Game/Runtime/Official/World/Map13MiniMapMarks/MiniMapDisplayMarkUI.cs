namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public class MiniMapDisplayMarkUI : MiniMapMarkUIBase
{
	public override MiniMapMarkType MarkType => MiniMapMarkType.Display;

	public override bool CanInteractive => true;

	public override void OpenDialog()
	{
		MiniMap.Popup<MiniMapDisplayMarkDialog>(InitDialog);
	}
}

}
