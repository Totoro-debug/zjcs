using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public class WorldCameraFollowController : WorldCameraBaseController
{
	protected override void OnEnter()
	{
		base.OnEnter();
		((Behaviour)mMonoProxy.DragController).enabled = false;
	}
	protected override void OnExit()
	{
		base.OnExit();
		((Behaviour)mMonoProxy.DragController).enabled = true;
	}
	protected override void OnUpdate()
	{
		base.OnUpdate();
		if (!((Object)(object)mTarget == (Object)null))
		{
			Vector3 position = ((Component)mTarget).transform.position;
			mMonoProxy.CameraFollow.position = position;
		}
	}
}
}
