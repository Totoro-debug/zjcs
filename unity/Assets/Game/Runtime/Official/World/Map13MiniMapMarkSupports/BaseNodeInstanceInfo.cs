namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
public class BaseNodeInstanceInfo : INodeInstanceInfo
{
	public static readonly BaseNodeInstanceInfo kDefault = new BaseNodeInstanceInfo();

	public virtual string GetInstanceDesc()
	{
		return "Base";
	}

	public override bool Equals(object obj)
	{
		return obj is BaseNodeInstanceInfo;
	}

	public override int GetHashCode()
	{
		return typeof(BaseNodeInstanceInfo).GetHashCode();
	}
}

}
