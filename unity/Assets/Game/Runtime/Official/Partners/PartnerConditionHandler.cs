// Original subscription methods; see partner-condition-handler-source-manifest.json.
using System;
using System.Collections.Generic;
using System.Linq;
using ConditionType = Zjcs.Official.Partners.PartnerConditionRules.ConditionType;

namespace Zjcs.Official.Partners
{
    // Subscription descriptors carry the original Type/composite structure.
    // Predicates remain in the migrated condition/profile/talent rules.
    public sealed class PartnerConditionHandler
    {
        public interface ICondition
        {
            ConditionType Type { get; }
            bool IsComposite { get; }
        }
        public sealed class Condition : ICondition
        {
            public ConditionType Type { get; }
            public bool IsComposite => false;
            public Condition(ConditionType type) { Type = type; }
        }
        public sealed class CompositeCondition : ICondition
        {
            public ConditionType Type { get; }
            public bool IsComposite => true;
            private readonly List<ICondition> children;
            public CompositeCondition(ConditionType type, IEnumerable<ICondition> children)
            {
                Type = type;
                this.children = children?.ToList() ?? throw new ArgumentNullException(nameof(children));
            }
            public List<ICondition> GetConditions() => new List<ICondition>(children);
        }
        private readonly object mPlayer;
        private readonly Action<Exception, ConditionType> reportFailure;
        private readonly Action onConditionMapChange;
            public readonly HashSet<ConditionType> Excluded = new HashSet<ConditionType>
    {
        ConditionType.None,
        ConditionType.Exception,
        ConditionType.Composite
    };

    private Dictionary<ConditionType, Dictionary<Action, int>> mConditionActionMap = new Dictionary<ConditionType, Dictionary<Action, int>>();

    private bool mIsInvoking;

    private Dictionary<ConditionType, Dictionary<Action, int>> mPendingRemoveMap = new Dictionary<ConditionType, Dictionary<Action, int>>();

    private Dictionary<ConditionType, Dictionary<Action, int>> mPendingAddMap = new Dictionary<ConditionType, Dictionary<Action, int>>();

        public PartnerConditionHandler(object player, Action<Exception, ConditionType> reportFailure,
            Action onConditionMapChange = null)
        {
            mPlayer = player;
            this.reportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
            this.onConditionMapChange = onConditionMapChange;
        }
        // Owner disposal follows original Destroy: clear callbacks, then unsubscribe.
        public void Destroy() { mConditionActionMap.Clear(); }

            private bool IsExcluded(ConditionType conditionType)
    {
        return Excluded.Contains(conditionType);
    }

    public void Register(ICondition condition, Action action)
    {
        ConditionType type = condition.Type;
        if (IsExcluded(type))
        {
            return;
        }
        if (condition.IsComposite)
        {
            ConditionInfoParser conditionInfoParser = new ConditionInfoParser();
            List<ICondition> list = new List<ICondition>();
            conditionInfoParser.Expand(condition, list);
            conditionInfoParser.Distinct(list);
            for (int i = 0; i < list.Count; i++)
            {
                Register(list[i], action);
            }
        }
        else
        {
            Register(type, action);
        }
    }

    private void Register(ConditionType conditionType, Action action)
    {
        if (mIsInvoking)
        {
            AddToPendingMap(conditionType, action, mPendingAddMap);
            return;
        }
        if (!mConditionActionMap.TryGetValue(conditionType, out var value))
        {
            value = new Dictionary<Action, int>();
            mConditionActionMap.Add(conditionType, value);
        }
        onConditionMapChange?.Invoke();
        if (!value.TryGetValue(action, out var value2))
        {
            value2 = 0;
            value.Add(action, value2);
        }
        value2++;
        value[action] = value2;
    }

    public void Deregister(ICondition condition, Action action)
    {
        if (mConditionActionMap.Count == 0 && mPlayer == null)
        {
            return;
        }
        ConditionType type = condition.Type;
        if (IsExcluded(type))
        {
            return;
        }
        if (condition.IsComposite)
        {
            ConditionInfoParser conditionInfoParser = new ConditionInfoParser();
            List<ICondition> list = new List<ICondition>();
            conditionInfoParser.Expand(condition, list);
            conditionInfoParser.Distinct(list);
            for (int i = 0; i < list.Count; i++)
            {
                Deregister(list[i], action);
            }
        }
        else
        {
            Deregister(type, action);
        }
    }

    private void Deregister(ConditionType conditionType, Action action)
    {
        if (mIsInvoking)
        {
            AddToPendingMap(conditionType, action, mPendingRemoveMap);
        }
        else
        {
            if (!mConditionActionMap.TryGetValue(conditionType, out var value))
            {
                return;
            }
            onConditionMapChange?.Invoke();
            if (value.TryGetValue(action, out var value2))
            {
                value2--;
                if (value2 <= 0)
                {
                    value.Remove(action);
                }
                else
                {
                    value[action] = value2;
                }
            }
        }
    }

