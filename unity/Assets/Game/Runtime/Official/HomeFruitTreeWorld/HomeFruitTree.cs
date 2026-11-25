// Original shared fruit-tree state and click routing; local data and text are injected.
using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeFruitTreeNative;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeFruitTreeWorld
{
    public class HomeFruitTree : ClickableHomeObj
    {
        private enum FruitTreeState { None, Repair1, Function1, Repair2, Function2 }

        [SerializeField] private WorldStateSwitcher<FruitTreeState> mStateSwitcher;
        [SerializeField] private List<HomeFruitTreeSubFurniture> mSubFurnitureList;
        [SerializeField] private MonoDictionary<FruitTreeState, string> mTreeAnimDict;
        [SerializeField] private SkeletonAnimation mTreeAnimation;
        private FruitTreeState mFruitTreeState;

        public IHomeFurnitureServices FurnitureServices { get; set; }
        public Func<string, string> Localize { get; set; }

        private void Awake()
        {
            foreach (HomeFruitTreeSubFurniture furniture in mSubFurnitureList)
            {
                furniture.OnStateChange = (Action)Delegate.Combine(furniture.OnStateChange, new Action(OnStateChange));
                furniture.GetStateIsLock = (Func<HomeFruitTreeSubFurniture, bool>)Delegate.Combine(
                    furniture.GetStateIsLock, new Func<HomeFruitTreeSubFurniture, bool>(OnGetStateLock));
            }
        }

        private void OnEnable() => RefreshState();

        private void RefreshState()
        {
            FruitTreeState state = FruitTreeState.None;
            for (int i = 0; i < mSubFurnitureList.Count; i++)
            {
                FurnitureBase.EFurnitureState furnitureState = mSubFurnitureList[i].GetState();
                if (furnitureState != FurnitureBase.EFurnitureState.Locked)
                {
                    state = (FruitTreeState)(i * 2 + 1);
                    if (furnitureState == FurnitureBase.EFurnitureState.Functional) state++;
                }
            }
            mFruitTreeState = state;
            mStateSwitcher.SetState(state);
            if (mTreeAnimation != null && mTreeAnimDict.TryGetValue(state, out string animation))
                mTreeAnimation.AnimationName = animation;
        }

        private void OnStateChange()
        {
            foreach (HomeFruitTreeSubFurniture furniture in mSubFurnitureList) furniture.UpdateState();
            RefreshState();
        }

        private bool OnGetStateLock(HomeFruitTreeSubFurniture subFurniture)
        {
            if (FurnitureServices.PlayerData.HasFurniture(subFurniture.Furniture)) return false;
            bool found = false;
            foreach (HomeFruitTreeSubFurniture furniture in mSubFurnitureList)
            {
                if (found)
                {
                    FurnitureBase.EFurnitureState state = furniture.GetState();
                    if (state == FurnitureBase.EFurnitureState.RepairNotice || state == FurnitureBase.EFurnitureState.CanRepair)
                        return true;
                }
                else if (furniture == subFurniture) found = true;
            }
            return false;
        }

        protected override void OnValidClick()
        {
            if (mFruitTreeState == FruitTreeState.None)
            {
                FurnitureServices.SystemMessage(Localize("HomeFruitTree.InitialStateTip"));
            }
            else
            {
                if (mFruitTreeState < FruitTreeState.Function1) return;
                foreach (HomeFruitTreeSubFurniture furniture in mSubFurnitureList)
                    if (furniture.TryCallFunction()) return;
                using (List<HomeFruitTreeSubFurniture>.Enumerator enumerator = mSubFurnitureList.GetEnumerator())
                    while (enumerator.MoveNext() && !enumerator.Current.TryCallTip()) { }
            }
        }
    }
}
