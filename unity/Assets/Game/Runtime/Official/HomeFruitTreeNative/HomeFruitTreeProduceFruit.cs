// Original fruit-production sub-furniture controller; online owners use local ports.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeUI;

namespace Zjcs.Official.HomeFruitTreeNative
{
    public interface IHomeFruitTreeHangUpHandler
    {
        void Init();
        void Uninit();
        bool HaveReward();
        void ResetData();
        bool TryInteractiveWithNotReward();
    }

    public sealed class HomeFruitTreeAward
    {
        public int SourceOperate;
        public IReadOnlyList<int> AwardIds;
        public object Original;
    }

    public interface IHomeFruitTreeProductionServices
    {
        bool AwardManagerAvailable { get; }
        int FruitId { get; }
        void RegisterAwardHandler(Func<HomeFruitTreeAward, bool> handler);
        void RemoveAwardHandler(Func<HomeFruitTreeAward, bool> handler);
        void CollectFruitTreeReward(Action<object, int> completed);
        void PopupAward(HomeFruitTreeAward award);
    }

    public class HomeFruitTreeProduceFruit : HomeFruitTreeSubFurniture
    {
        // The recovered prefab keeps its original component reference; the
        // interface binds the separately migrated native fruit-effect handler.
        [SerializeField] private MonoBehaviour mHomeFruitTreeHangUpHandlerFruitItem;
        public IHomeFruitTreeProductionServices ProductionServices { get; set; }
        private IHomeFruitTreeHangUpHandler FruitHandler => (IHomeFruitTreeHangUpHandler)mHomeFruitTreeHangUpHandlerFruitItem;

        private void OnEnable()
        {
            if (ProductionServices != null && ProductionServices.AwardManagerAvailable)
                ProductionServices.RegisterAwardHandler(OnGotAward);
        }

        private void OnDisable()
        {
            if (ProductionServices != null && ProductionServices.AwardManagerAvailable)
                ProductionServices.RemoveAwardHandler(OnGotAward);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            FruitHandler.Uninit();
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (mFurnitureState == EFurnitureState.Functional) FruitHandler.Init();
        }

        public override bool TryCallFunction()
        {
            if (!CheckValid()) return false;
            if (!FruitHandler.HaveReward()) return false;
            ProductionServices.CollectFruitTreeReward((response, error) => {
                if (error == 0 || error == 1243) FruitHandler.ResetData();
            });
            return true;
        }

        public override bool TryCallTip()
        {
            if (!CheckValid()) return false;
            return FruitHandler.TryInteractiveWithNotReward();
        }

        private bool OnGotAward(HomeFruitTreeAward message)
        {
            switch (message.SourceOperate)
            {
                case 43: // GuideReward
                    foreach (int awardId in message.AwardIds)
                        if (awardId == ProductionServices.FruitId) return true;
                    return false;
                case 22: // FruitTree
                    ProductionServices.PopupAward(message);
                    return true;
                default:
                    return false;
            }
        }

        private bool CheckValid()
        {
            if (!Services.PlayerData.HasFurniture(mFurniture)) return false;
            if (Services.CheckFunctionDisabled(EFurniture.FruitTree))
            {
                Services.ShowError(53851);
                return false;
            }
            return true;
        }
    }
}
