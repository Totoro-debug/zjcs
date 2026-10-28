using Cinemachine;
using UnityEngine;
namespace Zjcs.Official.World.NativeCamera
{
public static class CameraUtils
{
public static Bounds GetOrthographicCameraViewBounds(Camera camera)
    {
        if (!camera.orthographic) { Debug.LogError("The camera is not orthographic:" + camera.name + " scene:" + camera.scene.name); return default; }
        return GetOrthographicCameraViewBounds(camera.transform.position, camera.orthographicSize, camera.aspect);
    }
	public static Bounds GetOrthographicCameraViewBounds(CinemachineVirtualCamera camera)
	{
		return GetOrthographicCameraViewBounds(((Component)camera).transform.position, camera.m_Lens.OrthographicSize, camera.m_Lens.Aspect);
	}
	public static Bounds GetOrthographicCameraViewBounds(Vector3 center, float orthographicSize, float aspect)
	{
		float num = orthographicSize * 2f;
		float num2 = num * aspect;
		Bounds result = default;
		result.center = center;
		result.size = CameraVectorCast.Convert(new Vector2(num2, num));
		return result;
	}
	public static Camera GetMainCamera()
	{
		Camera[] allCameras = Camera.allCameras;
		foreach (Camera val in allCameras)
		{
			if (((Behaviour)val).enabled && ((Component)val).gameObject.activeInHierarchy && ((Component)val).gameObject.CompareTag("MainCamera"))
			{
				return val;
			}
		}
		return null;
	}
	public static Vector2 GetOrthographicCameraScreenScale(Camera camera)
	{
		Bounds orthographicCameraViewBounds = GetOrthographicCameraViewBounds(camera);
		return new Vector2(orthographicCameraViewBounds.size.x / (float)Screen.width, orthographicCameraViewBounds.size.y / (float)Screen.height);
	}
}
}
