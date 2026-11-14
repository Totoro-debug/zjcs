// Selected official ConditionHandler/ConditionInfoParser; local lifetime and event ports.
using System;
using System.Collections.Generic;
using System.Linq;
using ConditionType = Zjcs.Official.HomeConditions.HomeConditionRules.ConditionType;
namespace Zjcs.Official.HomeConditions
{
    // Values and composite children retain their selected configuration identity.
    public interface IHomeConditionNotification
    {
        ConditionType Type { get; }
        bool IsComposite { get; }
        List<IHomeConditionNotification> GetConditions();
    }
    // Original registry and callback order. The local player owner forwards the
    // corresponding committed player events and destroys this registry on exit.
    public sealed class HomeConditionNotifications
    {
        private IHomeConditionPlayer mPlayer;
        private readonly Action<Exception> reportException;
        private readonly Action onConditionMapChange;
        private readonly ConditionInfoParser conditionInfoParser = new ConditionInfoParser();
        private bool mIsInvoking;
        private Dictionary<ConditionType,Dictionary<Action,int>> mConditionActionMap = new Dictionary<ConditionType,Dictionary<Action,int>>();
        private Dictionary<ConditionType,Dictionary<Action,int>> mPendingRemoveMap = new Dictionary<ConditionType,Dictionary<Action,int>>();
        private Dictionary<ConditionType,Dictionary<Action,int>> mPendingAddMap = new Dictionary<ConditionType,Dictionary<Action,int>>();
        public HomeConditionNotifications(IHomeConditionPlayer player, Action<Exception> reportException, Action onConditionMapChange=null)
        {
            mPlayer=player??throw new ArgumentNullException(nameof(player));
            this.reportException=reportException??throw new ArgumentNullException(nameof(reportException));
            this.onConditionMapChange=onConditionMapChange;
        }
        public void Destroy() { mConditionActionMap.Clear(); mPlayer=null; }
    public readonly HashSet<ConditionType> Excluded = new HashSet<ConditionType>
    {
        ConditionType.None,
        ConditionType.Exception,
        ConditionType.Composite
    };
    private bool IsExcluded(ConditionType conditionType)
    {
        return Excluded.Contains(conditionType);
    }

