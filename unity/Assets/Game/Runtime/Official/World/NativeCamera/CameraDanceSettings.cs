using System;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
[Serializable]
public class CameraDanceSettings
{
	[Tooltip("Dance 模式下视野软上限（拉高镜头时的常规上限）")]
	public float DanceSoftViewSizeMax = 30f;
	[Tooltip("Dance 模式下视野缩放硬上限")]
	public float DanceZoomViewSizeMax = 30f;
}
}
