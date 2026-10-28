namespace Zjcs.Official.World.NativeCamera
{
public interface ICameraControllerDriven : ICameraController
{
	void Init(CameraMonoProxy monoProxy);
	void DriveEnter(bool immediately);
	void DriveExit();
	void DriveUpdate();
	void DriveLateUpdate();
}
}
