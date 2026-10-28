namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public class AppearanceEffectPart : MonoBehaviour, IAppearancePart
{
	public BaseSkeletonFollower Follower;

	public GameObject GameObject => ((Component)this).gameObject;

	public IProfessionAppearancePartFollower SkeletonFollower => Follower;

	public virtual void Init(GameObject skeletonGo)
	{
		Follower.InitFollowTarget(skeletonGo);
		Follower.ForceUpdate();
	}
}

}
