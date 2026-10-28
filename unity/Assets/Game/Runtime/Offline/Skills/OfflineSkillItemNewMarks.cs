using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Skills
{
    // ItemNewMarkManager.PlayerUpdateHandler / MainPlayer_OnItemCountUpdate /
    // RemoveNewItem. The offline aggregate item ID replaces the online ItemIndex.
    // This state belongs to the current player session and is never serialized.
    public sealed class OfflineSkillItemNewMarks
    {
        private static string currentPlayer;
        private static OfflineSkillItemNewMarks current;
        private readonly HashSet<int> newItems = new HashSet<int>();

        public static void EndPlayerSession()
        {
            current?.newItems.Clear();
            current = null;
            currentPlayer = null;
        }

        public static OfflineSkillItemNewMarks SelectPlayer(string identity)
        {
            if (string.IsNullOrEmpty(identity)) throw new ArgumentException("A local player identity is required.");
            if (!string.Equals(currentPlayer, identity, StringComparison.OrdinalIgnoreCase)) {
                currentPlayer = identity;
                current = new OfflineSkillItemNewMarks();
            }
            return current;
        }

        public void OnItemCountUpdate(int itemId, long previous, long count)
        {
            if (previous == 0 && count > 0) newItems.Add(itemId);
            else if (previous > 0 && count == 0) newItems.Remove(itemId);
        }

        public bool IsNewItem(int itemId) => newItems.Contains(itemId);
        public void RemoveNewItem(int itemId) => newItems.Remove(itemId);
    }
}
