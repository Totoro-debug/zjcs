using System;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
[Serializable]
public class WorldCameraBaseSetting : CameraBaseSetting
{
	[Tooltip("默认视野宽度格子数大小")]
	public float DefaultGridViewSize = 7f;
}
}
