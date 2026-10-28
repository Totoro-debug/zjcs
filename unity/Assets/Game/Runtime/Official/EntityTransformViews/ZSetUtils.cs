// Migrated complete original application class; see entity transform receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using AttachAgent = Zjcs.Official.StoryAttachment.AttachAgent;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using UnityEngine;

namespace Zjcs.Official.EntityTransformViews
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
		float z = YToZ(transform.localPosition.y);
		SetLocalZ(transform, z);
	}

	public static void SetLocalZ(Transform transform, float z)
	{
		Vector3 localPosition = transform.localPosition;
		localPosition.z = z;
		transform.localPosition = localPosition;
	}

	public static void SetWorldZ(Transform transform)
	{
		float z = YToZ(transform.position.y);
		SetWorldZ(transform, z);
	}

	public static void SetWorldZ(Transform transform, float z)
	{
		Vector3 position = transform.position;
		position.z = z;
		transform.position = position;
	}
}

}
