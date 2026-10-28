// Migrated official application/framework source; see bone-following source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using System;

namespace Zjcs.Official.StoryFollowBone
{
[Flags]
public enum TransformSyncType
{
	None = 0,
	Scale = 1,
	Rotation = 2,
	Position = 4,
	All = Scale | Rotation | Position
}

}
