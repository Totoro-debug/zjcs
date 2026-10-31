using System;

namespace Zjcs.Official.Partners
{
    // Common/NpcMeetStatus.cs: numeric order is also the first sorting key.
    public enum NpcMeetStatus { UnMeet, WaitMeet, HadMeet, CanMeet, NotMeet }
    public enum NpcRosterSort { Friendship, ActivateTime }

    public sealed class NpcRosterItem
    {
        public int NpcClassId;
        public int NpcRoleType;
        public NpcMeetStatus MeetStatus;
        public int FriendshipLevel;
        public long FriendshipExp;
        public bool IsCollected;
        public long ActivateTimeTicks;
        public long CollectedTimeTicks;
        public int ExclusiveGiftId;
        public bool IsAutoMeet;
    }

    public static class NpcRosterRules
    {
        // NpcHelper.GetNpcMeetStatus: CanMeet deliberately precedes CanShow.
        public static NpcMeetStatus MeetStatus(bool hasConfig, bool hasRecord,
            bool canMeet, bool canShow, bool waitMeet)
        {
            if (!hasConfig) return NpcMeetStatus.NotMeet;
            if (hasRecord) return NpcMeetStatus.HadMeet;
            if (canMeet) return NpcMeetStatus.CanMeet;
            if (!canShow) return NpcMeetStatus.NotMeet;
            return waitMeet ? NpcMeetStatus.WaitMeet : NpcMeetStatus.UnMeet;
        }

        // NpcPanelNpcList.InitNpcList: only the FIRST exclusive item is checked.
        public static bool IsVisible(NpcMeetStatus status, bool showAll, bool ownsExclusiveGift)
        {
            switch (status)
            {
                case NpcMeetStatus.HadMeet:
                case NpcMeetStatus.CanMeet: return true;
                case NpcMeetStatus.WaitMeet: return showAll;
                case NpcMeetStatus.UnMeet: return showAll && ownsExclusiveGift;
                default: return false;
            }
        }

        // NpcComparer: reversing the selected key never reverses the fixed priorities.
        public static int Compare(NpcRosterItem x, NpcRosterItem y, NpcRosterSort sort,
            bool reverse, bool pinUnlocked)
        {
            if (x == null || y == null) throw new ArgumentNullException(x == null ? nameof(x) : nameof(y));
            int result = y.MeetStatus.CompareTo(x.MeetStatus);
            if (result != 0) return result;
            bool xPinned = pinUnlocked && x.IsCollected, yPinned = pinUnlocked && y.IsCollected;
            result = yPinned.CompareTo(xPinned);
            if (result != 0) return result;
            result = (xPinned ? x.CollectedTimeTicks : 0).CompareTo(yPinned ? y.CollectedTimeTicks : 0);
            if (result != 0) return result;
            result = y.NpcRoleType.CompareTo(x.NpcRoleType);
            if (result != 0) return result;
            switch (sort)
            {
                case NpcRosterSort.Friendship:
                    result = reverse ? x.FriendshipExp.CompareTo(y.FriendshipExp) : y.FriendshipExp.CompareTo(x.FriendshipExp);
                    if (result == 0) result = y.ActivateTimeTicks.CompareTo(x.ActivateTimeTicks);
                    break;
                case NpcRosterSort.ActivateTime:
                    result = reverse ? x.ActivateTimeTicks.CompareTo(y.ActivateTimeTicks) : y.ActivateTimeTicks.CompareTo(x.ActivateTimeTicks);
                    if (result == 0) result = y.FriendshipExp.CompareTo(x.FriendshipExp);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(sort));
            }
            return result != 0 ? result : y.NpcClassId.CompareTo(x.NpcClassId);
        }
    }
}
