// Migrated complete original application class; see entity transform receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using AttachAgent = Zjcs.Official.StoryAttachment.AttachAgent;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using UnityEngine;
namespace Zjcs.Official.EntityTransformViews
{
public static class EntityDirectionRotation
{
public static Quaternion DirToRotation(this Direction dir)
	{
		switch (dir)
		{
		case Direction.Up:
			return Quaternion.Euler(0f, 0f, 0f);
		case Direction.Left:
			return Quaternion.Euler(0f, 0f, 90f);
		case Direction.Down:
			return Quaternion.Euler(0f, 0f, 180f);
		case Direction.Right:
			return Quaternion.Euler(0f, 0f, 270f);
		default:
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return Quaternion.Euler(0f, 0f, 0f);
		}
		}
	}
}

}
