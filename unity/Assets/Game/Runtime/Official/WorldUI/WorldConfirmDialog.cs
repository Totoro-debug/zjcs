using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // Original ConfirmDialog; CloseWindow replaces WindowManager.Close only.
    public sealed class WorldConfirmDialog : MonoBehaviour
    {
        public WorldMultiLanguageText TitleText, HintText, ConfirmText, CancelText;
        public WorldAutoAlignForSingleMultiLineText ContentText;
        public GameObject HintGo;
        public Button ConfirmBtn, CancelBtn;
        public WorldPopupWindow PopupWindow;
        public WorldLockMaskWithTime LockMaskWithTime;
        public float LockTime, CancelBtnCustomWidth;
        public LayoutElement CancelBtnLayoutElement;
        [NonSerialized] public Action CloseWindow;
        private Action callback, cancelCallback;
        private bool callbackExecuted;
        private bool callbackAutoExecute = true;
        private bool closeOnConfirm = true;

        private void Awake()
        {
            ConfirmBtn.onClick.AddListener(ConfirmBtnClickHandler);
            CancelBtn.onClick.AddListener(CancelBtnClickHandler);
            ContentText.OnTextRefresh += OnContentTextRefresh;
        }

        private void OnDisable()
        {
            if (!callbackExecuted && callbackAutoExecute)
            {
                callbackExecuted = true;
                cancelCallback?.Invoke();
            }
            SetCancelBtnShow(true);
            SetCloseByClickMask(true);
            closeOnConfirm = true;
        }

        private void OnDestroy() => ContentText.OnTextRefresh -= OnContentTextRefresh;

        public void Init(string contentTextKey, Action onConfirm, Action onCancel = null)
            => Init(contentTextKey, null, onConfirm, onCancel);

        public void Init(string contentTextKey, object[] args, Action onConfirm, Action onCancel = null)
        {
            ContentText.SetKey(contentTextKey, args);
            callback = onConfirm;
            cancelCallback = onCancel;
            callbackExecuted = false;
            closeOnConfirm = true;
            if (HintGo != null) HintGo.SetActive(false);
            TitleText.SetKey("confirmation");
            ConfirmText.SetKey("confirm");
            CancelText.SetKey("cancel");
        }

        public void SetCustomWidthBtn() => CancelBtnLayoutElement.preferredWidth = CancelBtnCustomWidth;
        public void SetTitleTextKey(string key) => TitleText.SetKey(key);
        public void SetTitleText(string key, params object[] args) => TitleText.SetKey(key, args);
        public void SetConfirmTextKey(string key) => ConfirmText.SetKey(key);
        public void SetCancelTextKey(string key) => CancelText.SetKey(key);
        public void SetCancelBtnShow(bool show) => CancelBtn.gameObject.SetActive(show);
        public void SetCloseByClickMask(bool close) => PopupWindow.CloseByClickMask = close;
        public void SetCloseOnConfirm(bool close) => closeOnConfirm = close;
        public void SetCancelCallbackAutoExecute(bool auto) => callbackAutoExecute = auto;
        public void SetHintTextKey(string key, params object[] args)
        {
            if (HintGo == null) return;
            HintText.SetKey(key, args);
            HintGo.SetActive(true);
        }

        public void SetConfirmBtnLock(float lockTime = -1f)
        {
            if (LockMaskWithTime != null) LockMaskWithTime.Init(lockTime);
        }

        private void CancelBtnClickHandler()
        {
            callbackExecuted = true;
            cancelCallback?.Invoke();
            CloseThis();
        }

        private void ConfirmBtnClickHandler()
        {
            callback?.Invoke();
            if (closeOnConfirm)
            {
                callbackExecuted = true;
                CloseThis();
            }
        }

        private void CloseThis()
        {
            if (CloseWindow != null) CloseWindow();
            else PopupWindow.Hide(null);
        }

        private void OnContentTextRefresh()
        {
            if (HintText == null || !HintText.gameObject.activeSelf) return;
            var hint = HintText.GetComponent<TextMeshProUGUI>();
            hint.alignment = ContentText.GetComponent<TextMeshProUGUI>().alignment;
            hint.ForceMeshUpdate(false, false);
        }
    }
}
