using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.HomeAirship;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeUI;

public static class HomeAirshipServicesProbe
{
    private sealed class Player : IHomeFurniturePlayer
    {
        public bool Owns;
        public event Action<EFurniture> OnRepairFurniture;
        public event Action<long> OnDebugTimeOffset;
        public bool HasFurniture(EFurniture type) => Owns && type == EFurniture.Airship;
        public bool IsMetFurnitureRepairCondition(FurnitureInfo info) => false;
        public bool IsMetFurnitureNoticeCondition(FurnitureInfo info) => false;
        public bool HasStartedStory(string name) => false;
        public void Repair(EFurniture type) => OnRepairFurniture?.Invoke(type);
    }
    private sealed class Animations : IHomeWeaponAnimationBindings
    {
        public Spine.Animation GetOrLoadSpineAnimation(ISkeletonComponent component, string name) => null;
        public void UpdateAnimation(SkeletonRenderer renderer) { }
        public void UpdateAnimation(SkeletonGraphic graphic) { }
    }
    private sealed class Activities : IOfflineAirshipActivitySource
    {
        public int clicks, releases;
        public readonly AirshipActivityEntry entry = new AirshipActivityEntry { ActivityId = 41 };
        public IDisposable Subscribe(Action callback) => new Lease();
        public IEnumerable<AirshipActivityEntry> Entries() { yield return entry; }
        public void Click(AirshipActivityEntry value) { if (value == entry) clicks++; }
        public string SubtitleKey(AirshipActivityEntry value) => "official";
        public GameObject LoadBanner(AirshipActivityEntry value, Transform slot) => slot.gameObject;
        public void ReleaseBanners() { releases++; }
    }
    private sealed class Lease : IDisposable { public void Dispose() { } }
    private static void Require(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        var texture = new Texture2D(2, 2);
        var first = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
        var second = Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero);
        var player = new Player(); var activities = new Activities(); int trade = 0, repairs = 0;
        var services = new OfflineHomeAirshipServices(player, new Animations(), activities, first, second, () => trade++);
        Require(!services.HasAirship, "unrepaired state");
        using (var sprite = services.LoadItemBackground(false)) Require(sprite.Sprite == first, "unrepaired background");
        using (var sprite = services.LoadItemBackground(true)) Require(sprite.Sprite == second, "repaired background");
        var subscription = services.SubscribeRepair(() => repairs++);
        player.Owns = true; player.Repair(EFurniture.Airship);
        Require(services.HasAirship && repairs == 1, "repair event");
        subscription.Dispose(); subscription.Dispose(); player.Repair(EFurniture.Airship);
        Require(repairs == 1, "released repair event");
        services.OpenTradePanel(); Require(trade == 1, "trade dispatch");
        foreach (var entry in services.ActivityEntries()) services.ClickActivity(entry);
        services.ReleaseBanners(); Require(activities.clicks == 1 && activities.releases == 1, "activity delegation");
        Debug.Log("HOME_AIRSHIP_SERVICES_PASS");
        UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(texture);
        EditorApplication.Exit(0);
    }
}
