using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.Startup
{
    public sealed class AgeTipsDialog : MonoBehaviour
    {
        public Button Button;
        private void Awake() => Button.onClick.AddListener(OnClickBtn);
        // Original WindowManager.Close route, with local instance ownership.
        private void OnClickBtn() => Destroy(gameObject);
    }
}
