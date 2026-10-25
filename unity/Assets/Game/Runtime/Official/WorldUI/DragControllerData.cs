// Ported from the pinned official DragControllerData.cs; Windows uses local component lifecycle.
namespace Zjcs.Official.WorldUI
{
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public class DragControllerData : ISerializationCallbackReceiver
{
	public enum UpdateType
	{
		LateUpdate,
		ManualUpdate
	}

	public enum DragUpdateType
	{
		OnDragUpdate,
		ManualUpdate
	}

	[Tooltip("启用水平滑动")]
	public bool Horizontal = true;

	[Tooltip("启用垂直滑动")]
	public bool Vertical = true;

	[Tooltip("移动类型")]
	public MovementType MyMovementType = MovementType.Elastic;

	[Tooltip("滑动敏感度，1代表与屏幕滑动完全同步")]
	[Range(0f, 2f)]
	public float Sensitive = 1f;

	[Tooltip("弹性值")]
	public float Elasticity = 0.1f;

	[Tooltip("超出可拖动范围的溢出系数")]
	public float RubberOverFactor = 0.55f;

	[Tooltip("启用惯性")]
	public bool Inertia = true;

	[Tooltip("惯性减速速率")]
	public float DecelerationRate = 0.135f;

	[Tooltip("拖拽对象")]
	public Transform Target;

	public Bounds TargetBounds;

	public bool UseMultipleRectangleBorders;

	public Bounds[] RectangleBounds;

	[Tooltip("边界大小信息,启用惯性时可超出")]
	public Bounds BorderBounds;

	[Tooltip("启用弹性时可用，允许超出边界的大小")]
	public Vector2 LimitSize;

	public UpdateType UpdateMode;

	public DragUpdateType DragUpdateMode;

	public Vector2 ContentPosLimitX { get; set; }

	public Vector2 ContentPosLimitY { get; set; }

	public bool IsContentPosLimit { get; set; }

	private void InitParameter()
	{

		IsContentPosLimit = LimitSize != Vector2.zero;
		if (IsContentPosLimit)
		{
			ContentPosLimitX = new Vector2(BorderBounds.min.x - LimitSize.x + TargetBounds.extents.x, BorderBounds.max.x + LimitSize.x - TargetBounds.extents.x);
			ContentPosLimitY = new Vector2(BorderBounds.min.y - LimitSize.y + TargetBounds.extents.y, BorderBounds.max.y + LimitSize.y - TargetBounds.extents.y);
		}
	}

	public void SetTargetBounds(Bounds bounds)
	{

		TargetBounds = bounds;
		UpdateBounds();
		InitParameter();
	}

	public void UpdateBounds()
	{

		if ((Object)(object)Target == (Object)null)
		{
			TargetBounds = default;
		}
		TargetBounds.center = Target.position;
		if (UseMultipleRectangleBorders)
		{
			UpdateBorderBounds();
		}
	}

	public void SetCustomLimit(Vector2 limitX, Vector2 limitY)
	{

		IsContentPosLimit = true;
		ContentPosLimitX = limitX;
		ContentPosLimitY = limitY;
	}

	public void UpdateBorderBounds()
	{

		if (RectangleBounds.Length == 1)
		{
			BorderBounds = RectangleBounds[0];
			return;
		}
		Vector3 val = (Vector3)new Vector2(float.MaxValue, float.MaxValue);
		Vector3 val2 = (Vector3)new Vector2(float.MinValue, float.MinValue);
		Vector3 position = Target.position;
		position.z = 0f;
		bool flag = false;
		int i = 0;
		for (int num = RectangleBounds.Length; i < num; i++)
		{
			Bounds val3 = RectangleBounds[i];
			if (val3.Contains(position))
			{
				Vector3 min = val3.min;
				Vector3 max = val3.max;
				if (min.x <= val.x)
				{
					val.x = min.x;
				}
				if (min.y <= val.y)
				{
					val.y = min.y;
				}
				if (max.x >= val2.x)
				{
					val2.x = max.x;
				}
				if (max.y >= val2.y)
				{
					val2.y = max.y;
				}
				flag = true;
			}
		}
		if (flag)
		{
			BorderBounds.SetMinMax(val, val2);
			InitParameter();
		}
	}

	public void OnBeforeSerialize()
	{
	}

	public void OnAfterDeserialize()
	{
		InitParameter();
	}

	public Vector3 ClampPos(Vector3 pos)
	{

		Bounds borderBounds = BorderBounds;
		Bounds targetBounds = TargetBounds;
		Vector3 val = borderBounds.min + targetBounds.extents;
		Vector3 val2 = borderBounds.max - targetBounds.extents;
		float num = Mathf.Clamp(pos.x, val.x, val2.x);
		float num2 = Mathf.Clamp(pos.y, val.y, val2.y);
		float num3 = Mathf.Clamp(pos.z, val.z, val2.z);
		return new Vector3(num, num2, num3);
	}

	public void OnDrawGizmos()
	{

		if (UseMultipleRectangleBorders)
		{
			float num = 1f / (float)RectangleBounds.Length;
			int i = 0;
			for (int num2 = RectangleBounds.Length; i < num2; i++)
			{
				float num3 = num * (float)(i + 1);
				Gizmos.color = new Color(1f, 1f, 1f - num3);
				Bounds val = RectangleBounds[i];
				Gizmos.DrawWireCube(val.center, val.size);
			}
		}
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireCube(BorderBounds.center, BorderBounds.size);
		Gizmos.color = Color.white;
		Gizmos.DrawWireCube(TargetBounds.center, TargetBounds.size);
		if (IsContentPosLimit)
		{
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireCube(BorderBounds.center, TargetBounds.size + new Vector3(ContentPosLimitX.y - ContentPosLimitX.x, ContentPosLimitY.y - ContentPosLimitY.x, 0f));
		}
	}
}

}
