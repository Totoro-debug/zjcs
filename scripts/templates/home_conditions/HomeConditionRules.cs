using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;
using SubRank = Zjcs.Official.HomeConditions.HomeConditionRules.SubRank;
using Rank = Zjcs.Official.HomeConditions.HomeConditionRules.Rank;
using Profession = Zjcs.Official.HomeConditions.HomeConditionRules.Profession;

namespace Zjcs.Official.HomeConditions
{
    public readonly struct HomeConditionItem
    {
        public readonly int ItemType;
        public readonly long Count;
        public HomeConditionItem(int itemType, long count) { ItemType=itemType; Count=count; }
    }

    public interface IHomeConditionPlayer : IPartnerConditionPlayer
    {
        int SubRank { get; }
        int GetHighestUnlockedProfessionRank();
        IEnumerable<HomeConditionItem> GetInventoryItems();
        int GetStoryCount(string storyKey);
        bool IsAnyGoddessActived();
        int GetStageCount(int stageId);
    }

    public interface IHomeProfessionConditionPlayer : IHomeConditionPlayer
    {
        int Profession { get; }
    }

    public sealed class HomeConditionRules
    {
        // Explicit snapshot of the three fields consumed by the original predicate.
        public sealed class PlanesMergeActivityShareSyncData
        {
            public bool IsDisabled;
            public DateTime StartTime, EndTime;
        }
        private readonly Func<PlanesMergeActivityShareSyncData> mergeActivity;
        private readonly Func<DateTime> activityClock;
        private readonly PartnerConditionRules partnerRules;
        private readonly Dictionary<int, Dictionary<string, object>> conditionRows;
        private readonly Dictionary<int, ICondition> cached = new Dictionary<int, ICondition>();
        private readonly Func<IReadOnlyDictionary<string, object>, bool?> policy;
        private readonly ProfessionBaseInfoParser professionBaseInfoParser;

        public HomeConditionRules(IEnumerable<Dictionary<string, object>> conditions,
            PartnerConditionRules partnerRules, Func<IReadOnlyDictionary<string, object>, bool?> policy = null,
            IEnumerable<Dictionary<string, object>> professions = null,
            Func<PlanesMergeActivityShareSyncData> mergeActivity = null, Func<DateTime> activityClock = null)
        {
            this.mergeActivity=mergeActivity;
            this.activityClock=activityClock;
            this.partnerRules=partnerRules ?? throw new ArgumentNullException(nameof(partnerRules));
            this.policy=policy;
            professionBaseInfoParser=professions==null ? null : new ProfessionBaseInfoParser(professions);
            conditionRows=conditions.ToDictionary(row=>Number(row,"ClassId"), row=>(Dictionary<string,object>)row["Condition"]);
        }

        public bool IsMet(Dictionary<string,object> node, IHomeConditionPlayer player)
        {
            if (player==null) throw new ArgumentNullException(nameof(player));
            return Create(node).IsMet(new DataHub(new Player(player)));
        }

        public bool IsMet(int id, IHomeConditionPlayer player)
        {
            if (player==null) throw new ArgumentNullException(nameof(player));
            var condition=GetCondition(id);
            if (condition==null) throw new KeyNotFoundException("Original home condition is unavailable: "+id);
            return condition.IsMet(new DataHub(new Player(player)));
        }

        private ICondition GetCondition(int id)
        {
            if (cached.TryGetValue(id,out var condition)) return condition;
            if (!conditionRows.TryGetValue(id,out var row)) return null;
            condition=Create(row);cached.Add(id,condition);return condition;
        }

        private ICondition Create(Dictionary<string,object> node)
        {
            if (node==null) throw new ArgumentNullException(nameof(node));
            bool? result=policy?.Invoke(node);
            if (result.HasValue) return new PolicyCondition(this,result.Value);
            switch ((string)node["_type"])
            {
                case "NoneCondition": case "AlwaysCondition": case "LevelCondition":
                case "HasItemCondition": case "MapUnlockCondition": case "MapRegionActivedCondition":
                case "HasManualMeetNpcCondition": return new PartnerCondition(this,node);
                case "CompositeAndCondition": return new CompositeAndCondition(this) { Ids=Ids(node) };
                case "CompositeOrCondition": return new CompositeOrCondition(this) { Ids=Ids(node) };
                case "HasItemTypeCondition": return new HasItemTypeCondition(this) { ItemType=(ItemType)Number(node,"ItemType"),Count=Number(node,"Count") };
                case "RankCondition": return new RankCondition(this) { Rank=(Rank)Number(node,"Rank"),Op=(string)node["Op"] };
                case "SubRankCondition": return new SubRankCondition(this) { SubRank=(SubRank)Number(node,"SubRank"),Op=(string)node["Op"] };
                case "MaxProfessionRankCondition": return new MaxProfessionRankCondition(this) { Rank=Number(node,"Rank"),Op=(string)node["Op"] };
                case "StoryCountCondition": return new StoryCountCondition(this) { StoryKey=(string)node["StoryKey"],Count=Number(node,"Count"),Op=(string)node["Op"] };
                case "AnyMapGoddessActivedCondition": return new AnyMapGoddessActivedCondition(this);
                case "StagePassCondition": return new StagePassCondition(this) { StageId=Number(node,"StageId"),Op=(string)node["Op"] };
                case "PlayerProfessionCondition": return new PlayerProfessionCondition(this) { Profession=(Profession)Number(node,"Profession") };
                case "PlayerProfessionFactionCondition": return new PlayerProfessionFactionCondition(this) { Profession=(Profession)Number(node,"Profession") };
                case "RegionCodeCondition": return new RegionCodeCondition(this) {
                    RegionCodes=node["RegionCodes"]==null ? null : ((List<object>)node["RegionCodes"]).Select(value=>(RegionCodeEnum)Convert.ToInt32(value)).ToList(),
                    Not=(bool)node["Not"] };
                case "PlanesMergeActivityEnableCondition": return new PlanesMergeActivityEnableCondition(this);
                default: throw new NotSupportedException("Condition outside migrated home closure: "+node["_type"]);
            }
        }

