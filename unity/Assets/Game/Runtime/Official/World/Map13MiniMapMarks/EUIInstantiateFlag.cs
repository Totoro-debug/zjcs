namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
public enum EUIInstantiateFlag
{
	None = 0,
	Position = 1,
	Rotation = 2,
	Scale = 4,
	AutoActive = 8,
	Transform = 7,
	KeepScale = 11,
	All = 15
}

}
