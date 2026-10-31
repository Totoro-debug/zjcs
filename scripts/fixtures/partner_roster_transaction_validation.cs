using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;

public static class PartnerRosterTransactionValidation
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Validate(OfficialCharacterCatalog catalog, OfflineMainlineProgressSession session)
    {
        DateTime now = new DateTime(2026, 10, 31, 1, 0, 0, DateTimeKind.Utc);
        var service = new OfflineCharacterProgressionService(session, catalog, () => now);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterProperties.NpcFriendships.Clear(); player.Inventory.Clear();
            player.UnlockedMapClassIds.Clear(); player.HasManualMeetNpc = false;
        }).IsApplied, "Seed empty partner roster");
        Require(service.NpcMeetStatusOf(8406) == NpcMeetStatus.NotMeet, "Locked map hides an unavailable NPC");
        Require(!service.CanPinNpc, "Pin remains locked without manual meet or condition");
        Require(session.ApplyCharacterMutation(player => player.UnlockedMapClassIds.Add(11)).IsApplied, "Unlock map 11");
        Require(service.NpcMeetStatusOf(8406) == NpcMeetStatus.UnMeet, "Map accessibility enables CanShow");
        Require(!service.NpcRoster(true).Any(value => value.NpcClassId == 8406), "UnMeet without a signal item stays hidden");
        Require(session.InitializeMap(11, new[] { 10 }).IsApplied, "Initialize tested map region");
        Require(session.SetMapRegionProgress(11, 10, 10000).IsApplied, "Complete region exploration");
        Require(service.NpcMeetStatusOf(8406) == NpcMeetStatus.UnMeet, "Exploration completion alone is not activation");
        Require(session.RecordGoddessActivated(11, 10, "partner-region-11-10").IsApplied, "Activate actual saved region");
        Require(service.NpcMeetStatusOf(8406) == NpcMeetStatus.WaitMeet, "Saved goddess activation satisfies official regional wait condition");
        Require(service.NpcRoster(true).Any(value => value.NpcClassId == 8406)
            && !service.NpcRoster().Any(value => value.NpcClassId == 8406), "ShowAll exposes waiting NPCs only when checked");

        // 8424 needs three signal items: one is enough to display its UnMeet card.
        Require(session.ApplyCharacterMutation(player => {
            player.UnlockedMapClassIds.Add(12);
            player.Inventory.Add(new OfflineItemStack { ItemId = 1313, Count = 1 });
        }).IsApplied, "Seed partial signal item");
        Require(service.NpcMeetStatusOf(8424) == NpcMeetStatus.UnMeet
            && service.NpcRoster(true).Any(value => value.NpcClassId == 8424), "Partial signal ownership shows an unmet card");
        Require(!service.MeetNpc(8424, "too-few-signals").IsApplied && session.InventoryCount(1313) == 1,
            "Insufficient signal items cannot meet or consume stock");
        Require(session.ApplyCharacterMutation(player => {
            player.Inventory.Add(new OfflineItemStack { ItemId = 1300, Count = 3 });
            player.UnlockedMapClassIds.Clear();
        }).IsApplied, "Seed complete signals while map is locked");
        Require(service.NpcMeetStatusOf(8401) == NpcMeetStatus.CanMeet
            && service.NpcRoster().Any(value => value.NpcClassId == 8401), "CanMeet precedes CanShow even for a locked map");
        Require(service.CanPinNpc, "Official HasManualMeetNpc also accepts a currently meetable non-auto NPC");
        Require(service.MeetNpc(8401, "roster-meet").IsApplied, "Meet with official conditions");
        Require(session.InventoryCount(1300) == 0 && service.NpcMeetStatusOf(8401) == NpcMeetStatus.HadMeet,
            "Meeting and signal consumption commit together");
        var met = service.NpcRoster().Single(value => value.NpcClassId == 8401);
        Require(met.ActivateTimeTicks == now.Ticks && met.FriendshipLevel == 1, "Actual meeting saves time and official initial friendship");
        Require(service.MeetNpc(8401, "roster-meet").Status == ProgressionResultStatus.Unchanged,
            "Meeting event replay is idempotent");
        Require(!service.MeetNpc(8401, "duplicate-meet").IsApplied, "Second meeting is rejected");

        now = now.AddMinutes(1);
        Require(service.SetNpcCollected(8401, true, "pin-partner").IsApplied, "Pin met partner");
        Require(service.SetNpcCollected(8401, true, "pin-partner").Status == ProgressionResultStatus.Unchanged, "Pin replay");
        now = now.AddMinutes(1);
        Require(service.SetNpcCollected(8401, true, "pin-no-change").Status == ProgressionResultStatus.Unchanged, "Repeated pin preserves its order");
        session.Reload();
        met = service.NpcRoster().Single(value => value.NpcClassId == 8401);
        Require(met.IsCollected && met.CollectedTimeTicks == now.AddMinutes(-1).Ticks
            && met.ActivateTimeTicks == now.AddMinutes(-2).Ticks, "Reload preserves separate meet and pin times");
        Require(!service.SetNpcCollected(8406, true, "pin-unmet").IsApplied, "Unmet partners cannot be pinned");
        Require(service.SetNpcCollected(8401, false, "unpin-partner").IsApplied, "Unpin met partner");

        // Saves made before the new optional fields must remain valid and unknown-dated.
        Require(session.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.Add(
            new OfflineNpcFriendship { NpcClassId = 8406, Level = 1 })).IsApplied, "Add legacy friendship");
        session.Reload();
        Require(service.NpcRoster().Single(value => value.NpcClassId == 8406).ActivateTimeTicks == 0,
            "Legacy record never receives an invented meeting date");
        Require(!service.NpcRoster(true).Any(value => value.NpcClassId >= 11015 && value.NpcClassId <= 11022),
            "Paused collaboration NPCs remain closed");
        ValidateOrdering();
        Debug.Log("Partner roster: official map/region conditions, partial signal ownership, CanMeet priority, atomic meeting, pin unlock/order, reload and legacy dates passed.");
    }

    static void ValidateOrdering()
    {
        var pinned = new NpcRosterItem { NpcClassId = 1, MeetStatus = NpcMeetStatus.HadMeet,
            IsCollected = true, CollectedTimeTicks = 10, FriendshipExp = 1, ActivateTimeTicks = 1 };
        var strong = new NpcRosterItem { NpcClassId = 2, MeetStatus = NpcMeetStatus.HadMeet,
            FriendshipExp = 1000, ActivateTimeTicks = 1000 };
        foreach (var sort in new[] { NpcRosterSort.Friendship, NpcRosterSort.ActivateTime })
        {
            foreach (bool reverse in new[] { false, true })
                Require(NpcRosterRules.Compare(pinned, strong, sort, reverse, true) < 0,
                    "Reverse must preserve pin priority");
            Require(NpcRosterRules.Compare(pinned, strong, sort, false, false) > 0,
                "Pin has no sorting effect while locked");
        }
        strong.IsCollected = true; strong.CollectedTimeTicks = 20;
        Require(NpcRosterRules.Compare(pinned, strong, NpcRosterSort.Friendship, true, true) < 0, "Older pin stays first");
        pinned.IsCollected = false; strong.IsCollected = false;
        strong.MeetStatus = NpcMeetStatus.CanMeet;
        Require(NpcRosterRules.Compare(strong, pinned, NpcRosterSort.ActivateTime, true, true) < 0,
            "Meetable cards lead met cards independently of reverse");
        strong.MeetStatus = NpcMeetStatus.HadMeet; pinned.NpcRoleType = 1;
        Require(NpcRosterRules.Compare(pinned, strong, NpcRosterSort.Friendship, false, true) < 0, "Special roles precede higher friendship");
        pinned.NpcRoleType = 0;
        Require(NpcRosterRules.Compare(pinned, strong, NpcRosterSort.Friendship, true, true) < 0, "Reverse friendship works within a priority group");
        pinned.FriendshipExp = strong.FriendshipExp; pinned.ActivateTimeTicks = strong.ActivateTimeTicks;
        Require(NpcRosterRules.Compare(strong, pinned, NpcRosterSort.Friendship, false, true) < 0, "Class id breaks final ties descending");
        Require(!NpcRosterRules.IsVisible(NpcMeetStatus.NotMeet, true, true), "ShowAll never bypasses CanShow/CanMeet");
    }
}
