using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ProfessionUI
{
    // PlayerPropItem's string value overload and presentation lifecycle. The host
    // supplies the original localized/percent/compact value and property icon.
    public sealed class ProfessionPlayerPropItem : MonoBehaviour
    {
        public Image myBg, myIcon;
        public WorldMultiLanguageText myPropTypeText, myPropValueText;
        public GameObject myPropPreviewObj, myEffect;
        public Button PropDetailBtn;
        public WorldMultiLanguageText PropValueFromText, PropValueToText;
        [SerializeField] private WorldStateSwitcher<bool> mActiveStateSwitcher;
        [SerializeField] private UnityEngine.Object mBlessPropBtn;
        public float myChangeTextTime = 0.2f, myEffectDuration = 0.55f;
        public bool UseOneText;
        public Color DefaultTextColor, PreviewTextColor;
        public bool AdjustPreviewToColor;
        public Color PreviewIncreaseColor, PreviewDecreaseColor;
        public bool ShowSign;
        [SerializeField] private Image mLineIcon;
        private string propName, fromValue, toValue, formatKey = "{0}";
        private bool isPreview, negative, needSpaceBeforeValue;
        public Action ShowDetail;

        private void OnEnable()
        {
            if (PropDetailBtn != null) PropDetailBtn.onClick.AddListener(ShowPropDetail);
        }
        private void OnDisable()
        {
            if (PropDetailBtn != null) PropDetailBtn.onClick.RemoveListener(ShowPropDetail);
            StopEffect();
        }
        private void ShowPropDetail() => ShowDetail?.Invoke();
        public void Init(string nameKey, string value, Sprite icon, string nextValue = null,
            bool instant = true, bool isBless = false, bool isNegative = false,
            string valueFormatKey = "{0}", bool previewIncreasing = false)
        {
            propName = nameKey;
            fromValue = value;
            toValue = nextValue;
            isPreview = nextValue != null;
            negative = isNegative;
            formatKey = valueFormatKey;
            if (myPropTypeText != null) myPropTypeText.SetKey(nameKey);
            if (AdjustPreviewToColor && PropValueToText != null)
                PropValueToText.Color = previewIncreasing ? PreviewIncreaseColor : PreviewDecreaseColor;
            mActiveStateSwitcher?.SetState(isBless);
            if (instant) ApplyText();
            if (myIcon != null)
            {
                myIcon.gameObject.SetActive(icon != null);
                myIcon.sprite = icon;
            }
        }
        private void ApplyText()
        {
            if (myPropValueText != null)
            {
                myPropValueText.gameObject.SetActive(!isPreview);
                PopulateValue(myPropValueText, fromValue);
            }
            if (myPropPreviewObj != null) myPropPreviewObj.SetActive(isPreview);
            if (PropValueFromText != null) PropValueFromText.SetKey(formatKey, fromValue);
            if (PropValueToText != null && toValue != null) PropValueToText.SetKey(formatKey, toValue);
        }
        private void PopulateValue(WorldMultiLanguageText text, string value)
        {
            if (needSpaceBeforeValue)
                value = WorldMultiLanguageText.Localize == null ? value
                    : string.Format(WorldMultiLanguageText.Localize("ui_space_before_value"), value);
            if (ShowSign && !negative) text.SetKey("ui_common_prop_plus", value);
            else if (UseOneText)
            {
                string name = WorldMultiLanguageText.Localize?.Invoke(propName) ?? propName;
                text.SetKey(formatKey, name, value);
            }
            else text.SetKey(formatKey, value);
        }
        public void SetNeedSpaceBeforeValue(bool value) { needSpaceBeforeValue = value; ApplyText(); }
        public IEnumerator PlayEffect(bool isForceShow = false)
        {
            if (myEffect == null || (!isForceShow && !isPreview)) yield break;
            myEffect.SetActive(true);
            yield return new WaitForSeconds(myChangeTextTime);
            ApplyText();
            yield return new WaitForSeconds(myEffectDuration - myChangeTextTime);
            myEffect.SetActive(false);
        }
        public void StopEffect() { ApplyText(); if (myEffect != null) myEffect.SetActive(false); }
        public void SetBgVisible(bool visible) { if (myBg != null) myBg.enabled = visible; }
        public void SetLineVisible(bool visible) { if (mLineIcon != null) mLineIcon.enabled = visible; }
    }
}
