namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public interface IAppearancePart
{
	GameObject GameObject { get; }

	IProfessionAppearancePartFollower SkeletonFollower { get; }

	void Init(GameObject skeletonGo);
}

}
