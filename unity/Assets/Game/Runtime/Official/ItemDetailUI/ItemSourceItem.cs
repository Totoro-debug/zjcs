using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class ItemSourceItem : MonoBehaviour, IWindowOperator
    {
        public WorldMultiLanguageText TypeText;
        public List<Button> SwitchToBtns;
        [SerializeField] private WorldStateSwitcher<bool> mStateSwitcher;
        [SerializeField] private CanvasGroup mCanvasGroup;
        private ItemDetailSource source;
        private GameObject container;
        private long requireCount;
        private bool limited;
        public GameObject Operator => container;
        public bool IsLimit { set { if (limited != value) { limited = value; UpdateLimitContent(); } } }
        private void Awake() { foreach (var button in SwitchToBtns) button.onClick.AddListener(OnSwitchToBtnClick); }
        public void Init(ItemDetailSource value, GameObject owner, long required = 0)
        {
            source = value ?? throw new ArgumentNullException(nameof(value));
            container = owner; requireCount = required;
            TypeText.SetKey(source.DescriptionKey, source.DescriptionArguments);
            limited = source.Limited; UpdateLimitContent();
        }
        public void OnSwitchToBtnClick()
        {
            if (source == null) throw new InvalidOperationException("Original item source is unbound.");
            if (limited) source.BroadcastLimit(); else source.Navigate(this, requireCount);
        }
        private void UpdateLimitContent()
        { mStateSwitcher.SetState(!limited); mCanvasGroup.alpha = limited ? .5f : 1f; }
    }
}
