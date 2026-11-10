using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.BattleStageStatusUI
{
    // Original stage strip lifecycle, cloned row template and background switch.
    // Stage/config lookup and resource loading are supplied by the local host.
    public sealed class FightMainPanelStageStatus : MonoBehaviour
    {
        [SerializeField] private Transform mSlot;
        [SerializeField] private FightStatusUIItem mStatusUIItem;
        [SerializeField] private GameObject mStatusBack;
        [SerializeField] private Button mBtn;
        private List<int> mStatusEntityIds;
        private readonly List<FightStatusUIItem> mItems = new List<FightStatusUIItem>();
        private FightStageStatusPresentation presentation;
        private Action open;

        public void Init(List<int> statusIds, FightStageStatusPresentation source, Action onOpen)
        {
            presentation = source;
            open = onOpen;
            mStatusEntityIds = statusIds == null ? new List<int>() : statusIds.Distinct().ToList();
            if (isActiveAndEnabled) RefreshView();
        }
        public void Refresh(List<int> statusIds)
        {
            var next = statusIds == null ? new List<int>() : statusIds.Distinct().ToList();
            if (mStatusEntityIds != null && mStatusEntityIds.SequenceEqual(next)) return;
            mStatusEntityIds = next;
            if (isActiveAndEnabled) RefreshView();
        }
        private void OnEnable()
        {
            mBtn.onClick.AddListener(OnBtnClicked);
            if (presentation != null) RefreshView();
        }
        private void OnDisable()
        {
            RemoveAllIcon();
            mBtn.onClick.RemoveListener(OnBtnClicked);
        }
        private void OnBtnClicked()
        {
            if (mStatusEntityIds != null && mStatusEntityIds.Count != 0) open?.Invoke();
        }
        private void RefreshView()
        {
            RemoveAllIcon();
            RefreshIcon();
            RefreshStatusBack();
        }
        private void RefreshIcon()
        {
            if (mStatusEntityIds == null || mStatusEntityIds.Count == 0) return;
            foreach (int statusId in mStatusEntityIds)
            {
                var entry = presentation.Resolve(statusId);
                var item = Instantiate(mStatusUIItem, mSlot, false);
                presentation.Bind(item.gameObject);
                item.gameObject.SetActive(true);
                item.Show(entry.Icon, entry.Background, 0);
                mItems.Add(item);
            }
        }
        private void RefreshStatusBack() => mStatusBack.SetActive(mItems.Count > 0);
        private void RemoveAllIcon()
        {
            foreach (var item in mItems)
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                    Destroy(item.gameObject);
                }
            mItems.Clear();
        }
    }
}
