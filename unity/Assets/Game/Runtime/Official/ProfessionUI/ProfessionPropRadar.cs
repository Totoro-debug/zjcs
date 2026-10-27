using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Original ProfessionPropRadar, with parser/settings services passed by the offline host.
    public sealed class ProfessionPropRadar : MonoBehaviour
    {
        [Serializable] private sealed class PropSlot
        {
            public int SlotType;
            public ProfessionPlayerPropItem PropItem;
        }
        [SerializeField] private List<PropSlot> mRadarPropSlots;
        [SerializeField] private Transform mNormalPropSlot;
        [SerializeField] private ProfessionPropRadarDrawer mDrawer;
        [SerializeField] private ProfessionPlayerPropItem mPropPrefab;
        [SerializeField] private List<ProfessionPlayerPropItem> mCommonPropItemCache;
        private readonly List<ProfessionPlayerPropItem> mCommonPropItems = new List<ProfessionPlayerPropItem>();

        public void ShowProp(IReadOnlyDictionary<int, long> featureProps, Color radarColor,
            Action<ProfessionPlayerPropItem, int, long> setPropValue)
        {
            Clear();
            var values = new float[4];
            foreach (var entry in featureProps)
            {
                ProfessionPlayerPropItem item;
                if (TryGetRadarPropItem(entry.Key, out PropSlot slot))
                {
                    values[mRadarPropSlots.IndexOf(slot)] = (float)CustomFunction(0.01f * entry.Value);
                    item = slot.PropItem;
                }
                else
                {
                    item = GetPropItem();
                    mCommonPropItems.Add(item);
                }
                setPropValue(item, entry.Key, entry.Value);
            }
            mDrawer.DrawRadar(values);
            mDrawer.color = radarColor;
        }
        private ProfessionPlayerPropItem GetPropItem()
        {
            int count = mCommonPropItemCache.Count;
            if (count == 0) return Instantiate(mPropPrefab, mNormalPropSlot, false);
            ProfessionPlayerPropItem result = mCommonPropItemCache[count - 1];
            mCommonPropItemCache.RemoveAt(count - 1);
            result.gameObject.SetActive(true);
            return result;
        }
        private bool TryGetRadarPropItem(int propType, out PropSlot item)
        {
            foreach (PropSlot slot in mRadarPropSlots)
                if (slot.SlotType == propType) { item = slot; return true; }
            item = null;
            return false;
        }
        public static double CustomFunction(float x)
        {
            if (x < 30f) return 0.5f * x / 30f;
            if (x < 40f) return 0.3f * x / 10f - 0.4f;
            return 1.0 / (1.0 + Math.Exp(-0.055f * (x - 20f)));
        }
        private void Clear()
        {
            foreach (ProfessionPlayerPropItem item in mCommonPropItems)
            {
                mCommonPropItemCache.Add(item);
                item.gameObject.SetActive(false);
            }
            mCommonPropItems.Clear();
        }
    }
}
