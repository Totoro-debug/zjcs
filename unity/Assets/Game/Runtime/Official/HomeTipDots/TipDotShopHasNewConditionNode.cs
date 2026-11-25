// Selected official TipDotShopHasNewConditionNode; local services replace injected managers.
using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeTipDots
{
    public readonly struct HomeTipShopData
    {
        public readonly int ShopId;
        public readonly long ShopIndex;
        public HomeTipShopData(int shopId, long shopIndex) { ShopId = shopId; ShopIndex = shopIndex; }
    }

    public interface IHomeShopNewMarkServices
    {
        bool TryGetShop(int shopId, out HomeTipShopData shop);
        bool HasNewGoods(long shopIndex);
        event Action<int> OnNewMarkUpdate;
        event Action<int> OnNewMarkRemove;
    }

    [Serializable]
    public class TipDotShopHasNewConditionNode : TipDotConditionNodeBase
    {
        public int ShopId;
        public IHomeShopNewMarkServices Services { get; set; }
        private bool mHasNew;

        public override void Init()
        {
            base.Init();
            mHasNew = Services.TryGetShop(ShopId, out HomeTipShopData shop)
                && Services.HasNewGoods(shop.ShopIndex);
            Services.OnNewMarkUpdate += OnNewMarkUpdate;
            Services.OnNewMarkRemove += OnNewMarkRemove;
        }

        public override void Release()
        {
            Services.OnNewMarkUpdate -= OnNewMarkUpdate;
            Services.OnNewMarkRemove -= OnNewMarkRemove;
            base.Release();
        }

        public override bool CheckShowTip() => mHasNew;

        private void OnNewMarkUpdate(int shopId)
        {
            if (shopId == ShopId)
            {
                mHasNew = true;
                SetDirty();
            }
        }

        private void OnNewMarkRemove(int shopId)
        {
            if (!mHasNew) return;
            if (!Services.TryGetShop(ShopId, out HomeTipShopData shop) || shop.ShopId == ShopId)
            {
                mHasNew = false;
                SetDirty();
            }
        }

        public override TipDotNodeBase Clone()
        {
            var node = new TipDotShopHasNewConditionNode();
            node.Fill(this);
            return node;
        }

        public void Fill(TipDotShopHasNewConditionNode source) => ShopId = source.ShopId;
    }
}
