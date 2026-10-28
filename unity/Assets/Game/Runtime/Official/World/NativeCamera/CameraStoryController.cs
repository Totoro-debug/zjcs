using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public class CameraStoryController : CameraBaseController
{
	public IMapRootManagerProxy MapRootManagerProxy { get; set; }
	protected override void OnEnter()
	{
		base.OnEnter();
		((Behaviour)mMonoProxy.DragController).enabled = false;
		CameraStorySettings storySettings = mCameraControlSettings.Value.StorySettings;
		mSoftViewSizeMax = storySettings.StorySoftViewSizeMax;
		mZoomViewSizeMax = storySettings.StoryZoomViewSizeMax;
		DefaultViewSize = 0f;
	}
	protected override void OnExit()
	{
		base.OnExit();
		((Behaviour)mMonoProxy.DragController).enabled = true;
	}
	protected override float GetDefaultViewSize()
	{
		return mMonoProxy.VirCamera.m_Lens.OrthographicSize;
	}
	public override void SetViewPos(Vector3 pos, bool immediately)
	{
		if ((Object)(object)MapRootManagerProxy.MapRoot != (Object)null && CameraControlUtils.IsMapMoveImmediately(pos, GetViewPos()))
		{
			CameraAsync.Observe(MoveAbruptly(pos));
		}
		else
		{
			base.SetViewPos(pos, immediately);
		}
	}
	private async System.Threading.Tasks.Task MoveAbruptly(Vector3 pos)
	{
		await MapRootManagerProxy.PrepareInstantly(pos);
		base.SetViewPos(pos, immediately: true);
	}
}
}
