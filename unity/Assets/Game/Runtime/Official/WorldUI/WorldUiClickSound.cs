using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Official.WorldUI
{
    // framework-firstpass-source/UIClickSound.cs, with its original left-button filter.
    public sealed class WorldUiClickSound : MonoBehaviour, IPointerClickHandler
    {
        public AudioClip AkEvent;
        [NonSerialized] public Action<AudioClip, GameObject> Post;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Post?.Invoke(AkEvent, gameObject);
        }
    }
}
