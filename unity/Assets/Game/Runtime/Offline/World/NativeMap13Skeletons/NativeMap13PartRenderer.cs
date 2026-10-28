using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    // Keeps the original empty serialized parts-renderer component's identity.
    // Mesh generation is owned by the independent native geometry view.
    public sealed class NativeMap13PartRenderer : MonoBehaviour
    {
        public string SourceKey;
    }
}
