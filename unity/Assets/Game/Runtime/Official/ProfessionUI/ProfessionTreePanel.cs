using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ProfessionUI
{
    public sealed class ProfessionTreePanel : MonoBehaviour
    {
        public List<ProfessionTreePanelItem> Items;
        [SerializeField] private WorldMultiLanguageText mTotalProfessionLevel;
        [SerializeField] private WorldStateSwitcher<int> mHighestRankSwitcher;
        private IProfessionTreeData data;
        private bool subscribed;
        public void Init(IProfessionTreeData context)
        {
            Unsubscribe();
            data = context ?? throw new ArgumentNullException(nameof(context));
            foreach (ProfessionTreePanelItem item in Items) item.Init(data);
            if (isActiveAndEnabled) Activate();
        }
        private void OnEnable() { if (data != null) Activate(); }
        private void Activate()
        {
            RefreshView();
            if (!subscribed) { data.Changed += RefreshView; subscribed = true; }
        }
        private void OnDisable() => Unsubscribe();
        private void Unsubscribe()
        {
            if (subscribed && data != null) data.Changed -= RefreshView;
            subscribed = false;
        }
        private void RefreshView()
        {
            mHighestRankSwitcher.SetState(data.HighestUnlockedRank);
            foreach (ProfessionTreePanelItem item in Items) item.Init(data);
            mTotalProfessionLevel.Format(data.TotalProfessionLevel);
        }
    }
}
