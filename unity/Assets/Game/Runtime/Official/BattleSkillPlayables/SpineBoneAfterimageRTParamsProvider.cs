using Spine;
using Spine.Unity;
using UnityEngine;

public class SpineBoneAfterimageRTParamsProvider : MonoBehaviour, IAfterimageRTParamsProvider
{
	[SerializeField] private int mRTWidth;
	[SerializeField] private int mRTHeight;
	[SerializeField] private int mOrthoSize;
	[SerializeField] private string mBoneName;
	[SerializeField] private Vector3 mOffset;

	public int RTWidth => mRTWidth;
	public int RTHeight => mRTHeight;
	public float OrthoSize => mOrthoSize;

	public Vector3 GetTargetPos(SkeletonRenderer skeletonRenderer)
	{
		Bone bone = skeletonRenderer.Skeleton.FindBone(mBoneName);
		return skeletonRenderer.transform.TransformPoint(new Vector3(0f, bone.WorldY)) + mOffset;
	}
}