        private static int Number(Dictionary<string,object> row,string field)=>Convert.ToInt32(row[field]);
        private static List<int> Ids(Dictionary<string,object> row)=>row["Ids"]==null ? null : ((List<object>)row["Ids"]).Select(Convert.ToInt32).ToList();

        private abstract class ICondition
        {
            protected readonly HomeConditionRules configuration;
            protected ICondition(HomeConditionRules configuration) { this.configuration=configuration; }
            public abstract ConditionType Type { get; }
            public virtual bool IsComposite=>false;
            public abstract bool IsMet(DataHub hub);
            public virtual (long current,long target) GetProgress(DataHub hub)=>throw new NotSupportedException();
        }

        private sealed class PartnerCondition : ICondition
        {
            private readonly Dictionary<string,object> node;
            internal PartnerCondition(HomeConditionRules configuration,Dictionary<string,object> node):base(configuration) { this.node=node; }
            public override ConditionType Type=>throw new NotSupportedException("Delegated leaf type is not used by home evaluation.");
            public override bool IsMet(DataHub hub)
            {
                hub.TryGetPlayer(out var player);
                return configuration.partnerRules.IsMet(node,player.Source);
            }
        }

        private sealed class PolicyCondition : ICondition
        {
            private readonly bool value;
            internal PolicyCondition(HomeConditionRules configuration,bool value):base(configuration) { this.value=value; }
            public override ConditionType Type=>throw new NotSupportedException("Offline policy leaf has no replacement official type.");
            public override bool IsMet(DataHub hub)=>value;
        }

        private sealed class Player
        {
            internal readonly IHomeConditionPlayer Source;
            internal Player(IHomeConditionPlayer source) { Source=source; }
            private ItemManager itemManager;
            public ItemManager ItemManager=>itemManager ?? (itemManager=new ItemManager(Source));
            public SubRank SubRank=>(SubRank)Source.SubRank;
            public Profession Profession=>(Profession)((Source as IHomeProfessionConditionPlayer)?.Profession
                ?? throw new InvalidOperationException("The current profession owner is required by this home condition."));
            public int GetHighestUnlockedProfessionRank()=>Source.GetHighestUnlockedProfessionRank();
            public int GetStoryCount(string key)=>Source.GetStoryCount(key);
            public bool IsAnyGoddessActived()=>Source.IsAnyGoddessActived();
            public int GetStageCount(int id)=>Source.GetStageCount(id);
        }

        private sealed class ItemManager
        {
            internal readonly Dictionary<ItemType,List<PlayerItemDataWrap>> ItemTypeDict;
            internal ItemManager(IHomeConditionPlayer source)
            {
                ItemTypeDict=source.GetInventoryItems().GroupBy(item=>(ItemType)item.ItemType)
                    .ToDictionary(group=>group.Key,group=>group.Select(item=>new PlayerItemDataWrap { Count=item.Count }).ToList());
            }
        }
        private sealed class PlayerItemDataWrap { public long Count; }
        private abstract class PlayerTaskDataWrap
        {
            public abstract bool TryGetAddtionParam<T>(out T value);
        }

        /*DATA_HUB*/
        /*CONDITION_DATA_TYPE*/
        /*CONDITION_TYPE*/
        /*SUB_RANK*/
        /*RANK*/
        /*ITEM_TYPE*/
        /*PROFESSION*/
        /*REGION_CODE*/
        /*PROFESSION_PARSER*/
        /*CONDITIONS*/

        private static class Formula
        {
            /*COMPARE*/
        }
    }

    internal static class HomeConditionEnumExt
    {
        /*TO_RANK*/
    }
}
