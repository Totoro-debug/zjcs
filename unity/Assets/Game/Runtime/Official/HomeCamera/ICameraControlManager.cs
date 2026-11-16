using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.HomeCamera
{
public interface ICameraControlManager
{
	CameraControlBrain ControlBrain { get; }
	SafeAction<Vector3> ViewPosUpdated { get; }
	SafeAction<float> ViewSizeUpdated { get; }
	void Init(CameraControlBrain cameraControlBrain);
}
}
