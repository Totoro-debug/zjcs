// Migrated original grid presentation/query source; see Map13 grid-view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using UnityEngine;


namespace Zjcs.Official.World.Map13EntityGridViews
{
public static class ZSetUtils
{
	public const float kYToZ = 1f;

	public const float kXToZ = 0.001f;

	public static float YToZ(float y)
	{
		return y * 1f;
	}

	public static float XYToZ(float x, float y)
	{
		return x * 0.001f + y * 1f;
	}

	public static void SetLocalZ(Transform transform)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		float z = YToZ(transform.localPosition.y);
		SetLocalZ(transform, z);
	}

	public static void SetLocalZ(Transform transform, float z)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		Vector3 localPosition = transform.localPosition;
		localPosition.z = z;
		transform.localPosition = localPosition;
	}

	public static void SetWorldZ(Transform transform)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		float z = YToZ(transform.position.y);
		SetWorldZ(transform, z);
	}

	public static void SetWorldZ(Transform transform, float z)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = transform.position;
		position.z = z;
		transform.position = position;
	}
}

}
