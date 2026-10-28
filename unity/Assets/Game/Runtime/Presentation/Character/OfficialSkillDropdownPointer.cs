using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Character
{
    internal sealed class OfficialSkillDropdownPointer : MonoBehaviour, IPointerDownHandler
    {
        public WorldUIDropdown Dropdown;
        public void OnPointerDown(PointerEventData data) => Dropdown.OnGlobalPointerDown(data.position);
    }
}
