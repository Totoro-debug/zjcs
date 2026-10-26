using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Zjcs.Official.WorldUI
{
    /// <summary>Port of UnityEngine.UI.Ext.TabGroup, including reset notification ordering.</summary>
    [DefaultExecutionOrder(-1), DisallowMultipleComponent, ExecuteAlways]
    public class WorldTabGroup : UIBehaviour
    {
        public class TabChangeEvent : UnityEvent<WorldTabItem, bool> { }
        [SerializeField] private bool m_AllowSwitchOff;
        [SerializeField] private List<WorldTabItem> mTabItems = new List<WorldTabItem>();
        [SerializeField] private WorldTabItem m_ResetTab;
        public TabChangeEvent OnTabItemChanged = new TabChangeEvent();

        private WorldTabItem mResetTab
        {
            get
            {
                if (m_ResetTab == null && mTabItems.Count > 0) m_ResetTab = mTabItems[0];
                return m_ResetTab;
            }
        }
        public bool allowSwitchOff { get => m_AllowSwitchOff; set => m_AllowSwitchOff = value; }
        protected override void OnEnable() => EnsureValidState();

        private void ValidateTabIsInGroup(WorldTabItem tabItem)
        {
            if (tabItem == null || !mTabItems.Contains(tabItem))
                throw new ArgumentException(string.Format("Toggle {0} is not part of ToggleGroup {1}", tabItem, this));
        }

        public void ResetTabItem(bool needNotify, WorldTabItem tab = null)
        {
            if (tab == null) tab = mResetTab;
            WorldTabItem first = GetFirstActiveTabItem();
            if (first != tab) tab.SetIsOn(true, needNotify, true);
            else if (needNotify)
            {
                OnTabItemChanged.Invoke(first, true);
                first.OnValueChanged.Invoke(true);
            }
        }

        public void NotifyTabOn(WorldTabItem tab, bool sendCallback = true, bool instant = false)
        {
            ValidateTabIsInGroup(tab);
            for (int index = 0; index < mTabItems.Count; index++)
                if (mTabItems[index] != tab) mTabItems[index].SetIsOn(false, sendCallback, instant);
        }
        public void UnregisterTab(WorldTabItem tab) => mTabItems.Remove(tab);
        public void RegisterTab(WorldTabItem tab)
        {
            if (!mTabItems.Contains(tab)) mTabItems.Add(tab);
        }
        public bool EnsureValidState()
        {
            bool result = false;
            if (!allowSwitchOff && !AnyTabOn() && mTabItems.Count != 0)
            {
                WorldTabItem first = mTabItems[0];
                first.isOn = true;
                NotifyTabOn(first);
                result = true;
            }
            IEnumerable<WorldTabItem> active = ActiveTabItems();
            if (active.Count() > 1)
            {
                WorldTabItem first = GetFirstActiveTabItem();
                foreach (WorldTabItem item in active) if (item != first) item.isOn = false;
            }
            return result;
        }
        public bool AnyTabOn() => mTabItems.Find(value => value.isOn) != null;
        public IEnumerable<WorldTabItem> ActiveTabItems() => mTabItems.Where(value => value.isOn);
        public WorldTabItem GetFirstActiveTabItem() => ActiveTabItems().FirstOrDefault();
        public void SwitchOffActiveTabItems()
        {
            if (!allowSwitchOff || !AnyTabOn() || mTabItems.Count == 0) return;
            foreach (WorldTabItem item in ActiveTabItems()) item.isOn = false;
        }
        public WorldTabItem GetTargetTabItem(Func<WorldTabItem, bool> selectFunc) => mTabItems.Find(item => selectFunc(item));
        public WorldTabItem GetTabItem(int index) => mTabItems == null || index >= mTabItems.Count ? null : mTabItems[index];
        public int GetTabItemIndex(WorldTabItem tab) => mTabItems.IndexOf(tab);
        public IEnumerable<WorldTabItem> GetAllTab(Func<WorldTabItem, bool> selector) => mTabItems.Where(selector);
        public WorldTabItem[] GetAllTabs() => mTabItems.ToArray();
    }
}
