using System;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public interface ICameraController
{
	Action<Vector3> OnPosUpdated { set; }
	Action<float> OnViewSizeUpdated { set; }
	Vector3 GetViewPos();
	void SetViewPos(Vector3 pos, bool immediately = false);
	float GetViewSize();
	void SetViewSize(float viewSize, bool isZoom);
}
}
