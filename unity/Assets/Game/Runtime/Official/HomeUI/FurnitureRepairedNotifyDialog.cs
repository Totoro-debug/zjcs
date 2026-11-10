using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeUI
{
    public interface IFurnitureRepairedNotifyServices
    {
        int GetFurnitureItemId(EFurniture furniture);
        FurnitureIconWidget GetFurnitureIconPrefab(EFurniture furniture);
        void GetValidHangUpIdList(EFurniture furniture, List<int> result);
        ProfessionPropertyType GetEffectFixedPropType(int hangUpId);
        double GetHangUpReward(int hangUpId, double hours);
    }

    // Official FurnitureRepairedNotifyDialog.cs; shared UIBase and pool lifecycle.
    public sealed class FurnitureRepairedNotifyDialog : UIBase
    {
        public Transform IconSlot;
        public WorldMultiLanguageText NameTxt;
        [SerializeField] private ProfessionPlayerPropItem mPropItemPrefab;
        [SerializeField] private Transform mPropItemSlot;
        [SerializeField] private WorldStateSwitcher<bool> mPropStateSwitcher;
        private readonly List<int> mHangUpList = new List<int>();
        private readonly List<ProfessionPlayerPropItem> mPropItems = new List<ProfessionPlayerPropItem>(2);
        private GameObjectPool<ProfessionPlayerPropItem> mPropItemPool;
        private EFurniture mFurniture;
        public IFurnitureRepairedNotifyServices Services { private get; set; }

        public void Init(EFurniture furniture) => InitWrapper(() => mFurniture = furniture);

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            int id = Services.GetFurnitureItemId(mFurniture);
            NameTxt.SetKey("item_" + id + "_name");
            foreach (Transform child in IconSlot) Destroy(child.gameObject);
            Instantiate(Services.GetFurnitureIconPrefab(mFurniture), IconSlot).SetState(true);
            Services.GetValidHangUpIdList(mFurniture, mHangUpList);
            bool flag = mHangUpList.Count > 0;
            mPropStateSwitcher.SetState(flag);
            if (flag) ShowHangUpList(mHangUpList);
        }

        protected override void OnDisableAndInited()
        {
            base.OnDisableAndInited();
            if (mHangUpList.Count > 0) mPropItemPool.ReturnMany(mPropItems);
        }

        private void ShowHangUpList(List<int> hangUpIdList)
        {
            if (mPropItemPool == null)
                mPropItemPool = new GameObjectPool<ProfessionPlayerPropItem>(transform, mPropItemSlot, mPropItemPrefab);
            mPropItemPool.ReturnMany(mPropItems);
            for (int i = 0, count = hangUpIdList.Count; i < count; i++)
            {
                int id = hangUpIdList[i];
                long value = (long)Services.GetHangUpReward(id, 1.0);
                ProfessionPlayerPropItem item = mPropItemPool.Borrow();
                mPropItems.Add(item);
                item.Init(Services.GetEffectFixedPropType(id), value, -1L);
            }
        }
    }
}
