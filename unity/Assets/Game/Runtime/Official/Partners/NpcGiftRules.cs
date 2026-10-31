using System;
using System.Collections.Generic;

namespace Zjcs.Official.Partners
{
    // Projections of NpcGiftItemCompare and NpcDetailPanelGiftContent.
    // Inventory and configuration ownership remain with the offline host.
    public sealed class NpcGiftChoice
    {
        public int ItemId, Quality, Friendship;
        public long Count;
        public bool Preferred;
        public double PreferenceFactor;
    }

    public static class NpcGiftRules
    {
        public static int Compare(NpcGiftChoice x, NpcGiftChoice y, bool autoSelect)
        {
            int result = y.Preferred.CompareTo(x.Preferred);
            if (result != 0) return result;
            result = autoSelect ? x.Quality.CompareTo(y.Quality) : y.Quality.CompareTo(x.Quality);
            if (result != 0) return result;
            result = autoSelect ? x.Friendship.CompareTo(y.Friendship) : y.Friendship.CompareTo(x.Friendship);
            if (result != 0 || !autoSelect) return result;
            return y.ItemId.CompareTo(x.ItemId);
        }

        // GetAllExpByDict rounds each item's whole stack, whereas auto-selection
        // uses GetGiftFriendship's rounded single-item value. Preserve both orders.
        public static int Experience(int friendship, long count, double factor)
        {
            if (friendship < 0 || count < 0 || double.IsNaN(factor) || double.IsInfinity(factor) || factor < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            long unscaled = checked(friendship * count);
            double rounded = Math.Round((double)unscaled * factor);
            if (double.IsInfinity(rounded) || rounded > int.MaxValue)
                throw new OverflowException("NPC gift friendship exceeds the official integer range.");
            return checked((int)rounded);
        }

        public static Dictionary<int, long> AutoSelect(IEnumerable<NpcGiftChoice> available,
            IReadOnlyDictionary<int, long> selected, long difference)
        {
            var result = new Dictionary<int, long>();
            if (selected != null)
                foreach (var item in selected) result.Add(item.Key, item.Value);
            if (difference <= 0) return result;
            var gifts = new List<NpcGiftChoice>(available);
            gifts.Sort((x, y) => Compare(x, y, true));
            foreach (var gift in gifts)
            {
                int single = Experience(gift.Friendship, 1, gift.PreferenceFactor);
                if (single <= 0) continue;
                result.TryGetValue(gift.ItemId, out long current);
                if (current >= gift.Count) continue;
                long needed = difference / single + (difference % single == 0 ? 0 : 1);
                long count = Math.Min(gift.Count - current, needed);
                result[gift.ItemId] = checked(current + count);
                difference = checked(difference - count * single);
                if (difference <= 0) break;
            }
            return result;
        }
    }
}