    private void AddToPendingMap(ConditionType conditionType, Action action, Dictionary<ConditionType, Dictionary<Action, int>> map)
    {
        if (!map.TryGetValue(conditionType, out var value))
        {
            value = new Dictionary<Action, int>();
            map.Add(conditionType, value);
        }
        if (!value.TryAdd(action, 1))
        {
            value[action]++;
        }
    }

    public void DeregisterConditionType(ConditionType conditionType)
    {
        mConditionActionMap.Remove(conditionType);
    }

    private void InvokeConditionAction(ConditionType conditionType)
    {
        if (IsExcluded(conditionType) || !mConditionActionMap.TryGetValue(conditionType, out var value))
        {
            return;
        }
        mIsInvoking = true;
        Action[] array = value.Keys.ToArray();
        foreach (Action action in array)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                reportFailure(exception, conditionType);
            }
        }
        mIsInvoking = false;
        PostInvoke();
    }

    private void PostInvoke()
    {
        foreach (KeyValuePair<ConditionType, Dictionary<Action, int>> item in mPendingAddMap)
        {
            ConditionType key = item.Key;
            foreach (KeyValuePair<Action, int> item2 in item.Value)
            {
                Action key2 = item2.Key;
                for (int i = 0; i < item2.Value; i++)
                {
                    Register(key, key2);
                }
            }
        }
        mPendingAddMap.Clear();
        foreach (KeyValuePair<ConditionType, Dictionary<Action, int>> item3 in mPendingRemoveMap)
        {
            ConditionType key3 = item3.Key;
            foreach (KeyValuePair<Action, int> item4 in item3.Value)
            {
                Action key4 = item4.Key;
                for (int j = 0; j < item4.Value; j++)
                {
                    Deregister(key3, key4);
                }
            }
        }
        mPendingRemoveMap.Clear();
    }

        private sealed class ConditionInfoParser
        {
                public void Expand(ICondition condition, List<ICondition> result)
    {
        if (condition.IsComposite)
        {
            List<ICondition> conditions = (condition as CompositeCondition).GetConditions();
            for (int i = 0; i < conditions.Count; i++)
            {
                Expand(conditions[i], result);
            }
        }
        else
        {
            result.Add(condition);
        }
    }

    public void Distinct(List<ICondition> list)
    {
        if (list == null || list.Count == 0)
        {
            return;
        }
        HashSet<ConditionType> hashSet = new HashSet<ConditionType>();
        int num = -1;
        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            ICondition condition = list[i];
            ConditionType type = condition.Type;
            if (hashSet.Add(type))
            {
                num++;
                if (num != i)
                {
                    list[num] = condition;
                }
            }
        }
        if (num < count - 1)
        {
            list.RemoveRange(num + 1, count - 1 - num);
        }
    }
        }
            public void MPlayer_OnItemCountUpdate(object itemData, long preCount, object sourceData)
    {
        InvokeConditionAction(ConditionType.HasItem);
        InvokeConditionAction(ConditionType.HasItemType);
        InvokeConditionAction(ConditionType.UsedItem);
        InvokeConditionAction(ConditionType.HasOrUsedItem);
        InvokeConditionAction(ConditionType.CostItem);
        InvokeConditionAction(ConditionType.GotItems);
        InvokeConditionAction(ConditionType.GotItemTypes);
        InvokeConditionAction(ConditionType.TouchOpCount);
        InvokeConditionAction(ConditionType.ProfessionSkillRankCount);
        InvokeConditionAction(ConditionType.HasItemTypeWithQuality);
        InvokeConditionAction(ConditionType.HasManualMeetNpc);
        InvokeConditionAction(ConditionType.TreasureCollect);
        InvokeConditionAction(ConditionType.HasAnyNpcExclusiveGift);
    }

    public void MPlayer_OnNpcFriendshipLevelUpdate(int npcClassId, int fromLevel, int toLevel)
    {
        InvokeConditionAction(ConditionType.NpcFriendshipLevel);
        InvokeConditionAction(ConditionType.NpcFriendshipLevelCount);
    }

    public void MPlayer_OnAddNewNpc(int npcId, int type)
    {
        InvokeConditionAction(ConditionType.HasNpcCount);
        InvokeConditionAction(ConditionType.NpcFriendshipLevelCount);
        InvokeConditionAction(ConditionType.NpcFriendshipLevel);
        InvokeConditionAction(ConditionType.HasManualMeetNpc);
        InvokeConditionAction(ConditionType.NpcMeet);
    }

    public void MPlayer_OnSubRankUpdate(int obj)
    {
        InvokeConditionAction(ConditionType.SubRank);
        InvokeConditionAction(ConditionType.Rank);
        InvokeConditionAction(ConditionType.EquipLevel);
        InvokeConditionAction(ConditionType.SubRankBetween);
        InvokeConditionAction(ConditionType.PlayerRankAtServerRank);
        InvokeConditionAction(ConditionType.CombatRatingOrRankSuppress);
        InvokeConditionAction(ConditionType.MapRegionIsNotLimit);
    }

    public void MPlayer_OnLevelUpdate(int obj)
    {
        InvokeConditionAction(ConditionType.Level);
        InvokeConditionAction(ConditionType.LevelBetween);
        InvokeConditionAction(ConditionType.MapRegionIsNotLimit);
    }

    public void MPlayer_OnWorldMapProgressUpdate(int mapClassId, List<int> arg2)
    {
        InvokeConditionAction(ConditionType.MapUnlock);
        InvokeConditionAction(ConditionType.WorldMapProgress);
        InvokeConditionAction(ConditionType.MapGroupProgress);
        InvokeConditionAction(ConditionType.AnyMapRegionActived);
        InvokeConditionAction(ConditionType.MapRegionActived);
    }

    public void MPlayer_OnMapStatusUpdate(int mapClassId, int regionId, int preStatus, int newStatus)
    {
        InvokeConditionAction(ConditionType.AnyMapRegionActived);
        InvokeConditionAction(ConditionType.MapRegionActived);
        InvokeConditionAction(ConditionType.MapGroupExploredCount);
        InvokeConditionAction(ConditionType.MapGroupActivedRegion);
        InvokeConditionAction(ConditionType.MapGalaxyIsCleared);
        InvokeConditionAction(ConditionType.WorldMapProgress);
    }

    public void OnWorldMapActived(int activedMapClassId)
    {
        InvokeConditionAction(ConditionType.MapWorldActived);
    }
    }

    // Resolve the same selected serialized condition tree for subscriptions.
    // No condition value, comparison or reward logic lives in this projection.
    public sealed class PartnerConditionSubscriptionCatalog
    {
        private readonly Dictionary<int, Dictionary<string, object>> rows;
        private static readonly Dictionary<string, ConditionType> types = new Dictionary<string, ConditionType>
        {
            { "AlwaysCondition", ConditionType.Always },
{ "CompositeAndCondition", ConditionType.CompositeAnd },
{ "CompositeOrCondition", ConditionType.CompositeOr },
{ "HasItemCondition", ConditionType.HasItem },
{ "HasManualMeetNpcCondition", ConditionType.HasManualMeetNpc },
{ "LevelCondition", ConditionType.Level },
{ "MapRegionActivedCondition", ConditionType.MapRegionActived },
{ "MapUnlockCondition", ConditionType.MapUnlock },
{ "NoneCondition", ConditionType.None },
{ "NpcFriendshipLevelCondition", ConditionType.NpcFriendshipLevel },
{ "NpcFriendshipMaxLevelCondition", ConditionType.NpcFriendshipMaxLevel },
{ "NpcTalentTreeNodePreNodeActiveCountCondition", ConditionType.NpcTalentTreeNodePreNodeActiveCount },
        };
        public PartnerConditionSubscriptionCatalog(IEnumerable<Dictionary<string, object>> conditionRows)
        {
            rows = conditionRows.ToDictionary(row => Convert.ToInt32(row["ClassId"]),
                row => (Dictionary<string, object>)row["Condition"]);
        }
        public PartnerConditionHandler.ICondition Create(Dictionary<string, object> node)
            => Create(node, new HashSet<int>());
        private PartnerConditionHandler.ICondition Create(Dictionary<string, object> node, HashSet<int> visiting)
        {
            if (node == null) return null;
            string name = (string)node["_type"];
            if (!types.TryGetValue(name, out var type))
                throw new NotSupportedException("Condition outside selected partner subscriptions: " + name);
            if (type != ConditionType.CompositeAnd && type != ConditionType.CompositeOr)
                return new PartnerConditionHandler.Condition(type);
            var children = new List<PartnerConditionHandler.ICondition>();
            if (node.TryGetValue("Ids", out var value) && value is List<object> ids)
                foreach (var id in ids)
                {
                    int key = Convert.ToInt32(id);
                    if (!visiting.Add(key)) throw new FormatException("Cyclic partner subscription condition: " + key);
                    if (!rows.TryGetValue(key, out var child))
                        throw new FormatException("Missing partner subscription condition: " + key);
                    children.Add(Create(child, visiting));
                    visiting.Remove(key);
                }
            return new PartnerConditionHandler.CompositeCondition(type, children);
        }
    }
}
