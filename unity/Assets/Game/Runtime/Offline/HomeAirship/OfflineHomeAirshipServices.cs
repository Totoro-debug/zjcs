using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeUI;

namespace Zjcs.Offline.HomeAirship
{
    // Supplies only activities that really exist in the local activity system and
    // have an official airship banner configuration and native prefab.
    public interface IOfflineAirshipActivitySource
    {
        IDisposable Subscribe(Action changed);
        IEnumerable<AirshipActivityEntry> Entries();
        void Click(AirshipActivityEntry entry);
        string SubtitleKey(AirshipActivityEntry entry);
        GameObject LoadBanner(AirshipActivityEntry entry, Transform slot);
        void ReleaseBanners();
    }

    public sealed class OfflineHomeAirshipServices : IAirshipFurnitureServices, IAirShipBannerServices
    {
        private sealed class Subscription : IDisposable
        {
            private Action release;
            public Subscription(Action release) { this.release = release; }
            public void Dispose() { var previous = release; release = null; previous?.Invoke(); }
        }

        private sealed class SpriteLease : IAirshipSpriteLease
        {
            public Sprite Sprite { get; private set; }
            public SpriteLease(Sprite sprite) { Sprite = sprite; }
            public void Dispose() { Sprite = null; }
        }

        private readonly IHomeFurniturePlayer player;
        private readonly IHomeWeaponAnimationBindings animations;
        private readonly IOfflineAirshipActivitySource activities;
        private readonly Sprite unrepairedBackground, repairedBackground;
        private readonly Action openTrade;

        public OfflineHomeAirshipServices(IHomeFurniturePlayer player, IHomeWeaponAnimationBindings animations,
            IOfflineAirshipActivitySource activities, Sprite unrepairedBackground, Sprite repairedBackground,
            Action openTrade)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.animations = animations ?? throw new ArgumentNullException(nameof(animations));
            this.activities = activities ?? throw new ArgumentNullException(nameof(activities));
            this.unrepairedBackground = unrepairedBackground ? unrepairedBackground : throw new ArgumentNullException(nameof(unrepairedBackground));
            this.repairedBackground = repairedBackground ? repairedBackground : throw new ArgumentNullException(nameof(repairedBackground));
            this.openTrade = openTrade ?? throw new ArgumentNullException(nameof(openTrade));
        }

        // The confirmed offline mode has no peer trade or trade display updates.
        public IDisposable SubscribeDisplayItemChange(Action<int, Sprite> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            return new Subscription(null);
        }

        public IAirshipSpriteLease LoadItemBackground(bool repaired)
            => new SpriteLease(repaired ? repairedBackground : unrepairedBackground);
        public void PlayAnimation(SkeletonAnimation skeleton, string animation, bool loop)
            => HomeWeaponAnimation.PlayAnimation(animations, skeleton, animation, loop);
        public void OpenTradePanel() => openTrade();

        public bool HasAirship => player.HasFurniture(EFurniture.Airship);
        public IDisposable SubscribeRepair(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            void Changed(EFurniture _) => callback();
            player.OnRepairFurniture += Changed;
            return new Subscription(() => player.OnRepairFurniture -= Changed);
        }
        public IDisposable SubscribeActivities(Action callback) => activities.Subscribe(callback);
        public IEnumerable<AirshipActivityEntry> ActivityEntries() => activities.Entries();
        public void ClickActivity(AirshipActivityEntry entry) => activities.Click(entry);
        public string SubtitleKey(AirshipActivityEntry entry) => activities.SubtitleKey(entry);
        public GameObject LoadActivityBanner(AirshipActivityEntry entry, Transform slot) => activities.LoadBanner(entry, slot);
        public void ReleaseBanners() => activities.ReleaseBanners();
    }
}
