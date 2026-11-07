using System;
using TMPro;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    /// <summary>The original ConfirmDialog prefab, mask, animations and sound bindings.
    /// Only the local resource lookup replaces WindowManager.Popup.</summary>
    internal sealed class OfficialBattleLeaveDialog : IDisposable
    {
        private GameObject root, mask;
        private AudioSource audio;
        private bool confirming, detached;
        public bool IsOpen => root != null;

        public void Show(Transform parent, TMP_FontAsset font, string key, Action confirmed)
        {
            if (IsOpen) return;
            var staging = new GameObject("BattleLeaveStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false);
            staging.SetActive(false);
            try
            {
                var assets = OfficialBattleLeaveAssetCatalog.LoadDefault();
                root = UnityEngine.Object.Instantiate(assets.DialogPrefab,
                    staging.transform, false);
                root.name = "OfficialBattleLeaveConfirmDialog";
                foreach (WorldMultiLanguageText text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    text.Localizer = assets.Localize;
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
                foreach (WorldButtonSound sound in root.GetComponentsInChildren<WorldButtonSound>(true))
                    sound.Post = PostSound;
                foreach (WorldPopupWindowSound sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true))
                    sound.Post = PostSound;
                var dialog = root.GetComponent<WorldConfirmDialog>();
                var popup = root.GetComponent<WorldPopupWindow>();
                dialog.CloseWindow = Close;
                popup.SetMaskClickAction(Close);
                dialog.Init(key, () =>
                {
                    confirming = true;
                    try { confirmed(); }
                    finally
                    {
                        confirming = false;
                        // The original callback precedes CloseThis. A mode may destroy
                        // its battle synchronously; the separate native window still hides.
                        if (detached) Close();
                    }
                });
                root.transform.SetParent(parent, false);
                root.SetActive(true);
                int order = 0;
                foreach (Canvas canvas in parent.GetComponentsInChildren<Canvas>())
                    if (!canvas.transform.IsChildOf(root.transform)) order = Math.Max(order, canvas.sortingOrder);
                popup.SetCanvasSortingOrder(order + 10);
                mask = UnityEngine.Object.Instantiate(assets.MaskPrefab,
                    parent, false);
                mask.name = "OfficialBattleLeaveInputMask";
                mask.GetComponent<WorldWindowMask>().AttachToWindow(popup);
                popup.Show();
            }
            catch { Dispose(); throw; }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        private void PostSound(AudioClip clip, GameObject owner)
        {
            if (clip == null || root == null) return;
            if (audio == null)
            {
                audio = root.transform.parent.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.loop = false;
                audio.spatialBlend = 0f;
            }
            audio.PlayOneShot(clip);
        }

        private void Close()
        {
            if (root == null) return;
            var closing = root;
            var closingMask = mask;
            closing.GetComponent<WorldPopupWindow>().Hide(() =>
            {
                if (root == closing)
                {
                    root = null;
                    mask = null;
                    detached = false;
                    ReleaseAudio();
                }
                if (closingMask != null) UnityEngine.Object.Destroy(closingMask);
                if (closing != null) UnityEngine.Object.Destroy(closing);
            });
        }

        public void Dispose()
        {
            if (confirming || detached) { detached = true; return; }
            if (root != null) UnityEngine.Object.Destroy(root);
            if (mask != null) UnityEngine.Object.Destroy(mask);
            root = mask = null;
            ReleaseAudio();
        }

        private void ReleaseAudio()
        {
            if (audio != null)
            {
                UnityEngine.Object.Destroy(audio);
            }
            audio = null;
        }
    }
}
