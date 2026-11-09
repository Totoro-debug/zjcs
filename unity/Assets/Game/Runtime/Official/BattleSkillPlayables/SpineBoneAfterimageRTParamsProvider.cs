using UnityEngine;
using Zjcs.Official.ProfessionUI;

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

	public Vector3 GetTargetPos(IProfessionAfterimageTarget target)
		=> target.GetCapturePosition(mBoneName, mOffset);
}
