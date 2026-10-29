using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
[RequireComponent(typeof(MiniMapMarkUIBase))]
public abstract class MiniMapMarkSupportBase : MonoBehaviour
{
	public abstract void Init(MiniMapMarkUIBase markUi);

	public abstract void Release(MiniMapMarkUIBase markUi);
}

}
