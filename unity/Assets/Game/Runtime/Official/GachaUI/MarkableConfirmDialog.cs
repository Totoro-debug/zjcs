// Original client-source/MarkableConfirmDialog.cs; existing local text/UI ports.
using System;
using UnityEngine;
using Zjcs.Official.SkillsUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.GachaUI
{
    public class MarkableConfirmDialog : MonoBehaviour
    {
        [SerializeField] private WorldConfirmDialog mConfirmDialog;
        [SerializeField] private UIToggle mOptionToggle;
        [SerializeField] private WorldMultiLanguageText mTipTxt;
        [SerializeField] private WorldStateSwitcher<bool> mToggleSelectStateSwitcher;
        private bool mIsSelected;

        private void Awake() => mOptionToggle.OnValueChanged += OnOptionValueChange;
        private void OnEnable()
        {
            mOptionToggle.isOn = false;
            mIsSelected = false;
        }

        public void Init(GachaTextKeyArgWrap textWrap, GachaTextKeyArgWrap tipWrap,
            Action<bool> confirmAction, Action cancelAction = null)
        {
            mConfirmDialog.Init(textWrap.Key, textWrap.Args, () => confirmAction?.Invoke(mIsSelected), cancelAction);
            mTipTxt.SetKey(tipWrap.Key, tipWrap.Args);
        }

        private void OnOptionValueChange(GameObject go, bool isOn)
        {
            mIsSelected = isOn;
            mToggleSelectStateSwitcher.SetState(isOn);
        }
    }
}
