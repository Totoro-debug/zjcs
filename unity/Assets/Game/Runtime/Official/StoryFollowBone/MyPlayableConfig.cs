// Migrated official application/framework source; see bone-following source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using UnityEngine;
namespace Zjcs.Official.StoryFollowBone
{
public class MyPlayableConfig : ScriptableObject
{
	[Tooltip("MyPlayableFollowBone组件中跟随格子大小对应的缩放大小")]
	public MonoDictionary<int, float> FollowBoneGridSizeToSize;
}

}
