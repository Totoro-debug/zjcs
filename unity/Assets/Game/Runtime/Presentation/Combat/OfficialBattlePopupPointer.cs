using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Presentation.Combat
{
    // Routes the original global-pointer dropdown dismissal through EventSystem input.
    public sealed class OfficialBattlePopupPointer : MonoBehaviour, IPointerDownHandler
    {
        public Action<Vector2> PointerDown;
        public void OnPointerDown(PointerEventData data) => PointerDown?.Invoke(data.position);
    }
}
