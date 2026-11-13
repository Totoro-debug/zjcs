using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFastHangUpUI
{
    public sealed class HomeFastHangUpInfo
    {
        public int FastHangUpId, MaxUseItemCount;
        public EFurniture BindFurniture;
        public bool AutoReceive;
        public Dictionary<int, int> FastHangUpItemDic;
        public List<int> HangUpIdList, ExtraHangUpRewardList;
    }
    public sealed class HomeFastHangUpRequest
    {
        public int FastHangUpId;
        public Dictionary<int, long> ItemCost;
        public bool IsReceive, IsFree;
    }
    public sealed class HomeFastHangUpResponse { public IHomeWindowAwardMessage AwardMsg; }
    public interface IHomeFastHangUpPlayer : IHomeHangUpWindowPlayer
    {
        long GetItemAmount(int itemId);
        bool HasUnlockedHangUp(int id);
        bool HasFurniture(EFurniture furniture);
    }
    // Original selection and response consumption; the local owner supplies
    // configuration, icon lifetimes, requests and the actual progress window.
    public interface IHomeFastHangUpServices
    {
        IHomeHangUpWindowServices Window { get; }
        IHomeFastHangUpPlayer Player { get; }
        int DefaultFastItemUseCount { get; }
        double FastItemMaxCountMessageColdDown { get; }
        HomeFastHangUpInfo GetFastHangUpInfo(int id);
        bool TryGetFastHangUpItemHours(int itemId, out int hours);
        IProfessionItemInfo GetHangUpRewardItem(int id);
        ItemIcon GetIcon(int itemId, Transform parent);
        ItemIcon GetIcon(IProfessionItemInfo item, Transform parent);
        void RecycleIcon(ItemIcon icon);
        double GetHangUpUnsettledRewardCount(int id, out double seconds);
        void RequestFastHangUp(HomeFastHangUpRequest request, Action<HomeFastHangUpResponse, int> completed);
        void OpenProgress(int id, Action finished);
        void OpenAward(IHomeWindowAwardMessage message);
    }
}
