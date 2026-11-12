using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.Partners;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeHudUI
{
    // Shares selected condition identities between original HUD listeners and
    // predicates that read the current committed local player on every query.
    public sealed class OfflineHomeHudConditions : IHomeMainUiConditions, IHomeHudConditionHandler, IDisposable
    {
        private sealed class Condition : IHomeHudCondition, IHomeConditionNotification
        {
            public readonly OfflineHomeHudConditions Owner;
            public readonly Dictionary<string, object> Node;
            public Condition(OfflineHomeHudConditions owner, Dictionary<string, object> node)
            { Owner = owner; Node = node; }
            public HomeConditionRules.ConditionType Type => HomeConditionNotifications.TypeForName((string)Node["_type"]);
            public bool IsComposite => Node["_type"] is string type &&
                (type == "CompositeAndCondition" || type == "CompositeOrCondition");
            public List<IHomeConditionNotification> GetConditions()
            {
                var result = new List<IHomeConditionNotification>();
                if (Node["Ids"] == null) return result;
                foreach (var value in (List<object>)Node["Ids"])
                {
                    int id = Convert.ToInt32(value);
                    var condition = (Condition)Owner.GetCondition(id);
                    if (condition == null) Debug.LogError("获取条件配置失败！, Id: " + id);
                    result.Add(condition);
                }
                return result;
            }
        }

        private readonly Dictionary<int, Dictionary<string, object>> conditions;
        private readonly Dictionary<Dictionary<string, object>, Condition> wrappers =
            new Dictionary<Dictionary<string, object>, Condition>();
        private readonly OfficialCurrentHomeRuleAssetCatalog configuration;
        private readonly OfflineHomeConditionPlayer player;
        private readonly HomeConditionRules rules;
        private readonly HomeConditionNotifications notifications;
        private readonly Func<bool> hasMainMap, openMapChannel;
        private bool disposed;

        public OfflineHomeHudConditions(OfflineMainlineProgressSession profile,
            OfficialCurrentHomeRuleAssetCatalog configuration, PartnerConditionRules partnerRules,
            int taskFirstTraceConditionId, Func<int, bool> ownsFurniture,
            Func<bool> hasMainMap, Func<bool> openMapChannel)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            this.configuration = configuration ? configuration : throw new ArgumentNullException(nameof(configuration));
            if (partnerRules == null) throw new ArgumentNullException(nameof(partnerRules));
            if (ownsFurniture == null) throw new ArgumentNullException(nameof(ownsFurniture));
            this.hasMainMap = hasMainMap ?? throw new ArgumentNullException(nameof(hasMainMap));
            this.openMapChannel = openMapChannel ?? throw new ArgumentNullException(nameof(openMapChannel));
            TaskFirstTraceConditionId = taskFirstTraceConditionId;
            conditions = configuration.Rows("conditions").ToDictionary(row => Convert.ToInt32(row["ClassId"]),
                row => (Dictionary<string, object>)row["Condition"]);
            player = new OfflineHomeConditionPlayer(configuration, () => profile.Save, ownsFurniture);
            rules = configuration.CreateConditions(partnerRules);
            notifications = new HomeConditionNotifications(player, Debug.LogException);
        }

        public int TaskFirstTraceConditionId { get; }
        public IHomeHudConditionHandler ConditionHandler => this;
        public bool HasMainMap { get { EnsureAlive(); return hasMainMap(); } }
        public bool OpenMapChannel { get { EnsureAlive(); return openMapChannel(); } }

        public IHomeHudCondition GetCondition(int id)
        {
            EnsureAlive();
            return conditions.TryGetValue(id, out var node) ? Wrap(node) : null;
        }

        public IHomeHudCondition GetFunctionCondition(FunctionType type)
        {
            EnsureAlive();
            // Selected FunctionLimitInfoParser.Get(type) uses the two default zero keys.
            var record = configuration.Rows("function_limit").SingleOrDefault(row =>
                Convert.ToInt32(row["Type"]) == (int)type && Convert.ToInt32(row["Key0"]) == 0 &&
                Convert.ToInt32(row["Key1"]) == 0);
            return record == null ? null : Wrap((Dictionary<string, object>)record["Condition"]);
        }

        private Condition Wrap(Dictionary<string, object> node)
        {
            if (!wrappers.TryGetValue(node, out var condition))
                wrappers.Add(node, condition = new Condition(this, node));
            return condition;
        }
        private Condition RequireCondition(IHomeHudCondition condition)
        {
            EnsureAlive();
            if (!(condition is Condition value) || !ReferenceEquals(value.Owner, this))
                throw new ArgumentException("A condition from this selected HUD scope is required.", nameof(condition));
            return value;
        }
        public bool IsMet(IHomeHudCondition condition) => rules.IsMet(RequireCondition(condition).Node, player);
        public void Register(IHomeHudCondition condition, Action action) => notifications.Register(RequireCondition(condition), action);
        public void Deregister(IHomeHudCondition condition, Action action) => notifications.Deregister(RequireCondition(condition), action);

        // The player owner forwards the matching committed event. The original
        // handler supplies its ordered condition-type fan-out, without polling.
        public void NotifyLevelUpdate() { EnsureAlive(); notifications.NotifyLevelUpdate(); }
        public void NotifySubRankUpdate() { EnsureAlive(); notifications.NotifySubRankUpdate(); }
        public void NotifyWorldMapProgressUpdate() { EnsureAlive(); notifications.NotifyWorldMapProgressUpdate(); }
        public void NotifyItemCountUpdate() { EnsureAlive(); notifications.NotifyItemCountUpdate(); }
        public void NotifyMapDataAdd() { EnsureAlive(); notifications.NotifyMapDataAdd(); }
        public void NotifyMapStatusUpdate() { EnsureAlive(); notifications.NotifyMapStatusUpdate(); }
        public void NotifyStoryCountUpdate() { EnsureAlive(); notifications.NotifyStoryCountUpdate(); }
        public void NotifyStoryStatusUpdate() { EnsureAlive(); notifications.NotifyStoryStatusUpdate(); }
        public void NotifyProfessionPromote() { EnsureAlive(); notifications.NotifyProfessionPromote(); }
        public void NotifyHangUpLevelUp() { EnsureAlive(); notifications.NotifyHangUpLevelUp(); }
        public void NotifyHangUpUnlock() { EnsureAlive(); notifications.NotifyHangUpUnlock(); }
        public void NotifyAddNewNpc() { EnsureAlive(); notifications.NotifyAddNewNpc(); }

        private void EnsureAlive()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeHudConditions));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            notifications.Destroy();
            wrappers.Clear();
        }
    }
}
