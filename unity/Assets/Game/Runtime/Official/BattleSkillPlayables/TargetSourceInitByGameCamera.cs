using UnityEngine;
using Zjcs.Official.StoryPlayables;

public class TargetSourceInitByGameCamera : TargetSourceOnInit
{
	[SerializeField]
	private bool mIsSource;

	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		SetMyTargetSource(playable);
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		SetMyTargetSource(playable);
	}

	private void SetMyTargetSource(MyPlayable playable)
	{
		Camera mainCamera = GetMainCamera();
		if (mainCamera != null)
			MyPlayableTargetSourceUtils.SetPlayableTargetSourceGo(playable, mainCamera.gameObject, mIsSource);
	}

	private static Camera GetMainCamera()
	{
		foreach (Camera camera in Camera.allCameras)
		{
			if (camera.enabled && camera.gameObject.activeInHierarchy && camera.gameObject.CompareTag("MainCamera"))
				return camera;
		}
		return null;
	}
}
