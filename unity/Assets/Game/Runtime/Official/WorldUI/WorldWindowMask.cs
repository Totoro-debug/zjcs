using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // Original WindowMask + WindowMaskBase. WindowMask.Prepare completes
    // immediately and its attach/unattach hooks are empty in the client.
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
    public sealed class WorldWindowMask : MonoBehaviour
    {
        [SerializeField] private int mMaskType;
        [SerializeField] private Button mBtn;
        [SerializeField] private Image mMaskImg;
        private Canvas mCanvase;
        private readonly List<WorldWindowMaskEffectBase> mMaskEffects = new List<WorldWindowMaskEffectBase>();
        public int MaskType => mMaskType;
        public Image MaskImg => mMaskImg;
        public WorldPopupWindow AttachWindow { get; private set; }
        public Canvas Canvas => mCanvase != null ? mCanvase : (mCanvase = GetComponent<Canvas>());
        public Button Button => mBtn;
        private void Start() => mBtn.onClick.AddListener(MaskClickHandler);
        private void MaskClickHandler()
        {
            if (AttachWindow != null) AttachWindow.OnMaskClick();
        }
        public void AttachToWindow(WorldPopupWindow window)
        {
            ApplyMaskEffect(window);
            AttachWindow = window;
            if (window == null) Debug.LogError("AttachToWindow -> window is null");
            else
            {
                Canvas.enabled = !AttachWindow.IsPause;
                Canvas.sortingOrder = AttachWindow.Canvas.sortingOrder - 1;
                gameObject.SetActive(true);
                Canvas.overrideSorting = true;
            }
        }
        public void UnattachToWindow()
        {
            AttachWindow = null;
            ClearMaskEffect();
            gameObject.SetActive(false);
        }
        public void Refresh() => Canvas.enabled = !AttachWindow.IsPause;
        private void ApplyMaskEffect(WorldPopupWindow window)
        {
            ClearMaskEffect();
            if (window == null) return;
            window.GetComponents(mMaskEffects);
            foreach (var effect in mMaskEffects) effect.ApplyEffect(this);
        }
        private void ClearMaskEffect()
        {
            foreach (var effect in mMaskEffects) effect.CancelEffect(this);
            mMaskEffects.Clear();
        }
    }
}
