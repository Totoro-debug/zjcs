using System.Collections.Generic;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeLabelUI;
using TabItem=Zjcs.Official.WorldUI.WorldTabItem;
using IGameObjectPoolItem=Zjcs.Official.ProfessionUI.IGameObjectPoolItem;
using Zjcs.Official.ProfessionUI;
public class MiniMapMarkUIFilterSetting
{
	public enum SelectionType
	{
		None,
		All,
		Free
	}

	public int Id { get; set; }

	public Dictionary<string, bool> FilterState { get; set; } = new Dictionary<string, bool>();

	public SelectionType SelectionState { get; set; }
}

}
