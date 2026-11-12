using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.HomeHudUI;
using TaskUI = Zjcs.Official.HomeTaskUI;

namespace Zjcs.Offline.HomeTaskUI
{
    // Selected TaskDataUtils.InitRewardIconWithProbability, closed over the
    // verified fixed rewards of these 55 tasks. No random preview is fabricated.
    public sealed class OfflineHomeTaskRewardPresenter
    {
        readonly OfflineHomeMainlineTaskData data;
        readonly HashSet<int> ignored;
        readonly int experienceId;
        readonly Dictionary<int, (int type, int quality)> items = new Dictionary<int, (int, int)>();

        public OfflineHomeTaskRewardPresenter(OfflineHomeMainlineTaskData data, JObject gameSettings)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (gameSettings == null) throw new ArgumentNullException(nameof(gameSettings));
            experienceId = (int)gameSettings["PlayerExpItemId"];
            ignored = new HashSet<int>(gameSettings["IgnoreShowAwardId"].Values<int>());
            foreach (var task in data.OrderedTasks)
            {
                var row = task.CopyOriginalRow();
                if ((row["RewardRuleId"] as JArray)?.Count > 0 || (row["RewardRuleIdWithProbability"] as JArray)?.Count > 0)
                    throw new NotSupportedException("Review the newly selected task reward rules.");
                foreach (var reward in ((JObject)row["Reward"]).Properties())
                {
                    int id = int.Parse(reward.Name, CultureInfo.InvariantCulture);
                    if (items.ContainsKey(id)) continue;
                    var info = data.CopyItemRow(id);
                    int type = (int)info["ItemType"];
                    // None of the selected seven definitions enters the original
                    // special award filters. Reject expansion before guessing.
                    if (type == 307 || type == 312 || type == 329 || type == 426
                        || type >= 317 && type <= 319 || type >= 330 && type <= 333 || type == 344)
                        throw new NotSupportedException("Review the newly selected award show filter: " + id);
                    items.Add(id, (type, (int)info["Quality"]));
                }
            }
        }

        int Score(int id) => id == experienceId ? 2 : items[id].type == 1 ? 1 : 0;

        public void Populate(int taskClassId, TaskUI.IItemIconManager manager, Transform slot,
            List<TaskUI.ItemIcon> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            foreach (var icon in slot.GetComponentsInChildren<TaskUI.ItemIcon>(false)) manager.RecycleIcon(icon);
            var rewards = ((JObject)data.Task(taskClassId).CopyOriginalRow()["Reward"]).Properties()
                .Select(value => (id: int.Parse(value.Name, CultureInfo.InvariantCulture), count: (long)value.Value))
                .Where(value => !ignored.Contains(value.id) && value.id != -1)
                .OrderByDescending(value => Score(value.id))
                .ThenByDescending(value => items[value.id].quality).ThenBy(value => value.id);
            foreach (var reward in rewards)
            {
                var icon = manager.GetIcon(reward.id, slot) as OfflineHomeTaskRewardIcon
                    ?? throw new InvalidOperationException("The selected native task icon pool is required.");
                icon.Native.SetClickPopupInfoDialog().SetCountTextScale(ItemIcon.sTextScale);
                icon.Native.SetAttachment(ItemIconAttachmentType.ProbabilityTip, false);
                icon.Native.SetCount(reward.count);
                results.Add(icon);
            }
        }
    }
}
