using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private GameObject pinnedWorldTask;
        private OfficialMap11UiData taskUiData;
        private OfficialMap11UiComponentFields pinnedTaskFields;

        // PinnedTaskPanel.AddTaskItem: instantiate the referenced TaskListItem,
        // retaining the source transforms and StateSwitcher bindings.
        private void InitializePinnedWorldTask(OfficialMap11UiData data)
        {
            taskUiData = data;
            var panel = data.components.Single(value => value.type == "PinnedTaskPanel").fields;
            pinnedWorldTask = Instantiate(Resources.Load<GameObject>(NativeUiResource("TaskListItem")),
                ui.Find("WorldCanvas", panel.myTaskItemSlot).transform, false);
            pinnedTaskFields = data.components.Single(value => value.panel == "TaskListItem" && value.type == "TaskListItem").fields;
            TaskObject(pinnedTaskFields.mGMBtn).SetActive(false);
            TaskObject(pinnedTaskFields.mCountdown).SetActive(false);
            TaskObject(pinnedTaskFields.RewardSection).SetActive(false);
            TaskObject(pinnedTaskFields.mClaimBtn).GetComponent<Button>().onClick.AddListener(() =>
            {
                progressionSave.GetCurrentTask(out var task, out bool ready);
                if (task == null || !ready) return;
                var result = progressionSave.ClaimTask(task.classId);
                if (result.IsApplied) UpdateWorldLabels();
            });
            RefreshPinnedWorldTask();
        }

        private GameObject TaskObject(string path) => path == "/TaskListItem" ? pinnedWorldTask
            : pinnedWorldTask.transform.Find(path.Substring("/TaskListItem/".Length)).gameObject;

        private string TaskText(string key) => taskUiData.texts.Single(value => value.key == key).value;

        // TaskListItem.RefreshTask / TaskGoal.PopulateConditions. The local
        // progression session evaluates the original conditions and counters.
        private void RefreshPinnedWorldTask()
        {
            if (pinnedWorldTask == null || progressionSave == null) return;
            progressionSave.GetCurrentTask(out var task, out bool ready);
            pinnedWorldTask.SetActive(task != null);
            if (task == null) return;
            var condition = task.finishCondition;
            var goal = taskUiData.components.Single(value => value.panel == "TaskListItem" && value.type == "TaskGoal").fields;
            var item = taskUiData.components.Single(value => value.panel == "TaskListItem" && value.type == "TaskConditionItem").fields;
            TaskObject(pinnedTaskFields.myTaskTitle).GetComponent<TMP_Text>().text = TaskText("task_" + task.classId + "_title");
            string region = condition.mapClassId == 11 ? TaskText("region_" + mapClassId + "_" + condition.region) : string.Empty;
            TaskObject(item.mText).GetComponent<TMP_Text>().text = string.Format(CultureInfo.CurrentCulture,
                TaskText("task_" + task.classId + "_progress"), region);
            progressionSave.GetTaskProgress(condition, out long current, out long target);
            TMP_Text progress = TaskObject(goal.ProgressText).GetComponent<TMP_Text>();
            bool showProgress = condition.type == "WorldMapProgressCondition";
            if (showProgress) { current /= 100; target /= 100; }
            progress.gameObject.SetActive(showProgress);
            if (showProgress)
            {
                string count = current.ToString(CultureInfo.CurrentCulture);
                if (!ready) count = "<color=#" + ColorUtility.ToHtmlStringRGB(new Color(taskUiData.lackColor[0],
                    taskUiData.lackColor[1], taskUiData.lackColor[2], taskUiData.lackColor[3])) + ">" + count + "</color>";
                progress.text = string.Format(CultureInfo.CurrentCulture, "{0}/{1}", count, target);
            }
            ApplyTaskState(pinnedTaskFields, pinnedTaskFields.mStateSwitcher, ready ? 1 : 0);
            bool recommend = !ready && condition.type == "WorldMapProgressCondition"
                && regionConfigs.TryGetValue(condition.region, out var regionConfig)
                && regionConfig.CombatRatingLevelValues != null && regionConfig.CombatRatingLevelValues.Length > taskUiData.recommendCombatRatingIndex
                && regionConfig.CombatRatingLevelValues[taskUiData.recommendCombatRatingIndex] > 0;
            ApplyTaskState(pinnedTaskFields, pinnedTaskFields.mMapTaskStateSwitcher, recommend ? 2 : 0);
            if (recommend)
            {
                var rating = taskUiData.components.Single(value => value.panel == "TaskListItem"
                    && value.type == "TaskListRecommendCombatRatingItem").fields;
                var config = regionConfigs[condition.region];
                int subRank = progression.Player.SubRank;
                int suppression = subRank < taskUiData.suppressUnlockRank ? 0
                    : progression.Catalog.RankOfSubRank(subRank) - progression.Catalog.RankOfSubRank(config.SubRank);
                TaskObject(rating.mCombatRatingText).GetComponent<TMP_Text>().text = suppression > 0
                    ? TaskText("ui_map_region_rank_suppression_text")
                    : WorldPlayerCombatRatingText.Format(config.CombatRatingLevelValues[taskUiData.recommendCombatRatingIndex]);
                ApplyTaskState(rating, rating.mIsRankOverStates, suppression > 0 ? 1 : 0);
                long combatRating = progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating;
                int index = 0;
                while (index < config.CombatRatingLevelValues.Length && combatRating >= config.CombatRatingLevelValues[index]) index++;
                int level = suppression > 0 ? -1 : config.CombatRatingLevelValues.Length - index;
                var colors = taskUiData.components.Single(value => value.panel == "TaskListItem" && value.type == "UIColorByLevel").fields;
                var color = taskUiData.combatRatingColors.Single(value => value.type == colors.mCombatRateColorType).colors.Single(value => value.Key == level).Value;
                foreach (string path in colors.mGraphics) TaskObject(path).GetComponent<Graphic>().color = color;
            }
        }

        private void ApplyTaskState(OfficialMap11UiComponentFields owner, OfficialMap11StateSwitcher state, int value)
        {
            foreach (var id in state.mStateConfigs)
            {
                var config = owner.references.RefIds.Single(entry => entry.rid == id.rid).data;
                if (config.mStateGameObjectData?.GoConfig != null)
                {
                    var rows = config.mStateGameObjectData.GoConfig;
                    var enabled = rows.Where(row => row.State.mValue == value).SelectMany(row => row.GameObjects).ToArray();
                    foreach (string path in rows.SelectMany(row => row.GameObjects).Distinct())
                        TaskObject(path).SetActive(enabled.Contains(path));
                }
                if (config.mStateColorData?.GraphicConfigs != null)
                    foreach (var graphic in config.mStateColorData.GraphicConfigs)
                    {
                        var color = graphic.Color.mSerializeBuffer.FirstOrDefault(entry => entry.Key.mValue == value);
                        if (color != null) foreach (string path in graphic.Graphics)
                            TaskObject(path).GetComponent<Graphic>().color = color.Value;
                    }
            }
        }
    }
}
