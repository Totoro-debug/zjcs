using System;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.Partners;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Offline.HomeHudUI
{
    // The original new-function manager belongs to the player session, not a HUD.
    public sealed class OfflinePetShopNewFunctionSession : IDisposable, IHomePetShopUnlockCondition
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly OfflineHomeHudConditions conditions;
        private readonly IHomeHudCondition condition;
        private readonly HomePetShopNewFunction shop;
        private readonly IDisposable notification;
        private bool disposed;
        private int level;
        private bool petTree;
        public HomeNewFunctionState State { get; } = new HomeNewFunctionState();

        public OfflinePetShopNewFunctionSession(ProfileSlotCatalog slots, string slotId,
            OfficialCurrentHomeRuleAssetCatalog configuration, PartnerConditionRules partnerRules, Action ended = null)
        {
            profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            conditions = new OfflineHomeHudConditions(profile, configuration, partnerRules, 0, OwnsFurniture,
                () => false, () => false);
            condition = conditions.GetFunctionCondition(FunctionType.Shop, HomePetShopNewFunction.PetShopId)
                ?? throw new InvalidOperationException("The selected pet shop function condition is missing.");
            Snapshot();
            shop = new HomePetShopNewFunction(State, this);
            shop.Init();
            notification = OfflinePlayerSessionNotifications.Subscribe(slots, slotId, Refresh, () =>
            {
                Dispose();
                ended?.Invoke();
            });
        }

        private bool OwnsFurniture(int type)
        {
            if (type != OfflineFruitTreeService.PetFurnitureType)
                throw new InvalidOperationException("Unsupported furniture in the pet shop unlock condition: " + type);
            return profile.Save.Player.FruitTree.PetFacilityRepaired;
        }

        public bool IsLimited => !conditions.IsMet(condition);
        public IDisposable Subscribe(Action changed)
        {
            conditions.Register(condition, changed);
            return new Registration(() => conditions.Deregister(condition, changed));
        }

        public void OnTabChanged(bool isOn, bool isShop) => shop.OnTabChanged(isOn, isShop);

        private void Refresh()
        {
            profile.Reload();
            bool levelChanged = level != profile.Save.Player.Level;
            bool itemChanged = petTree != profile.Save.Player.FruitTree.PetFacilityRepaired;
            Snapshot();
            if (levelChanged) conditions.NotifyLevelUpdate();
            if (itemChanged) conditions.NotifyItemCountUpdate();
        }

        private void Snapshot()
        {
            level = profile.Save.Player.Level;
            petTree = profile.Save.Player.FruitTree.PetFacilityRepaired;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            notification?.Dispose();
            shop.Dispose();
            conditions.Dispose();
            State.Reset();
        }

        private sealed class Registration : IDisposable
        {
            private Action release;
            public Registration(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
    }
}
