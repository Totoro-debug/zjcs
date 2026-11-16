using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.HomeCamera
{
public class CameraControlSettings : ScriptableObject
{
	public CameraStorySettings StorySettings;
	public CameraDanceSettings DanceSettings;
	public WorldCameraControlSetting WorldCameraControlSetting;
	public HomeCameraControlSetting HomeCameraControlSetting;
	public GameCameraControlSetting1 GameCameraControlSetting;
	public CameraBaseSetting GetBaseSettings(CameraControlEnv cameraControlEnv)
	{
		switch (cameraControlEnv)
		{
		case CameraControlEnv.Home:
			return HomeCameraControlSetting.BaseSetting;
		case CameraControlEnv.Map:
			return WorldCameraControlSetting.BaseSetting;
		case CameraControlEnv.Game:
			return GameCameraControlSetting.BaseSetting;
		default:
		{
			Debug.LogError(cameraControlEnv + " not supported");
			return null;
		}
		}
	}
}
}
