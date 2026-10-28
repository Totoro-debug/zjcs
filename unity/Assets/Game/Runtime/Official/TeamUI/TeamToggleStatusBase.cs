using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.TeamUI
{
    // Original UIToggleStatusBase.
    public abstract class TeamToggleStatusBase : MonoBehaviour
    {
        protected Toggle myToggle;
        private void Awake()
        {
            myToggle = GetComponentInParent<Toggle>();
            if (myToggle != null) myToggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
        private void OnEnable() { if (myToggle != null) PlayEffect(myToggle.isOn, true); }
        private void OnToggleValueChanged(bool value) { PlayEffect(value); }
        public abstract void PlayEffect(bool value, bool instant = false);
    }
}
