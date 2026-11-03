using Spine.Unity;
using UnityEngine;

public interface IAfterimageRTParamsProvider
{
	int RTWidth { get; }
	int RTHeight { get; }
	float OrthoSize { get; }
	Vector3 GetTargetPos(SkeletonRenderer skeletonRenderer);
}
