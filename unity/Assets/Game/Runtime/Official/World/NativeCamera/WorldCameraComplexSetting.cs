using System;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
[Serializable]
public class WorldCameraComplexSetting
{
	[Tooltip("相机跟随阻尼")]
	public Vector2 FollowDamping;
	[Tooltip("视野可移动范围内边距")]
	public Vector2 ViewMoveRangePadding = new Vector2(2f, 6f);
	[Tooltip("超出边界的弹性空间")]
	public Vector2 ElasticitySpace = new Vector2(2f, 2f);
	[Tooltip("视野安全区Y轴内间距")]
	public Vector2 ViewSafeAreaPaddingY = new Vector2(1f, 2f);
	[Tooltip("是否允许视野中丢失目标对象")]
	public bool AllowViewMissTarget;
	[Tooltip("视野追赶速度（当视野中心落后于玩家时向玩家追赶）")]
	public Vector2 ViewChaseSpeed = new Vector2(2f, 2f);
	public WorldCameraComplexSetting()
	{
	}
}
}
