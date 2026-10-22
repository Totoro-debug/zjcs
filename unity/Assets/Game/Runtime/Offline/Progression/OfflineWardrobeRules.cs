using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class WardrobeSkinDefinition
    {
        public int SkinId;
        public int Type;
        public int[] BlockedSkinIds = Array.Empty<int>();
        public int[] BindingSkinIds = Array.Empty<int>();
    }

    [Serializable]
    public sealed class WardrobeItemDefinition
    {
        public int ItemId;
        public int ItemType;
        public Dictionary<int, int> SkinBySex = new Dictionary<int, int>();
    }

    [Serializable]
    public sealed class WardrobeAppearanceState
    {
        public int Sex;
        public int Profession;
        public Dictionary<int, int> BaseSkinByType = new Dictionary<int, int>();
        public Dictionary<int, int> EquippedSkinBySlot = new Dictionary<int, int>();
        public Dictionary<int, int> AppendedItemByType = new Dictionary<int, int>();
        public Dictionary<int, long> WearTimeByType = new Dictionary<int, long>();
        public Dictionary<int, OfflineAppearanceDye> DyeByType = new Dictionary<int, OfflineAppearanceDye>();
    }

    public sealed class WardrobeConflictTip
    {
        public bool HasConflict;
        public bool IsCycle;
        public int TargetType;
        public List<int> LostTypes = new List<int>();
    }

    public sealed class WardrobeResolution
    {
        public Dictionary<int, int> SkinByType { get; } = new Dictionary<int, int>();
        public IReadOnlyCollection<int> EffectiveAppendedSkins { get; internal set; } = Array.Empty<int>();
        public IReadOnlyCollection<int> MutuallySuppressedTypes { get; internal set; } = Array.Empty<int>();
        public IReadOnlyCollection<int> CycleTypes { get; internal set; } = Array.Empty<int>();
    }

    public static class OfflineWardrobeRules
    {
        private const int Undecided = 0, Alive = 1, Blocked = 2, MutexLost = 3, CycleLost = 4;
        private static readonly Dictionary<int, int> ItemTypeToSkinType = new Dictionary<int, int>
        {
            { 317, 7 }, { 318, 8 }, { 319, 5 }, { 330, 6 },
            { 331, 12 }, { 332, 3 }, { 333, 11 }, { 344, 10 }
        };

        public static int SkinTypeForItemType(int itemType)
        {
            return ItemTypeToSkinType.TryGetValue(itemType, out int skinType) ? skinType : 0;
        }

        public static bool TryWear(WardrobeAppearanceState state, int itemId,
            IReadOnlyDictionary<int, long> ownedCounts, IReadOnlyDictionary<int, WardrobeItemDefinition> items,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins, ISet<string> allowedSexProfessions, long now)
        {
            if (state == null || ownedCounts == null || items == null || skins == null || allowedSexProfessions == null)
                throw new ArgumentNullException("Wardrobe state and pinned catalogs are required.");
            if (!ownedCounts.TryGetValue(itemId, out long count) || count <= 0
                || !items.TryGetValue(itemId, out WardrobeItemDefinition item)
                || !ItemTypeToSkinType.TryGetValue(item.ItemType, out int expectedType)
                || !item.SkinBySex.TryGetValue(state.Sex, out int skinId)
                || !skins.TryGetValue(skinId, out WardrobeSkinDefinition skin)
                || skin.Type != expectedType
                || !allowedSexProfessions.Contains(AllowedKey(state.Sex, state.Profession, skinId))) return false;
            if (skin.Type == 8 && !state.EquippedSkinBySlot.ContainsKey(14)) return false;
            if (state.AppendedItemByType.TryGetValue(skin.Type, out int worn) && worn == itemId) return false;
            state.AppendedItemByType[skin.Type] = itemId;
            state.WearTimeByType[skin.Type] = now;
            return true;
        }

        public static bool TakeOff(WardrobeAppearanceState state, int skinType)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            bool removed = state.AppendedItemByType.Remove(skinType);
            state.WearTimeByType.Remove(skinType);
            return removed;
        }

        public static WardrobeResolution Resolve(WardrobeAppearanceState state,
            IReadOnlyDictionary<int, WardrobeItemDefinition> items,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins, ISet<string> allowedSexProfessions)
        {
            if (state == null || items == null || skins == null || allowedSexProfessions == null)
                throw new ArgumentNullException("Wardrobe state and pinned catalogs are required.");
            var entries = new List<Entry>();
            foreach (KeyValuePair<int, int> worn in state.AppendedItemByType)
            {
                if (!items.TryGetValue(worn.Value, out WardrobeItemDefinition item)
                    || !item.SkinBySex.TryGetValue(state.Sex, out int skinId) || !skins.ContainsKey(skinId)) continue;
                state.WearTimeByType.TryGetValue(worn.Key, out long wearTime);
                entries.Add(new Entry { Type = worn.Key, Item = worn.Value, Skin = skinId, Time = wearTime });
            }
            entries.Sort((left, right) =>
            {
                int time = left.Time.CompareTo(right.Time);
                return time != 0 ? time : left.Item.CompareTo(right.Item);
            });
            Propagate(entries, skins);
            List<int> cycles = FindCycles(entries, skins);
            foreach (int index in cycles) entries[index].State = CycleLost;
            if (cycles.Count > 0) Propagate(entries, skins);

            List<int> effective = entries.Where(entry => entry.State == Alive).Select(entry => entry.Skin).ToList();
            List<int> suppressed = entries.Where(entry => entry.State == MutexLost || entry.State == CycleLost)
                .Select(entry => entry.Type).ToList();
            var result = new WardrobeResolution
            {
                EffectiveAppendedSkins = effective,
                MutuallySuppressedTypes = suppressed,
                CycleTypes = entries.Where(entry => entry.State == CycleLost).Select(entry => entry.Type).ToList()
            };

            foreach (KeyValuePair<int, int> baseSkin in state.BaseSkinByType.OrderBy(entry => entry.Key))
            {
                int type = baseSkin.Key;
                if (type == 8 && (!state.EquippedSkinBySlot.TryGetValue(14, out int secondary)
                    || secondary != 0 && !IsAllowed(secondary, state, skins, allowedSexProfessions))) continue;
                int selected = 0;
                if (!suppressed.Contains(type) && state.AppendedItemByType.TryGetValue(type, out int appendItem)
                    && items.TryGetValue(appendItem, out WardrobeItemDefinition item)
                    && item.SkinBySex.TryGetValue(state.Sex, out int appendSkin)
                    && IsAllowed(appendSkin, state, skins, allowedSexProfessions)) selected = appendSkin;
                if (selected == 0 && (type == 7 || type == 8))
                {
                    int slot = type == 7 ? 10 : 14;
                    if (state.EquippedSkinBySlot.TryGetValue(slot, out int equipped)
                        && IsAllowed(equipped, state, skins, allowedSexProfessions)) selected = equipped;
                }
                if (selected == 0) selected = baseSkin.Value;
                if (IsFiltered(effective, selected, skins)) continue;
                result.SkinByType[type] = selected;
                if (!skins.TryGetValue(selected, out WardrobeSkinDefinition definition)) continue;
                foreach (int binding in definition.BindingSkinIds ?? Array.Empty<int>())
                    if (skins.TryGetValue(binding, out WardrobeSkinDefinition bound)) result.SkinByType[bound.Type] = binding;
            }
            return result;
        }

        /// <summary>
        /// Original <c>TryGetConflictTip</c>: resolve the effective skins before and after wearing the
        /// item and report the part that would be hidden, or a mutex cycle that suppresses the item.
        /// </summary>
        public static WardrobeConflictTip PreviewWear(WardrobeAppearanceState state, int itemId, long now,
            IReadOnlyDictionary<int, WardrobeItemDefinition> items,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins, ISet<string> allowedSexProfessions)
        {
            var result = new WardrobeConflictTip();
            if (state == null || items == null || skins == null || allowedSexProfessions == null
                || !items.TryGetValue(itemId, out WardrobeItemDefinition item)
                || !item.SkinBySex.TryGetValue(state.Sex, out int skinId)
                || !skins.TryGetValue(skinId, out WardrobeSkinDefinition skin)) return result;
            result.TargetType = skin.Type;
            WardrobeResolution before = Resolve(state, items, skins, allowedSexProfessions);
            var afterState = new WardrobeAppearanceState
            {
                Sex = state.Sex,
                Profession = state.Profession,
                BaseSkinByType = new Dictionary<int, int>(state.BaseSkinByType),
                EquippedSkinBySlot = new Dictionary<int, int>(state.EquippedSkinBySlot),
                AppendedItemByType = new Dictionary<int, int>(state.AppendedItemByType),
                WearTimeByType = new Dictionary<int, long>(state.WearTimeByType),
                DyeByType = new Dictionary<int, OfflineAppearanceDye>(state.DyeByType)
            };
            afterState.AppendedItemByType[skin.Type] = itemId;
            afterState.WearTimeByType[skin.Type] = now;
            WardrobeResolution after = Resolve(afterState, items, skins, allowedSexProfessions);
            if (after.CycleTypes.Contains(skin.Type))
            {
                result.HasConflict = true;
                result.IsCycle = true;
                result.LostTypes.AddRange(after.CycleTypes);
                return result;
            }
            if (after.MutuallySuppressedTypes.Contains(skin.Type))
            {
                result.HasConflict = true;
                result.IsCycle = true;
                foreach (KeyValuePair<int, int> worn in state.AppendedItemByType)
                {
                    if (worn.Key == skin.Type || !items.TryGetValue(worn.Value, out WardrobeItemDefinition other)
                        || !other.SkinBySex.TryGetValue(state.Sex, out int otherSkin)
                        || !skins.TryGetValue(otherSkin, out WardrobeSkinDefinition otherDefinition)) continue;
                    if (!before.EffectiveAppendedSkins.Contains(otherSkin)) continue;
                    if (Filters(otherSkin, skinId, skins)) result.LostTypes.Add(worn.Key);
                }
                return result;
            }
            foreach (KeyValuePair<int, int> worn in state.AppendedItemByType)
            {
                if (worn.Key == skin.Type || !items.TryGetValue(worn.Value, out WardrobeItemDefinition other)
                    || !other.SkinBySex.TryGetValue(state.Sex, out int otherSkin)) continue;
                if (!before.EffectiveAppendedSkins.Contains(otherSkin)) continue;
                if (before.SkinByType.TryGetValue(worn.Key, out int beforeSkin) && beforeSkin != otherSkin) continue;
                if (after.EffectiveAppendedSkins.Contains(otherSkin)) continue;
                result.LostTypes.Add(worn.Key);
            }
            result.LostTypes = result.LostTypes.Distinct().ToList();
            result.HasConflict = result.LostTypes.Count > 0;
            return result;
        }

        public static string AllowedKey(int sex, int profession, int skinId)
        {
            return sex + ":" + profession + ":" + skinId;
        }

        private static bool IsAllowed(int id, WardrobeAppearanceState state,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins, ISet<string> allowed)
        {
            return id > 0 && skins.ContainsKey(id) && allowed.Contains(AllowedKey(state.Sex, state.Profession, id));
        }

        private static bool IsFiltered(IEnumerable<int> effective, int target,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins)
        {
            return effective.Any(current => Filters(current, target, skins));
        }

        private static bool Filters(int current, int target,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins)
        {
            return skins.TryGetValue(current, out WardrobeSkinDefinition skin)
                && Array.IndexOf(skin.BlockedSkinIds ?? Array.Empty<int>(), target) >= 0;
        }

        private static bool Blocks(IReadOnlyList<Entry> entries, int blocker, int target,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins)
        {
            if (blocker == target || !Filters(entries[blocker].Skin, entries[target].Skin, skins)) return false;
            return blocker < target || !Filters(entries[target].Skin, entries[blocker].Skin, skins);
        }

        private static void Propagate(IReadOnlyList<Entry> entries,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins)
        {
            bool changed;
            do
            {
                changed = false;
                for (int target = 0; target < entries.Count; target++)
                {
                    if (entries[target].State != Undecided) continue;
                    bool pending = false;
                    int state = Alive;
                    for (int blocker = 0; blocker < entries.Count; blocker++)
                    {
                        if (!Blocks(entries, blocker, target, skins)) continue;
                        if (entries[blocker].State == Alive)
                        {
                            state = Filters(entries[target].Skin, entries[blocker].Skin, skins) ? MutexLost : Blocked;
                            break;
                        }
                        if (entries[blocker].State == Undecided) pending = true;
                    }
                    if (state == Alive && pending) state = Undecided;
                    if (state != Undecided) { entries[target].State = state; changed = true; }
                }
            } while (changed);
        }

        private static List<int> FindCycles(IReadOnlyList<Entry> entries,
            IReadOnlyDictionary<int, WardrobeSkinDefinition> skins)
        {
            var cycles = new List<int>();
            for (int start = 0; start < entries.Count; start++)
            {
                if (entries[start].State != Undecided) continue;
                var visited = new HashSet<int> { start };
                var queue = new Queue<int>();
                queue.Enqueue(start);
                bool cycle = false;
                while (queue.Count > 0 && !cycle)
                {
                    int current = queue.Dequeue();
                    for (int next = 0; next < entries.Count; next++)
                    {
                        if (entries[next].State != Undecided || !Blocks(entries, current, next, skins)) continue;
                        if (next == start) { cycle = true; break; }
                        if (visited.Add(next)) queue.Enqueue(next);
                    }
                }
                if (cycle) cycles.Add(start);
            }
            return cycles;
        }

        private sealed class Entry
        {
            public int Type, Item, Skin, State;
            public long Time;
        }
    }
}
