using System;
using UnityEngine;
namespace Zjcs.Official.WorldUI
{
    // framework-firstpass-source/PopupWindowSound.cs, with a local audio posting service.
    public sealed class WorldPopupWindowSound : MonoBehaviour
    {
        public AudioClip ShowSound, HideSound;
        [NonSerialized] public Action<AudioClip, GameObject> Post;
        private WorldPopupWindow mPopupWindow;
        public WorldPopupWindow PopupWindow => mPopupWindow != null ? mPopupWindow :
            (mPopupWindow = GetComponentInParent<WorldPopupWindow>());
        private void Awake() { PopupWindow.OnShowing += OnShowing; PopupWindow.OnHiding += OnHiding; }
        private void OnDestroy()
        {
            if (mPopupWindow == null) return;
            mPopupWindow.OnShowing -= OnShowing; mPopupWindow.OnHiding -= OnHiding;
        }
        public void OnShowing(WorldPopupWindow window) { if (ShowSound != null) Post?.Invoke(ShowSound, gameObject); }
        public void OnHiding(WorldPopupWindow window) { if (HideSound != null) Post?.Invoke(HideSound, gameObject); }
    }
}
