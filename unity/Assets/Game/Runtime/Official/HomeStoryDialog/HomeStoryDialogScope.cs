using UnityEngine;

namespace Zjcs.Official.HomeStoryDialog
{
    // The parent supplies its local scope before the original prefab is instantiated.
    public sealed class HomeStoryDialogScope : MonoBehaviour
    {
        public IStoryDialogServices Services { get; set; }
    }
}
