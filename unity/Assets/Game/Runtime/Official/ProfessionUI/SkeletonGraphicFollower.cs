namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

[AddComponentMenu("Spine/SkeletonGraphicFollower(骨骼跟随)")]
public class SkeletonGraphicFollower : BaseSkeletonFollower
{
	public Component SkeletonGraphic;

    private IProfessionFollowerGraphic Target => (IProfessionFollowerGraphic)SkeletonGraphic;

	public override void InitFollowTarget(GameObject target)
	{
		SkeletonGraphic = (Component)target.GetComponent<IProfessionFollowerGraphic>();
	}

	protected override bool InitComponent(ref IProfessionFollowerAnimation skeletonAnimation, ref Transform skeletonTransform, ref bool useCustomZ)
	{
		Component skeletonGraphic = SkeletonGraphic;
		if ((Object)(object)skeletonGraphic == (Object)null || ((IProfessionFollowerGraphic)skeletonGraphic).Skeleton == null)
		{
			return false;
		}
		skeletonAnimation = (IProfessionFollowerAnimation)(object)skeletonGraphic;
		skeletonTransform = ((Component)skeletonGraphic).transform;
		useCustomZ = ((IProfessionFollowerGraphic)skeletonGraphic).UseCustomZSpacing;
		return true;
	}

	public override void Clear()
	{
		SkeletonGraphic = null;
		base.Clear();
	}

	protected override bool UpdateVisible()
	{

		if ((int)Target.UpdateWhenInvisible != 3)
		{
			return Target.UpdateMode != Target.UpdateWhenInvisible;
		}
		return true;
	}

	protected override float GetZSpacing()
	{
		if ((Object)(object)SkeletonGraphic != (Object)null)
		{
			return Target.ZSpacing * 100f;
		}
		return base.GetZSpacing();
	}
}

}
