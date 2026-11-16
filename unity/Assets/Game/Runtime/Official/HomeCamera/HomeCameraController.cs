using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.HomeCamera
{
public class HomeCameraController : CameraBaseController
{
	protected override float GetDefaultViewSize()
	{
		return mMonoProxy.VirCamera.m_Lens.OrthographicSize;
	}
	public void SetClampViewPosition(Vector3 pos, bool immediately = false)
	{
		pos = Clamp(pos);
		SetViewPos(pos, immediately);
		if (immediately)
		{
			UpdateCameraFrame();
		}
	}
	public void SetFocusPositionXForce(float posX, bool immediately = false)
	{
		Vector3 position = ((Component)mMonoProxy.CameraFollow).transform.position;
		position.x = posX;
		SetClampViewPosition(position, immediately);
	}
	public void SetFocusPositionX(float posX, float margin = 1f, bool immediately = false)
	{
		Bounds orthographicCameraViewBounds = CameraUtils.GetOrthographicCameraViewBounds(mMonoProxy.VirCamera);
		if (!(posX - margin >= orthographicCameraViewBounds.min.x) || !(posX + margin <= orthographicCameraViewBounds.max.x))
		{
			SetFocusPositionXForce(posX, immediately);
		}
	}
	private Vector3 Clamp(Vector3 pos)
	{
		float viewSize = GetViewSize();
		float aspect = mMonoProxy.VirCamera.m_Lens.Aspect;
		Bounds borderBounds = mMonoProxy.DragController.Data.BorderBounds;
		Vector3 val = new Vector3(viewSize * aspect, viewSize);
		Vector3 val2 = borderBounds.min + val;
		Vector3 val3 = borderBounds.max - val;
		float num = Mathf.Clamp(pos.x, val2.x, val3.x);
		float num2 = Mathf.Clamp(pos.y, val2.y, val3.y);
		float num3 = Mathf.Clamp(pos.z, val2.z, val3.z);
		return new Vector3(num, num2, num3);
	}
}
}
