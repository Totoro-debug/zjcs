using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.State;
using Zjcs.Offline.World;

namespace Zjcs.Editor
{
    public static partial class DailyDungeonModuleRuleAcceptance
    {
        private static void ValidateRoomSelection(ProfileSlotCatalog slots, DailyDungeonRuntimeData original)
        {
            var catalog = JsonUtility.FromJson<DailyDungeonRuntimeData>(JsonUtility.ToJson(original));
            string slot = slots.CreateSlot();
            var module = DailyDungeonModule.Open(slots, slot, catalog);
            if (!module.TryBegin(11, 0, _ => true, (_, __) => true, (_, __, ___) => true,
                0, 1700000000, out string error)) throw new InvalidOperationException(error);
            var branch = module.FindMap(module.ActiveRun.mapGroupId).roomArray.Select(module.FindRoom)
                .First(room => room != null && room.nextRoomIds?.Length >= 2);
            int first = branch.nextRoomIds[0], second = branch.nextRoomIds[1];
            branch.nextRoomIds = new[] { first, second };
            module.FindRoom(first).type = 2;
            module.FindRoom(second).type = 1;
            catalog.settings.DailyDungeonRoomPriorityList = new[] { 1, 2 };
            long now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            var fixture = JsonUtility.FromJson<DailyDungeonSave>(JsonUtility.ToJson(module.Save));
            fixture.activeRun.currentRoomId = branch.id;
            fixture.activeRun.clearedRoomIds.Clear();
            string path = Path.Combine(slots.GetSlotDirectory(slot), "daily-dungeon-save.json");
            File.WriteAllText(path, JsonUtility.ToJson(fixture));
            module = DailyDungeonModule.Open(slots, slot, catalog);
            if (!module.RecordRoomCleared(branch.id, string.Empty, now))
                throw new InvalidOperationException("Selection fixture did not clear the current room.");
            var selecting = JsonUtility.ToJson(module.Save);
            if (module.BuildRoomSelectionState(now).NextRoomId != second
                || module.BuildRouteHudModel(now).RoomSelection.RemainingSeconds != 10
                || module.TrySelectTimedOutRoom(now + 9 * TimeSpan.TicksPerSecond, out _))
                throw new InvalidOperationException("Official priority or countdown boundary was not preserved.");
            module = DailyDungeonModule.Open(slots, slot, catalog);
            module.BeginRoomSelection(now + 5 * TimeSpan.TicksPerSecond);
            if (module.BuildRoomSelectionState(now + 5 * TimeSpan.TicksPerSecond).RemainingSeconds != 5
                || !module.TrySelectTimedOutRoom(now + 10 * TimeSpan.TicksPerSecond, out int chosen)
                || chosen != second || module.ActiveRun.currentRoomId != second
                || module.ActiveRun.nextRoomSelectionStartedAtUtcTicks != 0
                || module.TrySelectTimedOutRoom(now + 20 * TimeSpan.TicksPerSecond, out _))
                throw new InvalidOperationException("Cold countdown recovery or automatic selection was not exactly once.");
            File.WriteAllText(path, selecting);
            module = DailyDungeonModule.Open(slots, slot, catalog);
            if (!module.ChooseNextRoom(first)
                || module.TrySelectTimedOutRoom(now + 20 * TimeSpan.TicksPerSecond, out _)
                || module.ActiveRun.currentRoomId != first)
                throw new InvalidOperationException("Manual selection did not immediately cancel the old timeout.");
            File.WriteAllText(path, selecting);
            module = DailyDungeonModule.Open(slots, slot, catalog);
            module.FindRoom(first).type = 1;
            if (module.BuildRoomSelectionState(now).NextRoomId != first)
                throw new InvalidOperationException("Equal room types did not retain configuration order.");
            var legacy = JsonUtility.FromJson<DailyDungeonSave>(selecting);
            legacy.activeRun.nextRoomSelectionStartedAtUtcTicks = 0;
            File.WriteAllText(path, JsonUtility.ToJson(legacy));
            module = DailyDungeonModule.Open(slots, slot, catalog);
            if (module.BuildRoomSelectionState(now).HasStarted || !module.BeginRoomSelection(now)
                || module.BuildRoomSelectionState(now).RemainingSeconds != 10)
                throw new InvalidOperationException("A legacy route did not initialize one selection countdown.");
            Debug.Log("Dungeon room selection passed: priority, configuration-order ties, timeout, immediate manual choice and cold recovery.");
        }
    }
}
