using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public interface IWorldContentModerationServices
    {
        // The adapter projects only ServerFunctionType.ContentCustom updates.
        event Action<HashSet<long>> DisabledUpdated;
        bool CheckDisabled(int customType);
        void ShowError(int code);
    }

    // Official ContentModerationClickMask. ServerInfoManager is projected by
    // the host; the original refresh, sub-index filter and click branches remain.
    public sealed class WorldContentModerationClickMask : MonoBehaviour
    {
        public int Type;
        public Button MaskBtn;
        private Action OnClick;
        private IWorldContentModerationServices services;
        public void Configure(IWorldContentModerationServices value)
            => services = value ?? throw new ArgumentNullException(nameof(value));
        private void Awake() => MaskBtn.onClick.AddListener(OnMaskClick);
        private void OnEnable()
        {
            services.DisabledUpdated += OnFunctionDisabledUpdate;
            Refresh();
        }
        private void OnDisable() => services.DisabledUpdated -= OnFunctionDisabledUpdate;
        private void OnFunctionDisabledUpdate(HashSet<long> subIndex)
        {
            if (subIndex.Contains(Type)) Refresh();
        }
        private void Refresh() => MaskBtn.gameObject.SetActive(services.CheckDisabled(Type));
        private void OnMaskClick()
        {
            if (OnClick != null) OnClick();
            else services.ShowError(1230);
        }
        public void SetClickCallback(Action callback) => OnClick = callback;
    }
}
