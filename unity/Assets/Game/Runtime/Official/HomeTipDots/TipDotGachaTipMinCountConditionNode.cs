// Selected official TipDotGachaTipMinCountConditionNode; parser and player access use local ports.
using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeTipDots
{
    public enum GachaType
    {
        Skill, Friend, SkillSenior, Pet, BlueTreasure, PurpleTreasure, OrangeTreasure,
        GoldTreasure, FireTreasure, WaterTreasure, WindTreasure, LightTreasure, DarkTreasure
    }

    public sealed class HomeTipGachaInfo
    {
        public GachaType GachaType;
        public int GachaClassType, ChestId, CostItemId, ShowRedDotMinCount;
    }

    public interface IHomeGachaTipServices
    {
        HomeTipGachaInfo GetGacha(GachaType type);
        bool TryGetChestCost(int chestId, int itemId, out long cost);
        bool IsGachaFunctionLimited(GachaType type);
        bool IsGachaDailyCountLimited(int classType);
        bool IsChestFunctionLimited(int chestId);
        long GetItemAmount(int itemId);
        IDisposable SubscribeChestCondition(int chestId, Action changed);
        event Action OnGacha;
        event Action<int> OnItemCountUpdate;
    }

    [Serializable]
    public class TipDotGachaTipMinCountConditionNode : TipDotConditionNodeBase
    {
        public int OverriderMinCountForShow = -1;
        public int OverriderMaxCountForShow = -1;
        public List<GachaType> GachaTypes;
        public IHomeGachaTipServices Services { get; set; }
        private readonly Dictionary<HomeTipGachaInfo, long> mCostDict = new Dictionary<HomeTipGachaInfo, long>();
        private readonly HashSet<int> mCostItemId = new HashSet<int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        public override void Init()
        {
            base.Init();
            InitCostDict();
            Services.OnGacha += OnGacha;
            Services.OnItemCountUpdate += OnItemUpdate;
            foreach (GachaType type in GachaTypes)
                subscriptions.Add(Services.SubscribeChestCondition(Services.GetGacha(type).ChestId, SetDirty));
        }

        public override void Release()
        {
            mCostDict.Clear();
            mCostItemId.Clear();
            Services.OnGacha -= OnGacha;
            Services.OnItemCountUpdate -= OnItemUpdate;
            foreach (IDisposable subscription in subscriptions) subscription.Dispose();
            subscriptions.Clear();
            base.Release();
        }

        private void OnItemUpdate(int itemId)
        {
            if (mCostItemId.Contains(itemId)) SetDirty();
        }

        private void OnGacha() => SetDirty();

        private void InitCostDict()
        {
            mCostDict.Clear();
            mCostItemId.Clear();
            foreach (GachaType type in GachaTypes)
            {
                HomeTipGachaInfo info = Services.GetGacha(type);
                if (info != null && Services.TryGetChestCost(info.ChestId, info.CostItemId, out long cost))
                {
                    mCostItemId.Add(info.CostItemId);
                    mCostDict[info] = cost;
                }
            }
        }

        public override bool CheckShowTip()
        {
            foreach (KeyValuePair<HomeTipGachaInfo, long> item in mCostDict)
            {
                HomeTipGachaInfo info = item.Key;
                long cost = item.Value;
                if (cost != 0 && !Services.IsGachaFunctionLimited(info.GachaType)
                    && !Services.IsGachaDailyCountLimited(info.GachaClassType)
                    && !Services.IsChestFunctionLimited(info.ChestId))
                {
                    long count = Services.GetItemAmount(info.CostItemId) / cost;
                    int minimum = OverriderMinCountForShow > 0 ? OverriderMinCountForShow : info.ShowRedDotMinCount;
                    if (count < minimum) return false;
                    if (OverriderMaxCountForShow > 0 && count > OverriderMaxCountForShow) return false;
                    return true;
                }
            }
            return false;
        }

        public override TipDotNodeBase Clone()
        {
            var node = new TipDotGachaTipMinCountConditionNode();
            node.Fill(this);
            return node;
        }

        public void Fill(TipDotGachaTipMinCountConditionNode source)
        {
            OverriderMinCountForShow = source.OverriderMinCountForShow;
            OverriderMaxCountForShow = source.OverriderMaxCountForShow;
            GachaTypes = source.GachaTypes == null ? null : new List<GachaType>(source.GachaTypes);
        }
    }
}
