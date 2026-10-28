using Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
[ExecuteAlways]
public class OrthographicCameraResolutionAdaptor : MonoBehaviour
{
	protected enum AdaptPriority
	{
		Auto,
		Width,
		Height
	}
	[Header("Base")]
	[Tooltip("适配优先级")]
	[SerializeField]
	private AdaptPriority mAdapterPriority = AdaptPriority.Height;
	[SerializeField]
	private Bounds mySafeArea;
	[SerializeField]
	protected CinemachineVirtualCamera mCamera;
	private bool mHasOriginalOrthographicSize;
	private float mOriginalOrthographicSize;
	private void Awake()
	{
		ResolutionAdapt();
	}
	private void ResolutionAdapt()
	{
		if (!((Object)(object)mCamera == (Object)null))
		{
			float orthographicSize = mCamera.m_Lens.OrthographicSize;
			if (!mHasOriginalOrthographicSize)
			{
				mOriginalOrthographicSize = orthographicSize;
				mHasOriginalOrthographicSize = true;
			}
			float orthographicSize2 = AdaptOrthographicSize(orthographicSize, mAdapterPriority, mySafeArea);
			mCamera.m_Lens.OrthographicSize = orthographicSize2;
			if (Application.isPlaying)
			{
				OnApplyAdapt();
			}
		}
	}
	protected virtual void OnApplyAdapt()
	{
	}
	public float CalcAdaptOrthographicSize(float orthographicSize)
	{
		return AdaptOrthographicSize(orthographicSize, mAdapterPriority, mySafeArea);
	}
	public float GetOriginalOrthographicSize()
	{
		if (mHasOriginalOrthographicSize)
		{
			return mOriginalOrthographicSize;
		}
		return mCamera.m_Lens.OrthographicSize;
	}
	protected static float AdaptOrthographicSize(float orthographicSize, AdaptPriority adaptPriority, Bounds safeArea)
	{
		float num = 1f * (float)Screen.height / (float)Screen.width;
		orthographicSize = adaptPriority switch
		{
			AdaptPriority.Width => safeArea.extents.y,
			AdaptPriority.Height => safeArea.extents.x * num,
			_ => orthographicSize,
		};
		if (orthographicSize < safeArea.extents.y)
		{
			orthographicSize = safeArea.extents.y;
		}
		else if (orthographicSize / num < safeArea.extents.x)
		{
			orthographicSize = safeArea.extents.x * num;
		}
		return orthographicSize;
	}
}
}
