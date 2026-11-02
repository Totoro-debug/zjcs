using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Offline.ProfessionUI
{
    [DisallowMultipleComponent]
    public sealed class ProfessionNativeScenePartOrder : MonoBehaviour
    {
        private SortingGroup group;
        internal ProfessionNativeSceneRenderer Owner;
        internal SortingGroup Group => group ? group : group = gameObject.AddComponent<SortingGroup>();
    }
}
