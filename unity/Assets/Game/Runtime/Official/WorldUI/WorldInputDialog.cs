using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // Official InputDialog; language arguments and the window manager cross
    // explicit local boundaries. All button and initialization order is retained.
    public sealed class WorldInputDialog : WorldInitializedUI
    {
        public WorldMultiLanguageText TitleText, PlaceholderText;
        public Button ConfirmBtn, CancelBtn, ResetBtn;
        public TMP_InputField InputField;
        [SerializeField] private WorldContentModerationClickMask mContentModerationClickMask;
        [SerializeField] private WorldMultiLanguageText mTipText;
        [NonSerialized] public Action<GameObject> CloseWindow;
        private Action<string> mCallback;
        private Action mCancelCallback, mResetCallback;
        private bool mAutoClose;

        private void Awake()
        {
            if (ConfirmBtn != null) ConfirmBtn.onClick.AddListener(HandleConfirmBtnClick);
            if (CancelBtn != null) CancelBtn.onClick.AddListener(HandleCancelBtnClick);
            if (ResetBtn != null) ResetBtn.onClick.AddListener(OnResetBtnClick);
        }

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            if (ResetBtn != null) ResetBtn.gameObject.SetActive(mResetCallback != null);
        }

        public void Init(string title, string placeholder, Action<string> callback,
            Action cancelCallback = null, Action resetCallback = null, bool autoClose = false,
            int inputLimit = -1, object[] titleArgs = null, object[] placeholderArgs = null)
        {
            InitWrapper(() =>
            {
                if (TitleText != null) TitleText.SetKey(title, titleArgs);
                if (PlaceholderText != null) PlaceholderText.SetKey(placeholder, placeholderArgs);
                mCallback = callback;
                mCancelCallback = cancelCallback;
                mResetCallback = resetCallback;
                mAutoClose = autoClose;
                if (inputLimit != -1) InputField.characterLimit = inputLimit;
                mTipText.gameObject.SetActive(false);
            });
        }

        public void SetContentModerationClickMask(int customType)
        {
            if (mContentModerationClickMask == null) return;
            mContentModerationClickMask.Type = customType;
            mContentModerationClickMask.gameObject.SetActive(true);
        }

        public void ShowTip(string key, params object[] args)
        {
            mTipText.SetKey(key, args);
            mTipText.gameObject.SetActive(true);
        }

        private void HandleConfirmBtnClick()
        {
            string text = InputField.text;
            mCallback?.Invoke(text);
            if (mAutoClose) CloseWindow(gameObject);
        }

        private void HandleCancelBtnClick()
        {
            mCancelCallback?.Invoke();
            CloseWindow(gameObject);
        }

        protected override void OnDisable()
        {
            InputField.text = "";
            base.OnDisable();
        }

        private void OnResetBtnClick() => mResetCallback?.Invoke();
        public void RefreshInput(string input) => InputField.text = input;
    }
}
