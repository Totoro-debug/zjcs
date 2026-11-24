using System;
using System.Collections.Generic;
using Zjcs.Official.HomeUI;

namespace Zjcs.Official.HomeTipDots
{
    public interface IHomeTipDotNode
    {
        IHomeTipDotServices Services { get; set; }
    }

    public sealed class HomeTipFastHangUpInfo
    {
        public int FastHangUpId;
        public List<int> HangUpIdList;
        public Dictionary<int, int> FastHangUpItemDic;
    }

    public sealed class HomeTipItemInfo
    {
        public int ClassId;
    }

    public sealed class HomeTipItemData
    {
        public HomeTipItemInfo Info;
    }

    public interface IHomeTipDotPlayer
    {
        bool CanFreeFastHangUp(int fastHangUpId);
        long GetItemAmount(int itemId);
        bool HasUnlockedHangUp(int hangUpId);
        event Action<int> OnHangUpUnlock;
        event Action<int> OnFastHangUp;
        event Action<long> OnDebugTimeOffset;
        event Action<HomeTipItemData, long, object> OnItemCountUpdate;
    }

    public interface IHomeTipDotServices
    {
        IHomeTipDotPlayer Player { get; }
        bool TryGetFastHangUpInfo(EFurniture furniture, out List<HomeTipFastHangUpInfo> list);
        bool IsFastHangUpItem(int itemId);
        EFurniture GetHangUpBindFurniture(int hangUpId);
        IDisposable SubscribeEnterNewDay(Action callback);
    }
}
