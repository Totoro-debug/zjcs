using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Editor
{
    internal static class OfficialNationShopAcceptance
    {
        private const int Map13Group = 13;
        private const int Map13Currency = 13;
        private const int Map13FirstGood = 32101;

        [MenuItem("Zjcs/Validate/Mainline Nation Shop Rules")]
        public static void Run()
        {
            string validation = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(validation) || !Directory.Exists(validation))
                throw new BuildFailedException("Set ZJCS_VALIDATION_DIR to this validation session directory.");
            string profileRoot = Path.Combine(validation, "nation-shop-profiles");
            Directory.CreateDirectory(profileRoot);
            var slots = new ProfileSlotCatalog(profileRoot);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds() };
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "国度商店验收";
            save.Player.Level = 30;
            save.Player.Wardrobe.Profession = 1;
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = Map13Currency, Count = 500 });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));

            OfficialNationShopCatalog catalog = OfficialNationShopCatalog.LoadDefault();
            Require(catalog.Data.groups.Length == 8 && catalog.Data.levels.Length == 32
                && catalog.Data.shops.Length == 32 && catalog.Data.goods.Length == 329,
                "The pinned mainline nation-shop catalog counts changed.");
            Require(catalog.LevelForExperience(Map13Group, 3499)?.shopId == 131
                && catalog.LevelForExperience(Map13Group, 3500)?.shopId == 132
                && catalog.LevelForExperience(Map13Group, 8000)?.shopId == 133
                && catalog.LevelForExperience(Map13Group, 13000)?.shopId == 134,
                "Map13 nation-shop level boundaries differ from the official table.");

            OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenForCommerce(slots, slot);
            OfflineCommerceSession commerce = OfflineCommerceSession.Open(slots, slot, DateTime.Now, profile);
            var service = new OfflineNationShopService(catalog, commerce);
            NationShopPurchaseResult insufficient = service.Purchase(Map13Group, Map13FirstGood);
            Require(insufficient.Code == NationShopPurchaseCode.CurrencyInsufficient
                && service.Experience(Map13Group) == 0 && commerce.State.Inventory.Single(value => value.ItemId == Map13Currency).Count == 500,
                "An insufficient Map13 purchase changed currency or experience.");

            ProgressionResult grant = profile.ApplyInventoryTransaction("nation-shop-acceptance:fund-map13", mainline =>
            {
                OfflineItemStack currency = mainline.Player.Inventory.Single(value => value.ItemId == Map13Currency);
                currency.Count = 50000;
            });
            Require(grant.IsApplied && commerce.Advance(DateTime.Now), "The Map13 nation-shop test wallet could not be funded.");
            Require(commerce.UpdateState(state => SetCounter(state, "nation-shop-exp:13", 2500)),
                "The Map13 boundary fixture could not be persisted.");

            NationShopPurchaseResult purchased = service.Purchase(Map13Group, Map13FirstGood);
            Require(purchased.Code == NationShopPurchaseCode.Applied && purchased.Experience == 3500
                && purchased.Level == 2 && service.CurrentShopId(Map13Group) == 132,
                "A Map13 purchase did not grant exact paid-currency experience or unlock level 2.");
            Require(commerce.State.Inventory.Single(value => value.ItemId == Map13Currency).Count == 49000
                && commerce.State.Inventory.Single(value => value.ItemId == 110).Count == 1
                && service.RemainingStock(catalog.Data.goods.Single(value => value.id == Map13FirstGood)) == 0,
                "The Map13 purchase did not atomically debit, grant, and decrement fixed stock.");
            Require(service.Purchase(Map13Group, Map13FirstGood).Code == NationShopPurchaseCode.Locked,
                "A good from the previous Map13 shop level remained available after promotion.");

            OfflineMainlineProgressSession reopenedProfile = OfflineMainlineProgressSession.OpenForCommerce(slots, slot);
            OfflineCommerceSession reopened = OfflineCommerceSession.Open(slots, slot, DateTime.Now, reopenedProfile);
            var reopenedService = new OfflineNationShopService(catalog, reopened);
            Require(reopenedService.Experience(Map13Group) == 3500 && reopenedService.Level(Map13Group) == 2
                && reopenedService.PurchasedCount(Map13FirstGood) == 1
                && reopened.State.Inventory.Single(value => value.ItemId == 110).Count == 1,
                "Map13 nation-shop experience, stock, or inventory did not survive cold reopen.");
            Debug.Log("Mainline nation-shop rules passed: all official catalog rows, Map13 thresholds, insufficient-funds rollback, atomic fixed-stock purchase, level availability, and cold reopen.");
        }

        private static void SetCounter(OfflineCommerceState state, string key, long count)
        {
            CommerceCounter counter = state.Bought.SingleOrDefault(value => value.Key == key);
            if (counter == null) state.Bought.Add(new CommerceCounter { Key = key, Count = count });
            else counter.Count = count;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }
    }
}
