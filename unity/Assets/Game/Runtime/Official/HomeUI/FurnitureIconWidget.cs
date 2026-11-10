using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeUI
{
    // Official FurnitureIconWidget.cs; retain the original serialized state switch.
    public sealed class FurnitureIconWidget : MonoBehaviour
    {
        [SerializeField] private WorldStateSwitcher<bool> mRequiredStateSwitch;
        public void SetState(bool isRequired) => mRequiredStateSwitch.SetState(isRequired);
    }
}
