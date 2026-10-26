using System;
using TMPro;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>Official ElementConfrontationUI state/color/enable order; local language and animation bindings.</summary>
    public sealed class ElementConfrontationUI : MonoBehaviour
    {
        private SpriteRenderer mEffectImg, mEffectUIMesh, mEffectMeshRender;
        private GameObject mBgGo;
        private TMP_Text mDescribeText;
        private string mElementDefenseKey, mElementConfrontWeak, mElementConfrontStrength, mElementConfrontImmunity;
        private string mSetTriggerName = "";
        private Action<string> setTrigger;
        private Action textRefreshed;

        public string TriggerName => mSetTriggerName;
        public TMP_Text DescribeText => mDescribeText;

        public void Bind(SpriteRenderer effectImg, SpriteRenderer effectUIMesh, SpriteRenderer effectMeshRender,
            GameObject bgGo, TMP_Text describeText, string defense, string weak, string strength, string immunity,
            Action<string> animationTrigger, Action refreshText)
        {
            mEffectImg = effectImg;
            mEffectUIMesh = effectUIMesh;
            mEffectMeshRender = effectMeshRender;
            mBgGo = bgGo;
            mDescribeText = describeText;
            mElementDefenseKey = defense;
            mElementConfrontWeak = weak;
            mElementConfrontStrength = strength;
            mElementConfrontImmunity = immunity;
            setTrigger = animationTrigger;
            textRefreshed = refreshText;
        }

        public void Reset()
        {
            if (mDescribeText == null) return;
            mDescribeText.text = "";
            textRefreshed?.Invoke();
            SetUIShowState(false);
        }

        private void SetUIShowState(bool active)
        {
            mDescribeText.gameObject.SetActive(active);
            mBgGo.SetActive(active);
            mEffectMeshRender.enabled = active;
            mEffectImg.enabled = active;
            mEffectUIMesh.enabled = active;
        }

        public void SetContent(float confrontVal, Color color)
        {
            if (Mathf.Approximately(confrontVal, 0f))
            {
                mDescribeText.text = "";
                textRefreshed?.Invoke();
                return;
            }
            SetUIShowState(true);
            mDescribeText.color = color.linear;
            if (confrontVal > 0f)
            {
                mSetTriggerName = "DiKangTrigger";
                mDescribeText.text = Mathf.Approximately(confrontVal, 1f)
                    ? mElementConfrontImmunity : mElementDefenseKey;
            }
            else
            {
                // Both original threshold branches use the same weak key and trigger.
                mDescribeText.text = mElementConfrontWeak;
                mSetTriggerName = "KeZhiTrigger";
            }
            textRefreshed?.Invoke();
        }

        private void SetElementConfrontStrength()
        {
            mDescribeText.text = mElementConfrontStrength;
            textRefreshed?.Invoke();
            mSetTriggerName = "DaKeTrigger";
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(mSetTriggerName)) setTrigger?.Invoke(mSetTriggerName);
        }
    }
}
