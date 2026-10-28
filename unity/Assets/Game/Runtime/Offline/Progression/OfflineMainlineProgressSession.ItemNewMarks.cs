using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Skills;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineMainlineProgressSession
    {
        public static void EndPlayerSession() => OfflineSkillItemNewMarks.EndPlayerSession();
        private OfflineSkillItemNewMarks itemNewMarks;
        public bool IsNewSessionItem(int itemId) => itemNewMarks.IsNewItem(itemId);
        public void RemoveNewSessionItem(int itemId) => itemNewMarks.RemoveNewItem(itemId);

        private void UpdateSessionItemMarks(OfflinePlayerProgress previous, OfflinePlayerProgress next)
        {
            Dictionary<int, long> Counts(OfflinePlayerProgress player) {
                var counts = player.Inventory.GroupBy(value => value.ItemId)
                    .ToDictionary(group => group.Key, group => group.Sum(value => value.Count));
                foreach (var skill in player.Skills.Skills)
                    if (skill.Rank > 0) counts[skill.ClassId] = 1;
                return counts;
            }
            var before = Counts(previous);
            var after = Counts(next);
            foreach (int item in before.Keys.Union(after.Keys)) {
                before.TryGetValue(item, out long preCount);
                after.TryGetValue(item, out long count);
                itemNewMarks.OnItemCountUpdate(item, preCount, count);
            }
        }
    }
}
