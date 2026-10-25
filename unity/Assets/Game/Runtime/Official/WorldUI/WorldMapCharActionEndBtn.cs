using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    /// <summary>The original action-end button is hidden until an action provider is bound.</summary>
    public sealed class WorldMapCharActionEndBtn : MonoBehaviour
    {
        public Button mEndBtn;
        private void Awake() => SetButtonVisible(false);
        public void SetButtonVisible(bool visible) => mEndBtn.gameObject.SetActive(visible);
    }
}
