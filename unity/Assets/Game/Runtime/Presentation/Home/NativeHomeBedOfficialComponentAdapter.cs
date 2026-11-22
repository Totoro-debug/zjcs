using UnityEngine;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Local MonoBehaviour target for the original 903 Bed prefab's
    /// Assembly-CSharp components. The source prefab is kept intact under
    /// Official/Home/Native903; this adapter only replaces the unavailable
    /// plugin assembly references in the Resources copy.
    ///
    /// The native visual hierarchy and serialized resource references remain
    /// official. Furniture state, repair story, reward timing, red dots and
    /// auxiliary HUD behaviour are still driven by the offline home runtime
    /// and are not claimed complete by this placeholder lifecycle component.
    /// </summary>
    public sealed class NativeHomeBedOfficialComponentAdapter : MonoBehaviour
    {
    }
}