    public void Register(IHomeConditionNotification condition, Action action)
    {
        ConditionType type = condition.Type;
        if (IsExcluded(type))
        {
            return;
        }
        if (condition.IsComposite)
        {
            List<IHomeConditionNotification> list = new List<IHomeConditionNotification>();
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

    public void Deregister(IHomeConditionNotification condition, Action action)
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
            List<IHomeConditionNotification> list = new List<IHomeConditionNotification>();
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
            catch (Exception exception) { reportException(exception); }
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

    public void NotifyLevelUpdate()
    {
        InvokeConditionAction(ConditionType.Level);
        InvokeConditionAction(ConditionType.LevelBetween);
        InvokeConditionAction(ConditionType.MapRegionIsNotLimit);
    }

    public void NotifySubRankUpdate()
    {
        InvokeConditionAction(ConditionType.SubRank);
        InvokeConditionAction(ConditionType.Rank);
        InvokeConditionAction(ConditionType.EquipLevel);
        InvokeConditionAction(ConditionType.SubRankBetween);
        InvokeConditionAction(ConditionType.PlayerRankAtServerRank);
        InvokeConditionAction(ConditionType.CombatRatingOrRankSuppress);
        InvokeConditionAction(ConditionType.MapRegionIsNotLimit);
    }

    public void NotifyWorldMapProgressUpdate()
    {
        InvokeConditionAction(ConditionType.MapUnlock);
        InvokeConditionAction(ConditionType.WorldMapProgress);
        InvokeConditionAction(ConditionType.MapGroupProgress);
        InvokeConditionAction(ConditionType.AnyMapRegionActived);
        InvokeConditionAction(ConditionType.MapRegionActived);
    }

    public void NotifyItemCountUpdate()
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

    public void NotifyMapDataAdd()
    {
        InvokeConditionAction(ConditionType.MapEnter);
    }

    public void NotifyMapStatusUpdate()
    {
        InvokeConditionAction(ConditionType.AnyMapRegionActived);
        InvokeConditionAction(ConditionType.MapRegionActived);
        InvokeConditionAction(ConditionType.MapGroupExploredCount);
        InvokeConditionAction(ConditionType.MapGroupActivedRegion);
        InvokeConditionAction(ConditionType.MapGalaxyIsCleared);
        InvokeConditionAction(ConditionType.WorldMapProgress);
    }

    public void NotifyStoryCountUpdate()
    {
        InvokeConditionAction(ConditionType.StoryCount);
    }

    public void NotifyStoryStatusUpdate()
    {
        InvokeConditionAction(ConditionType.StoryStatus);
    }

    public void NotifyProfessionPromote()
    {
        InvokeConditionAction(ConditionType.ProfessionUnlock);
        InvokeConditionAction(ConditionType.PlayerProfession);
        InvokeConditionAction(ConditionType.MaxProfessionRank);
    }

    public void NotifyHangUpLevelUp()
    {
        InvokeConditionAction(ConditionType.HangUpLevel);
        InvokeConditionAction(ConditionType.HangUpLevelCount);
        InvokeConditionAction(ConditionType.HangUpTotalLevel);
    }

    public void NotifyHangUpUnlock()
    {
        InvokeConditionAction(ConditionType.HangUpLevel);
        InvokeConditionAction(ConditionType.HangUpLevelCount);
        InvokeConditionAction(ConditionType.HangUpTotalLevel);
    }

    public void NotifyAddNewNpc()
    {
        InvokeConditionAction(ConditionType.HasNpcCount);
        InvokeConditionAction(ConditionType.NpcFriendshipLevelCount);
        InvokeConditionAction(ConditionType.NpcFriendshipLevel);
        InvokeConditionAction(ConditionType.HasManualMeetNpc);
        InvokeConditionAction(ConditionType.NpcMeet);
    }

    public void NotifyOpenChest()
    {
        InvokeConditionAction(ConditionType.OpenChest);
        InvokeConditionAction(ConditionType.TotalOpenChest);
        InvokeConditionAction(ConditionType.TreasureGachaCount);
        InvokeConditionAction(ConditionType.PetGachaCount);
    }

    public void NotifyTaskAdd()
    {
        InvokeConditionAction(ConditionType.HasTask);
        InvokeConditionAction(ConditionType.HasNotTask);
    }

    public void NotifyTaskUpdate()
    {
        InvokeConditionAction(ConditionType.TaskComplete);
    }

    public void NotifyTaskRemove()
    {
        InvokeConditionAction(ConditionType.HasTask);
        InvokeConditionAction(ConditionType.HasNotTask);
        InvokeConditionAction(ConditionType.TaskSubmit);
    }

    public void NotifyAddFinishedTask()
    {
        InvokeConditionAction(ConditionType.NpcDelegateTaskFinishCount);
    }

    public void NotifyDemonBattlefieldLayerUpdate()
    {
        InvokeConditionAction(ConditionType.DemonBattleLayerPass);
    }

    public void NotifyUpdateBlackboardData()
    {
        InvokeConditionAction(ConditionType.MapEntityEnd);
        InvokeConditionAction(ConditionType.MapLogicEntityEnd);
        InvokeConditionAction(ConditionType.MapGameTotalCount);
        InvokeConditionAction(ConditionType.MapLogicEntityEndByTag);
    }

    public void NotifyActiveableEntUpdate(bool isGoddess)
    {
        if (isGoddess)
        {
            InvokeConditionAction(ConditionType.MapRegionGoddess);
            InvokeConditionAction(ConditionType.AnyMapGoddessActived);
        }
        InvokeConditionAction(ConditionType.WorldMapProgress);
    }

private sealed class ConditionInfoParser
{
    public void Expand(IHomeConditionNotification condition, List<IHomeConditionNotification> result)
    {
        if (condition.IsComposite)
        {
            List<IHomeConditionNotification> conditions = condition.GetConditions();
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

    public void Distinct(List<IHomeConditionNotification> list)
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
            IHomeConditionNotification condition = list[i];
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
public static ConditionType TypeForName(string name)
{
switch(name)
{
case "NoneCondition": return ConditionType.None;
case "AlwaysCondition": return ConditionType.Always;
case "LevelCondition": return ConditionType.Level;
case "HasItemCondition": return ConditionType.HasItem;
case "MapUnlockCondition": return ConditionType.MapUnlock;
case "MapRegionActivedCondition": return ConditionType.MapRegionActived;
case "HasManualMeetNpcCondition": return ConditionType.HasManualMeetNpc;
case "CompositeAndCondition": return ConditionType.CompositeAnd;
case "CompositeOrCondition": return ConditionType.CompositeOr;
case "HasItemTypeCondition": return ConditionType.HasItemType;
case "RankCondition": return ConditionType.Rank;
case "SubRankCondition": return ConditionType.SubRank;
case "MaxProfessionRankCondition": return ConditionType.MaxProfessionRank;
case "StoryCountCondition": return ConditionType.StoryCount;
case "AnyMapGoddessActivedCondition": return ConditionType.AnyMapGoddessActived;
case "StagePassCondition": return ConditionType.StagePass;
case "WorldMapProgressCondition": return ConditionType.WorldMapProgress;
case "MapGameTotalCountCondition": return ConditionType.MapGameTotalCount;
case "MapLogicEntityEndByTagCondition": return ConditionType.MapLogicEntityEndByTag;
case "MapRegionGoddessCondition": return ConditionType.MapRegionGoddess;
case "TotalOpenChestCondition": return ConditionType.TotalOpenChest;
case "DemonBattleLayerPassCondition": return ConditionType.DemonBattleLayerPass;
case "TaskSubmitCondition": return ConditionType.TaskSubmit;
case "TaskCompleteCondition": return ConditionType.TaskComplete;
case "RegionCodeCondition": return ConditionType.RegionCode;
case "ServerRankUnlockDaysCondition": return ConditionType.ServerRankUnlockDays;
case "PlanesMergeActivityEnableCondition": return ConditionType.PlanesMergeActivityEnable;
default: throw new NotSupportedException("Condition outside migrated notification closure: "+name);
}
}
}
}
