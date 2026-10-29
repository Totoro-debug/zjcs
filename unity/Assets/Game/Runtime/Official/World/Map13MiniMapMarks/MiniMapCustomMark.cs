using System.Collections.Generic;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public class MiniMapCustomMark : MiniMapMarkBase
{
	private List<Pos2d> mBodyRange;

	public override MiniMapMarkDataType DataType => MiniMapMarkDataType.Entity;

	public override bool EntityActive => false;

	public override List<Pos2d> BodyRange
	{
		get
		{
			List<Pos2d> list = mBodyRange;
			if (list == null)
			{
				List<Pos2d> list2 = new List<Pos2d> { GridPos };
				List<Pos2d> list3 = list2;
				mBodyRange = list2;
				list = list3;
			}
			return list;
		}
	}

	public override int GetEntityState()
	{
		return 0;
	}

	public override string GetMarkNameKey()
	{
		return null;
	}

	public override MiniMapMarkTextKeyArg GetMarkDescTextArgKey()
	{
		return default;
	}

	public override string GetDialogIconPrefabPath()
	{
		return null;
	}

	public override void InitDialogIconHandler(IMapEntityDialogIconHandler[] handlers)
	{
	}

	public override bool InteractPossible()
	{
		return true;
	}

	public override void OnClick()
	{
	}
}

}
