using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    // Original release guard: no GM actor / no opened debug state => hide.
    // Administration commands are outside the ordinary-item detail migration.
    public sealed class ItemDetailGmTools : MonoBehaviour
    {
        [Serializable] private sealed class FixedCountBinding { public Button Button; public int Count; }
        [SerializeField] private GameObject mGmToolsRoot;
        [SerializeField] private ItemDetailDialog mItemDetailDialog;
        [SerializeField] private FixedCountBinding[] mFixedCountBindings;
        [SerializeField] private TMP_InputField mCustomInputField;
        [SerializeField] private Button mCustomAddButton;
        [SerializeField] private Toggle mAllowNegativeToggle;
        [SerializeField] private WorldMultiLanguageText mCopyItemIdLabelText;
        [SerializeField] private Button mCopyItemIdButton;
        private bool bound;
        public void BindReleaseActor(bool gm, bool debugStateOpen)
        {
            if (gm && debugStateOpen) throw new NotSupportedException("Bind original GM administration before opening its tools.");
            bound = true;
        }
        private void OnEnable()
        {
            if (!bound) throw new InvalidOperationException("Bind the original release GM guard before activation.");
            mGmToolsRoot.SetActive(false);
        }
    }
}
