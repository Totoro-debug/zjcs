// Original GachaBlueDotBase; parser and player access use the shared local ports.
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeTipDots;

namespace Zjcs.Official.HomePetGachaUI
{
    public interface IHomeGachaBlueDotServices : IHomeGachaTipServices
    {
        bool HasPlayer { get; }
    }

    public abstract class GachaBlueDotBase : RedDotItemBase
    {
        [SerializeField] private List<GachaType> mGachaTypes;
        private readonly Dictionary<HomeTipGachaInfo, long> mCostDict = new Dictionary<HomeTipGachaInfo, long>();
        private readonly HashSet<int> mCostItemId = new HashSet<int>();

        public IHomeGachaBlueDotServices GachaServices { get; set; }

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            InitCostDict();
            GachaServices.OnGacha += OnGacha;
            GachaServices.OnItemCountUpdate += OnItemUpdate;
        }

        protected override void OnDisableAndInited()
        {
            mCostDict.Clear();
            mCostItemId.Clear();
            if (GachaServices.HasPlayer)
            {
                GachaServices.OnGacha -= OnGacha;
                GachaServices.OnItemCountUpdate -= OnItemUpdate;
            }
            base.OnDisableAndInited();
        }

        private void OnItemUpdate(int itemId)
        {
            if (mCostItemId.Contains(itemId)) Refresh();
        }

        private void OnGacha() => Refresh();

        private void InitCostDict()
        {
            mCostDict.Clear();
            mCostItemId.Clear();
            foreach (GachaType type in mGachaTypes)
            {
                HomeTipGachaInfo info = GachaServices.GetGacha(type);
                if (info != null && GachaServices.TryGetChestCost(info.ChestId, info.CostItemId, out long cost))
                {
                    mCostItemId.Add(info.CostItemId);
                    mCostDict[info] = cost;
                }
            }
        }

        public override bool ShowRedDot()
        {
            foreach (KeyValuePair<HomeTipGachaInfo, long> item in mCostDict)
            {
                HomeTipGachaInfo info = item.Key;
                long cost = item.Value;
                if (cost != 0 && !GachaServices.IsGachaFunctionLimited(info.GachaType)
                    && !GachaServices.IsGachaDailyCountLimited(info.GachaClassType)
                    && !GachaServices.IsChestFunctionLimited(info.ChestId)
                    && GachaServices.GetItemAmount(info.CostItemId) / cost >= GetShowRedDotMinCount(info))
                    return true;
            }
            return false;
        }

        protected abstract long GetShowRedDotMinCount(HomeTipGachaInfo info);
    }
}
