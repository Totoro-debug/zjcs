using System;
using Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.HomeCamera
{
[Serializable]
public class OrthographicCameraExtendAdaptor
{
	[SerializeField]
	private Vector2 mBaseAspectRatio;
	[SerializeField]
	private float mBaseSize;
	[SerializeField]
	protected CinemachineVirtualCamera mCamera;
	public float ResolutionAdapt()
	{
		if ((Object)(object)mCamera == (Object)null)
		{
			return mBaseSize;
		}
		float num = mBaseAspectRatio.x / mBaseAspectRatio.y;
		float num2 = 1f * (float)Screen.width / (float)Screen.height;
		float num3 = 0f;
		num3 = ((!(num2 < num)) ? mBaseSize : (mBaseSize * num / num2));
		mCamera.m_Lens.OrthographicSize = num3;
		return num3;
	}
}
}
