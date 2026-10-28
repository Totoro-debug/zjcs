namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

[AddComponentMenu("Spine/SkeletonRendererFollower(骨骼跟随)")]
public class SkeletonRendererFollower : BaseSkeletonFollower
{
	public Component SkeletonRenderer;

    private IProfessionFollowerRenderer Target => (IProfessionFollowerRenderer)SkeletonRenderer;

	public override void InitFollowTarget(GameObject target)
	{
		SkeletonRenderer = (Component)target.GetComponent<IProfessionFollowerRenderer>();
	}

	protected override bool InitComponent(ref IProfessionFollowerAnimation skeletonAnimation, ref Transform skeletonTransform, ref bool useCustomZ)
	{

		Component skeletonRenderer = SkeletonRenderer;
		if ((Object)(object)skeletonRenderer == (Object)null || ((IProfessionFollowerRenderer)skeletonRenderer).Skeleton == null)
		{
			return false;
		}
		skeletonAnimation = (IProfessionFollowerAnimation)skeletonRenderer;
		skeletonTransform = ((Component)skeletonRenderer).transform;
		useCustomZ = ((IProfessionFollowerRenderer)skeletonRenderer).UseCustomZSpacing;
		return true;
	}

	public override void Clear()
	{
		SkeletonRenderer = null;
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
		if ((Object)(object)SkeletonRenderer != (Object)null)
		{
			return Target.ZSpacing;
		}
		return base.GetZSpacing();
	}
}

}
