using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.HomeShopNewMark
{
    public static class ShopNewMarkValidation
    {
        sealed class Manager : ITipDotNodeManager
        {
            public Action OnAddScopeDataCollector { get; set; }
            public Action OnRemoveScopeDataCollector { get; set; }
            public readonly HashSet<TipDotConditionNodeBase> Dirty = new HashSet<TipDotConditionNodeBase>();
            public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key) => null;
            public TipDotNodeBase FindNode(TipDotNodeKey key) => null;
            public void AddDirtyConditionNode(TipDotConditionNodeBase node) => Dirty.Add(node);
            public void RemoveDirtyConditionNode(TipDotConditionNodeBase node) => Dirty.Remove(node);
        }

        sealed class Shop : IHomeShopNewMarkServices
        {
            public bool Exists, NewGoods;
            public int ReturnedId = 23;
            public long Index = 99;
            public event Action<int> OnNewMarkUpdate;
            public event Action<int> OnNewMarkRemove;
            public bool TryGetShop(int shopId, out HomeTipShopData shop)
            { shop = new HomeTipShopData(ReturnedId, Index); return Exists; }
            public bool HasNewGoods(long shopIndex) => shopIndex == Index && NewGoods;
            public void Update(int id) => OnNewMarkUpdate?.Invoke(id);
            public void Remove(int id) => OnNewMarkRemove?.Invoke(id);
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        public static void Run()
        {
            var manager = new Manager();
            var shop = new Shop { Exists = true, NewGoods = true };
            var node = new TipDotShopHasNewConditionNode { ShopId = 23, Services = shop, NodeManager = manager };
            node.Init();
            Require(node.CheckShowTip(), "Existing shop with marked goods must initialize visible.");
            shop.Remove(456);
            Require(!node.CheckShowTip(), "The original removal refreshes the selected shop even for another event id.");
            shop.Update(456);
            Require(!node.CheckShowTip(), "Another shop update must not mark the selected shop.");
            shop.Update(23);
            Require(node.CheckShowTip(), "Selected shop update must mark it.");
            shop.ReturnedId = 24;
            shop.Remove(23);
            Require(node.CheckShowTip(), "A different returned shop id must retain the mark.");
            shop.Exists = false;
            shop.Remove(23);
            Require(!node.CheckShowTip(), "Removing the shop must clear its mark.");
            var clone = (TipDotShopHasNewConditionNode)node.Clone();
            Require(clone.ShopId == 23 && clone.Services == null && !clone.CheckShowTip(),
                "Cloning must copy configuration without copying the live service or state.");
            node.Release();
            shop.Update(23);
            Require(!node.CheckShowTip() && manager.Dirty.Count == 0, "Released node retained a subscription or dirty registration.");
            Debug.Log("HOME_SHOP_NEW_MARK_VALIDATION_PASSED");
        }
    }
}
