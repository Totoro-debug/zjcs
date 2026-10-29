using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
public class MinimapMarkTipDotLinkerSupport : MiniMapMarkSupportBase
{
	[SerializeField]
	private MapEntityTipDotNodeLinker mTipdotLinker;

	public override void Init(MiniMapMarkUIBase markUi)
	{
		if (!((IMiniMapMarkSupportContext)markUi.MiniMap).IsLeader || !(markUi.Mark is MiniMapEntityMark miniMapEntityMark))
		{
			return;
		}
		EcEntityIdentifier id = ((IMiniMapSpawnEntity)miniMapEntityMark.Entity).Id;
		if ((Object)(object)mTipdotLinker == (Object)null)
		{
			Debug.LogError("小地图Mark[" + name + "]上的组件MinimapMarkTipDotLinkerSupport没挂载物件红点，请挂载或者删除此脚本");
		}
		else
		{
			mTipdotLinker.RegisterEntity(id);
		}
	}

	public override void Release(MiniMapMarkUIBase markUi)
	{
		if ((Object)(object)mTipdotLinker != (Object)null)
		{
			mTipdotLinker.UnregisterEntity();
		}
	}
}

}
