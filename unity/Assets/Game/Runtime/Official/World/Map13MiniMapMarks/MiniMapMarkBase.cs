using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public abstract class MiniMapMarkBase
{
	public abstract MiniMapMarkDataType DataType { get; }

	public int MapClassId { get; protected set; }

	public int Region { get; protected set; }

	public Pos2d GridPos { get; protected set; }

	public Vector3 LocalPosition { get; protected set; }

	public abstract bool EntityActive { get; }

	public abstract List<Pos2d> BodyRange { get; }

	public abstract int GetEntityState();

	public abstract string GetMarkNameKey();

	public abstract MiniMapMarkTextKeyArg GetMarkDescTextArgKey();

	public abstract string GetDialogIconPrefabPath();

	public abstract void InitDialogIconHandler(IMapEntityDialogIconHandler[] handlers);

	public abstract bool InteractPossible();

	public abstract void OnClick();
}

}
