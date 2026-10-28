namespace Zjcs.Official.World.NativeCamera
{
public interface ICameraControlManager
{
	CameraControlBrain ControlBrain { get; }
	void Init(CameraControlBrain cameraControlBrain);
}
}
