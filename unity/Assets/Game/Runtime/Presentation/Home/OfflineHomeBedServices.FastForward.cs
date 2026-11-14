using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Official.HomeFastHangUpUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class OfflineHomeBedServices : IHomeFastHangUpServices, IHomeFastHangUpPlayer
    {
        readonly HashSet<Response> fastResponses = new HashSet<Response>();
        JObject fastConfiguration;
        public Func<OfflineHomeBedAwards> FastAwards;
        public Action<int, Action> OpenFastProgress;
        JObject FastConfiguration => fastConfiguration ?? (fastConfiguration =
            JObject.Parse(OfficialHomeFastHangUpAssetCatalog.LoadDefault().rules.text));
        IHomeFastHangUpPlayer IHomeFastHangUpServices.Player => this;
        public int DefaultFastItemUseCount => (int)FastConfiguration["homeSettings"]["mDefaultFastItemUseCount"];
        public double FastItemMaxCountMessageColdDown => (double)FastConfiguration["homeSettings"]["mFastItemMaxCountMessageColdDown"];
        public long GetItemAmount(int id) => home.Profile.InventoryCount(id);
        public bool HasUnlockedHangUp(int id) => home.Session.Definitions.Any(value => value.HangUpId == id && value.Unlocked);
        public bool HasFurniture(EFurniture furniture) => home.Rules.HasFurniture((int)furniture,
            home.Session.OwnsFurniture((int)furniture));
        HomeFastHangUpInfo IHomeFastHangUpServices.GetFastHangUpInfo(int id)
        {
            var row = home.Rules.FastHangUp(id) ?? throw new InvalidOperationException("Unknown selected acceleration: " + id);
            return new HomeFastHangUpInfo { FastHangUpId = id, BindFurniture = (EFurniture)row.bindFurniture,
                MaxUseItemCount = row.maxUseItemCount, AutoReceive = row.autoReceive,
                HangUpIdList = row.hangUpIdList.ToList(), ExtraHangUpRewardList = row.extraHangUpRewardList.ToList(),
                FastHangUpItemDic = row.items.ToDictionary(value => value.itemId, value => OriginalHours(value.hours)) };
        }
        static int OriginalHours(double hours)
        {
            int value = checked((int)hours);
            if (value != hours) throw new InvalidOperationException("Original acceleration item hours must remain integral.");
            return value;
        }
        public bool TryGetFastHangUpItemHours(int id, out int hours)
        {
            foreach (var fast in home.Rules.Data.fastHangUps)
                foreach (var item in fast.items)
                    if (item.itemId == id) { hours = OriginalHours(item.hours); return true; }
            hours = 0; return false;
        }
        IProfessionItemInfo IHomeFastHangUpServices.GetHangUpRewardItem(int id)
        {
            var row = home.Rules.HangUp(id) ?? throw new InvalidOperationException("Unknown selected hang-up: " + id);
            if (row.rewardType == 3) return null;
            var reward = home.Rules.HangUpReward(row.rewardId)
                ?? throw new InvalidOperationException("Selected reward row is unavailable: " + row.rewardId);
            // Every selected single/random/auto handler returns the first rule
            // item for this UI lookup. The committed random result is separate.
            var items = home.Rules.SelectRewardRule(reward.rewardRuleId, null)?.ids;
            if (items == null || items.Length == 0) throw new InvalidOperationException("Selected preview reward is unavailable.");
            return FastAwards().GetItemInfo(items[0]);
        }
        public ItemIcon GetIcon(int id, Transform parent) => FastAwards().GetIcon(FastAwards().GetItemInfo(id), parent);
        public ItemIcon GetIcon(IProfessionItemInfo item, Transform parent)
            => FastAwards().GetIcon(FastAwards().GetItemInfo(item.ClassId), parent);
        public void RecycleIcon(ItemIcon icon) => FastAwards().RecycleIcon(icon);
        public double GetHangUpUnsettledRewardCount(int id, out double seconds) => Unsettled(id, out seconds);

        public void RequestFastHangUp(HomeFastHangUpRequest request, Action<HomeFastHangUpResponse, int> completed)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeBedServices));
            var hold = request.IsFree ? home.HoldFastForwardAwards() : null;
            Response response = null;
            try
            {
                var result = home.RequestPresentedFastForward(request.FastHangUpId, request.IsFree, request.ItemCost);
                if (result == null) { hold?.Dispose(); completed(null, 1); return; }
                if (request.IsFree)
                {
                    response = new Response { IsFast = true, Hold = hold };
                    foreach (var reward in result.Rewards) { pending.Add(reward); response.Rewards.Add(reward); }
                    fastResponses.Add(response);
                }
                Output.Refresh();
                OnFastHangUp?.Invoke(request.FastHangUpId);
                completed(new HomeFastHangUpResponse { AwardMsg = response }, 0);
            }
            catch
            {
                hold?.Dispose();
                if (response != null)
                {
                    fastResponses.Remove(response);
                    foreach (OfflineHomeCollectedReward reward in response.Rewards) pending.Remove(reward);
                }
                home.DrainHomeInventory();
                throw;
            }
        }
        public void OpenProgress(int id, Action finished) => OpenFastProgress(id, finished);
        public void OpenAward(IHomeWindowAwardMessage message)
        {
            if (!(message is Response response) || !response.IsFast || !response.Awarded || !fastResponses.Remove(response))
                throw new InvalidOperationException("An awarded acceleration response is required.");
            FastAwards().Show(response.Rewards.Cast<OfflineHomeCollectedReward>());
            foreach (OfflineHomeCollectedReward reward in response.Rewards) pending.Remove(reward);
        }
        void ReleaseFastResponses()
        {
            if (fastResponses.Count == 0) return;
            foreach (var response in fastResponses) { response.Hold?.Dispose(); response.Hold = null; }
            fastResponses.Clear();
            home.DrainHomeInventory();
        }
    }
}
