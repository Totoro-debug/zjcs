// Migrated original source; see map14-story-bubble-runtime source receipt.
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.StoryBubbles
{
[DisallowMultipleComponent]
public class UIBloomGroup : UIBloomGroupBase
{
	public Image[] Masks;

	public bool IsOverrideMask;

	public int OccluderCount;

	private int mStartStencilId = -1;

	public int StartStencilId => mStartStencilId;
}

}
