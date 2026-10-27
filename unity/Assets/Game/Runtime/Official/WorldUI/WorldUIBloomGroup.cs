using UnityEngine;
using UnityEngine.UI;
namespace Zjcs.Official.WorldUI
{
    // client-source/UIBloomGroup.cs / UIBloomGroupBase.cs; renderer consumes this metadata.
    [DisallowMultipleComponent]
    public sealed class WorldUIBloomGroup : MonoBehaviour
    {
        public Image[] Masks;
        public bool IsOverrideMask;
        public int OccluderCount;
        private int mStartStencilId = -1;
        public int StartStencilId => mStartStencilId;
    }
}
