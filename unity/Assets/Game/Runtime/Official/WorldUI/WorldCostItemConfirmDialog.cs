using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // ConfirmDialog and CostItemConfirmDialog preserve their original callback
    // and close order. PlayerCostMoneyContent uses the original bound fields.
    public sealed class WorldCostItemConfirmDialog : MonoBehaviour
    {
        public TMP_Text TitleText, ContentText, ConfirmText, CancelText, CostText;
        public Button ConfirmBtn, CancelBtn;
        public Image CostItemIcon;
        public Color EnoughColor, LackColor;
        public GameObject HintGo, ItemContentObj, MoneyContent, LockMask;
        public Transform Slot, TopBarSlot;
        public WorldPopupWindow PopupWindow;
        private Action callback, cancelCallback, closed;
        private bool callbackExecuted;

        public void Init(string title, string content, string confirm, string cancel, string cost,
            Sprite currency, bool enough, Action onConfirm, Action onCancel, Action onClosed)
        {
            callback = onConfirm;
            cancelCallback = onCancel;
            closed = onClosed;
            callbackExecuted = false;
            HintGo.SetActive(false);
            LockMask.SetActive(false);
            CancelBtn.gameObject.SetActive(true);
            TitleText.text = title;
            ContentText.text = content;
            ConfirmText.text = confirm;
            CancelText.text = cancel;
            MoneyContent.SetActive(true);
            CostItemIcon.sprite = currency;
            CostText.text = cost;
            CostText.color = enough ? EnoughColor : LackColor;
            // UpdateEmptyCurrency / SetItemContent both clear their original slots.
            foreach (Transform child in TopBarSlot) Destroy(child.gameObject);
            foreach (Transform child in Slot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            ItemContentObj.SetActive(true);
            ConfirmBtn.onClick.RemoveAllListeners();
            CancelBtn.onClick.RemoveAllListeners();
            ConfirmBtn.onClick.AddListener(Confirm);
            CancelBtn.onClick.AddListener(Cancel);
            PopupWindow.Show();
        }

        public void Confirm()
        {
            if (callbackExecuted) return;
            callback?.Invoke();
            callbackExecuted = true;
            Close();
        }

        public void Cancel()
        {
            if (callbackExecuted) return;
            callbackExecuted = true;
            cancelCallback?.Invoke();
            Close();
        }

        public void Abandon()
        {
            callbackExecuted = true;
            callback = cancelCallback = closed = null;
        }

        private void Close() => PopupWindow.Hide(() => closed?.Invoke());
        private void OnDisable()
        {
            if (callbackExecuted) return;
            callbackExecuted = true;
            cancelCallback?.Invoke();
        }
    }
}
