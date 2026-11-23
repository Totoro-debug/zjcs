using System;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeNativeAssets
{
    // Resource/DI boundary only. HomeWeaponRack retains the official selection,
    // reload, slot-reset and handle-release order. Destroy its controllers before
    // disposing this owner; appearance catalogs have their own wider lifetime.
    public sealed class HomeNativeWeaponRackServices : IHomeWeaponRackServices, IDisposable
    {
        readonly Func<IProfessionPlayerAppearanceSource> mainPlayer, playerData;
        readonly Func<Action, IDisposable> subscribeHomeChange;
        readonly IProfessionAppearanceUILiteBindings appearanceBindings;
        readonly IHomeWeaponAnimationBindings animations;
        readonly HomeNativePrefabLoader loader = new HomeNativePrefabLoader();
        readonly HomeNativePrefabFactory factory;
        bool disposed;

        public IProfessionPlayerAppearanceSource MainPlayer => mainPlayer();
        public IProfessionPlayerAppearanceSource PlayerData => playerData();
        public ProfessionAppearanceResolver Appearance { get; }
        public ProfessionAppearanceTables Tables { get; }

        public HomeNativeWeaponRackServices(Func<IProfessionPlayerAppearanceSource> mainPlayer,
            Func<IProfessionPlayerAppearanceSource> playerData, ProfessionAppearanceResolver appearance,
            ProfessionAppearanceTables tables, Func<Action, IDisposable> subscribeHomeChange,
            IProfessionAppearanceUILiteBindings appearanceBindings, IHomeWeaponAnimationBindings animations)
        {
            this.mainPlayer = mainPlayer ?? throw new ArgumentNullException(nameof(mainPlayer));
            this.playerData = playerData ?? throw new ArgumentNullException(nameof(playerData));
            Appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            this.subscribeHomeChange = subscribeHomeChange ?? throw new ArgumentNullException(nameof(subscribeHomeChange));
            this.appearanceBindings = appearanceBindings ?? throw new ArgumentNullException(nameof(appearanceBindings));
            this.animations = animations ?? throw new ArgumentNullException(nameof(animations));
            factory = new HomeNativePrefabFactory(Install);
        }

        void CheckLifetime()
        {
            if (disposed) throw new ObjectDisposedException(nameof(HomeNativeWeaponRackServices));
        }
        public IDisposable SubscribeHomeChange(Action action)
        {
            CheckLifetime();
            return subscribeHomeChange(action);
        }
        public IHomeFurniturePrefabHandle LoadPrefab(string originalLocation)
        {
            CheckLifetime();
            return loader.LoadPrefab(originalLocation);
        }
        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            CheckLifetime();
            return factory.Instantiate(prefab, parent, worldPositionStays);
        }
        void Install(GameObject instance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<AppearanceRendererLite>(true))
                renderer.Configure(appearanceBindings);
            foreach (var side in instance.GetComponentsInChildren<AppearancePartSideSwitcher>(true))
                side.AnimationBindings = animations;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            factory.Dispose();
            loader.Dispose();
        }
    }
}
