using Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.HomeCamera
{
public class HomeCameraIniter : MonoBehaviour
{
	public CameraControlBrain ControlBrain;
	public IgnoreUIDragController DragController;
	public CinemachineVirtualCamera VirtualCamera;
	public OrthographicCameraExtendAdaptor Adaptor;
	private void Awake()
	{
		Adaptor.ResolutionAdapt();
		ControlBrain.Init(null);
		ControlBrain.SetController<HomeCameraController>(immediately: true);
		float num = 1f * (float)Screen.width / (float)Screen.height;
		Bounds orthographicCameraViewBounds = CameraUtils.GetOrthographicCameraViewBounds(((Component)VirtualCamera).transform.position, VirtualCamera.m_Lens.OrthographicSize, num);
		DragController.ScreenOffsetScale = CameraUtils.GetOrthographicCameraScreenScale(orthographicCameraViewBounds);
		if (121f / 160f < num)
		{
			float num2 = (num - 121f / 160f) * VirtualCamera.m_Lens.OrthographicSize;
			Bounds borderBounds = DragController.Data.BorderBounds;
			Vector3 extents = borderBounds.extents;
			borderBounds.extents = new Vector3(extents.x + num2, extents.y, extents.z);
			DragController.SetBorderBounds(borderBounds);
		}
		DragController.Data.SetTargetBounds(orthographicCameraViewBounds);
	}
}
}
