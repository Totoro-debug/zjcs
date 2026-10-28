using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public class WorldCameraBaseController : CameraBaseController
{
	public IMapRootManagerProxy MapRootManagerProxy { get; set; }
	protected override float GetDefaultViewSize()
	{
		float orthographicSize = ((WorldCameraBaseSetting)mBaseSetting).DefaultGridViewSize / 2f / (float)Screen.width * (float)Screen.height;
		return mMonoProxy.ResolutionAdaptor.CalcAdaptOrthographicSize(orthographicSize);
	}
	public override void SetViewPos(Vector3 pos, bool immediately)
	{
		if (CameraControlUtils.IsMapMoveImmediately(pos, GetViewPos()))
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
	protected override Vector3 ClampPos(Vector3 pos)
	{
		pos.z = 0f;
		return pos;
	}
}
}
