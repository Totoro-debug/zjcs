using System;
using System.Collections.Generic;
using System.IO;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using UnityEngine;

namespace Zjcs.Presentation.Commerce
{
    [DisallowMultipleComponent]
    public sealed class CommerceFeatureRuntime : MonoBehaviour
    {
        private ProfileSlotCatalog slots;
        private OfficialCommerceAssetCatalog assetCatalog;
        private OfflineMainlineProgressSession profile;
        private OfflineCommerceSession session;
        private OfflineCommerceOffers offers;

        public string ActiveProfileSlotId { get; private set; }
        public string PersistentRoot { get; set; }
        public int ProfessionId => profile == null ? 0 : profile.Save.Player.Wardrobe.Profession;
        public OfflineCommerceSession Session => session;
        public OfflineCommerceOffers Offers => offers;
        public OfficialCommerceCatalogView Catalog { get; private set; }
        public TMPro.TMP_FontAsset ChineseTmpFont
        {
            get { EnsureCatalog(); return assetCatalog.ChineseTmpFont; }
        }
        public bool IsOpen => session != null && offers != null;

        public IReadOnlyList<string> ExistingCharacterSlots()
        {
            if (slots == null) slots = new ProfileSlotCatalog(string.IsNullOrEmpty(PersistentRoot) ? Application.persistentDataPath : PersistentRoot);
            var result = new List<string>();
            foreach (string slotId in slots.GetSlotIds())
            {
                try
                {
                    OfflineMainlineProgressSession.OpenForCommerce(slots, slotId);
                    result.Add(slotId);
                }
                catch (FileNotFoundException) { }
                catch (InvalidDataException) { }
            }
            return result;
        }

        public void OpenProfile(string slotId)
        {
            if (!ProfileSlotCatalog.TryParseSlotId(slotId))
                throw new ArgumentException("A selected existing profile slot is required.", nameof(slotId));

            EnsureCatalog();
            profile = OfflineMainlineProgressSession.OpenForCommerce(slots, slotId);
            session = OfflineCommerceSession.Open(slots, slotId, DateTime.Now, profile);
            offers = new OfflineCommerceOffers(Catalog, Catalog.Data.settings.voucherItemId, session);
            ActiveProfileSlotId = slotId;
        }

        /// <summary>
        /// Advances the local day/week checkpoint while the window stays open. Returns true when
        /// the session state (and therefore the visible lists) changed.
        /// </summary>
        public bool Tick()
        {
            if (session == null) return false;
            int beforeDay = session.State.Day;
            string beforeInventory = InventorySignature();
            if (!session.Advance(DateTime.Now)) return false;
            return session.State.Day != beforeDay || InventorySignature() != beforeInventory;
        }

        private string InventorySignature()
        {
            var builder = new System.Text.StringBuilder();
            foreach (CommerceAmount item in session.State.Inventory)
                builder.Append(item.ItemId).Append(':').Append(item.Count).Append(';');
            return builder.ToString();
        }

        public OfficialCommercePrefabRenderer CreatePrefabRenderer()
        {
            EnsureCatalog();
            return new OfficialCommercePrefabRenderer(assetCatalog);
        }

        public Sprite FindSprite(string sourcePath)
        {
            EnsureCatalog();
            return assetCatalog.FindSprite(sourcePath);
        }

        public Sprite FindItemSprite(int itemId)
        {
            EnsureCatalog();
            return assetCatalog.FindItemSprite(itemId);
        }

        public AudioClip FindAudio(string eventName)
        {
            EnsureCatalog();
            return assetCatalog.FindAudio(eventName);
        }

        public GameObject InstantiateOfficialPrefab(string prefabKey, Transform parent,
            Action<string, GameObject> bindButton = null)
        {
            RequireOpenProfile();
            return CreatePrefabRenderer().Create(prefabKey, parent, bindButton);
        }

        public CommerceOffer Quote(string kind, int id, int groupId)
        {
            RequireOpenProfile();
            return offers.Quote(kind, id, groupId);
        }

        public CommercePaymentMethod PaymentMethod(CommerceOffer offer)
        {
            RequireOpenProfile();
            return offers.PaymentMethod(offer);
        }

        public CommerceTransactionResult Purchase(CommerceOffer offer)
        {
            RequireOpenProfile();
            if (offer == null) return new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "商品不存在或已售罄。");
            return offers.Purchase(offer, offers.PaymentMethod(offer));
        }

        public CommerceTransactionResult Purchase(CommerceOffer offer, CommercePaymentMethod method)
        {
            RequireOpenProfile();
            if (offer == null) return new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "商品不存在或已售罄。");
            return offers.Purchase(offer, method);
        }

        public CommerceTransactionResult ConfirmPurchase(string kind, int id, int groupId)
        {
            RequireOpenProfile();
            CommerceOffer current = Quote(kind, id, groupId);
            return current == null
                ? new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "商品不存在或已售罄。")
                : offers.Purchase(current, offers.PaymentMethod(current));
        }

        public CommerceTransactionResult ClaimDailyFree()
        {
            RequireOpenProfile();
            return offers.ClaimDailyFree();
        }

        public CommerceTransactionResult ClaimWeeklyFree(int id, int groupId)
        {
            RequireOpenProfile();
            return offers.ClaimWeeklyFree(id, groupId);
        }

        public CommerceTransactionResult ClaimFirstAward(int day)
        {
            RequireOpenProfile();
            return offers.ClaimFirstAward(day, profile.Save.Player.Wardrobe.Profession);
        }

        public CommerceTransactionResult ActivateFirstPurchase()
        {
            RequireOpenProfile();
            return offers.ActivateFirstPurchase();
        }

        public CommerceTransactionResult ClaimAccumulated(int level)
        {
            RequireOpenProfile();
            return offers.ClaimAccumulated(level);
        }

        public CommerceTransactionResult Exchange(long amount)
        {
            RequireOpenProfile();
            return offers.Exchange(amount);
        }

        public void CloseProfile()
        {
            profile = null;
            session = null;
            offers = null;
            ActiveProfileSlotId = null;
        }

        private void EnsureCatalog()
        {
            if (slots == null) slots = new ProfileSlotCatalog(string.IsNullOrEmpty(PersistentRoot) ? Application.persistentDataPath : PersistentRoot);
            if (assetCatalog == null) assetCatalog = OfficialCommerceAssetCatalog.LoadDefault();
            if (Catalog == null) Catalog = new OfficialCommerceCatalogView(assetCatalog.Load());
        }

        private void RequireOpenProfile()
        {
            if (session == null || offers == null || profile == null)
                throw new InvalidOperationException("Open an existing character profile before using the commerce feature.");
        }
    }
}

