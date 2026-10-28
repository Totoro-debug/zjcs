using System;
using Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
[Serializable]
public class CameraMonoProxy
{
	public CinemachineVirtualCamera VirCamera;
	public Transform CameraFollow;
	public IgnoreUIDragController DragController;
	public OrthographicCameraResolutionAdaptor ResolutionAdaptor;
	public CameraControlEnv CameraControlEnv;
	[NonSerialized]
	public Transform Target;
	[NonSerialized]
	public CinemachineBrain CinemachineBrain;
	public CameraBaseSetting GetCameraBaseSetting(CameraControlSettings cameraControlSettings)
	{
		return cameraControlSettings.GetBaseSettings(CameraControlEnv);
	}
}
}
