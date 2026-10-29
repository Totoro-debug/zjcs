using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public sealed class MiniMapWayPointMarkUI : MiniMapDisplayMarkUI
{
	private IMiniMapWayPointBlockHandler[] mBlockHandlers;

	public override MiniMapMarkType MarkType => MiniMapMarkType.WayPoint;

	public override bool CanInteractive => true;

	protected override void Awake()
	{
		base.Awake();
		mBlockHandlers = ((Component)this).GetComponents<IMiniMapWayPointBlockHandler>();
	}

	public override void OpenDialog()
	{
		if (!Mark.InteractPossible())
		{
			MiniMap.ShowError(2116);
			return;
		}
		if (mBlockHandlers != null && mBlockHandlers.Length != 0)
		{
			IMiniMapWayPointBlockHandler[] array = mBlockHandlers;
			for (int i = 0; i < array.Length; i++)
			{
				if (!array[i].CanUse())
				{
					return;
				}
			}
		}
		MiniMap.Popup<MiniMapWayPointMarkDialog>(InitDialog);
	}
}

}
