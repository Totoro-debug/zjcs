using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private const string TaskPinnedSlotPath = "/HomeUI/Content/Left/LeftBotGo/PinnedTaskPanel/Slot";
        private const string TaskRowsPath = "/TaskPanel/BgPanel/Content/MiddleGo/Content_Task/ScrollRect/Viewport/AnchorContent";
        private const string TaskEmptyRootPath = "/TaskPanel/BgPanel/Content/MiddleGo/Content_Task/TipEmpty";
        private const string TaskEmptyTextPath = "/TaskPanel/BgPanel/Content/MiddleGo/Content_Task/TipEmpty/Text";
        private const string TaskPanelPrefix = "/TaskPanel/BgPanel/Content/BottomGo/TabGroup_11/TabGroup/TaskPanelTabGroupItem_11_";
        private const string TaskProgressRoot = "/TaskPanelListItemProgress/Content/";

        private OfflineMainlineProgressSession taskProgress;
        private Action<OfficialMainlineTaskDefinition> taskNavigate;
        private HomeUiRenderer taskPanelRenderer;
        private GameObject taskPanelRow;
        private GameObject pinnedTaskRow;
        private HomeUiRenderer pinnedTaskRenderer;
        private bool taskPanelResourcesLoaded;
        private int selectedTaskTab;

        public void OpenTaskPanel(OfflineMainlineProgressSession progress = null,
            Action<OfficialMainlineTaskDefinition> navigate = null)
        {
            taskProgress = progress ?? host.Profile;
            taskNavigate = navigate ?? NavigateMainlineTask;
            EnsureTaskPanelResources();
            OpenDialog("quests", (renderer, panel) =>
            {
                taskPanelRenderer = renderer;
                BindTaskPanelButton("/TaskPanel/BgPanel/Content/BottomGo/CloseBtn_dark", CloseDialog);
                BindTaskTab(0, "Task");
                BindTaskTab(1, "Delegate");
                BindTaskTab(2, "Daily");
                BindTaskTab(3, "Weekly");
                BindTaskTab(4, "AchievementTitle");
                SelectTaskTab(0);
            });
            RefreshTasks();
        }

        public GameObject BuildPinnedTask(OfflineMainlineProgressSession progress = null,
            Action<OfficialMainlineTaskDefinition> navigate = null)
        {
            taskProgress = progress ?? host.Profile;
            taskNavigate = navigate ?? NavigateMainlineTask;
            EnsureTaskPanelResources();
            Transform slot = homeRenderer == null ? null : homeRenderer.Find(TaskPinnedSlotPath)?.transform;
            if (slot == null) return null;

            ClearPinnedTaskRow();
            OfficialMainlineTaskDefinition task = null;
            bool ready = false;
            ProgressionResult current = taskProgress == null
                ? new ProgressionResult { Status = ProgressionResultStatus.Unsupported, UnsupportedCondition = "ProgressSessionMissing" }
                : taskProgress.GetCurrentTask(out task, out ready);
            if (current.Status == ProgressionResultStatus.NotReady && task == null)
            {
                slot.gameObject.SetActive(false);
                return null;
            }

            slot.gameObject.SetActive(true);
            pinnedTaskRenderer = Renderer();
            pinnedTaskRow = pinnedTaskRenderer.Create("task_progress", slot,
                (path, node) => OnTaskRowButton(path, task));
            if (task == null)
                RenderTaskPlaceholder(pinnedTaskRenderer, current);
            else
                RenderTaskRow(pinnedTaskRenderer, task, ready);
            return pinnedTaskRow;
        }

        public void RefreshTasks()
        {
            EnsureTaskPanelResources();
            if (activeDialog != null && activePanelKey == "quests" && taskPanelRenderer != null)
                RenderTaskPanel();
            BuildPinnedTask(taskProgress, taskNavigate);
        }

        private void EnsureTaskPanelResources()
        {
            if (taskPanelResourcesLoaded) return;
            string path = Path.Combine(Application.streamingAssetsPath, "Home", "Tasks", "task-panels.json");
            catalog.LoadPanelExtension(path);
            taskPanelResourcesLoaded = true;
        }

        private void BindTaskPanelButton(string path, Action action)
        {
            Button button = taskPanelRenderer.Find<Button>(path);
            if (button == null) return;
            button.onClick.AddListener(() => action());
        }

        private void BindTaskTab(int index, string tabName)
        {
            string path = TaskPanelPrefix + tabName;
            GameObject tab = taskPanelRenderer.Find(path);
            if (tab == null) return;
            Toggle toggle = tab.GetComponent<Toggle>();
            if (toggle != null)
            {
                toggle.onValueChanged.AddListener(selected => { if (selected) SelectTaskTab(index); });
                return;
            }
            Button button = tab.GetComponent<Button>();
            if (button == null)
            {
                Image target = tab.GetComponent<Image>();
                if (target == null) target = tab.AddComponent<Image>();
                target.color = new Color(1f, 1f, 1f, 0f);
                target.raycastTarget = true;
                button = tab.AddComponent<Button>();
                button.targetGraphic = target;
                button.transition = Selectable.Transition.None;
            }
            if (button != null) button.onClick.AddListener(() => SelectTaskTab(index));
        }

        private void SelectTaskTab(int index)
        {
            selectedTaskTab = index;
            string[] tabs = { "Task", "Delegate", "Daily", "Weekly", "AchievementTitle" };
            for (int tab = 0; tab < tabs.Length; tab++)
                taskPanelRenderer?.SetActive(TaskPanelPrefix + tabs[tab] + "/Selected", tab == index);
            RenderTaskPanel();
        }

        private void RenderTaskPanel()
        {
            if (taskPanelRenderer == null || activePanelKey != "quests") return;
            taskPanelRenderer.SetActive("/TaskPanel/BgPanel/Content/MiddleGo/Content_Task", true);
            taskPanelRenderer.SetActive(TaskRowsPath, false);
            taskPanelRenderer.SetActive("/TaskPanel/BgPanel/Content/MiddleGo/Content_HonorTitleTask", false);
            taskPanelRenderer.SetActive("/TaskPanel/BgPanel/Content/MiddleGo/Content_DelegateTask", false);
            if (taskPanelRow != null) UnityEngine.Object.Destroy(taskPanelRow);
            taskPanelRow = null;

            if (selectedTaskTab != 0)
            {
                taskPanelRenderer.SetActive(TaskEmptyRootPath, true);
                taskPanelRenderer.SetText(TaskEmptyTextPath, "该任务类型在离线模式暂未开放");
                taskPanelRenderer.SetActive(TaskRowsPath, false);
                return;
            }

            if (taskProgress == null)
            {
                taskPanelRenderer.SetActive(TaskEmptyRootPath, true);
                taskPanelRenderer.SetText(TaskEmptyTextPath, "主线进度不可用");
                return;
            }

            ProgressionResult result = taskProgress.GetCurrentTask(
                out OfficialMainlineTaskDefinition currentTask, out bool readyToClaim);
            if (currentTask == null)
            {
                taskPanelRenderer.SetActive(TaskEmptyRootPath, true);
                taskPanelRenderer.SetText(TaskEmptyTextPath, result.Status == ProgressionResultStatus.Unsupported
                    ? "主线条件暂未开放：" + (result.UnsupportedCondition ?? "未知条件")
                    : "当前没有可追踪的主线任务");
                return;
            }

            taskPanelRenderer.SetActive(TaskEmptyRootPath, false);
            taskPanelRenderer.SetActive(TaskRowsPath, true);
            Transform rows = taskPanelRenderer.Find(TaskRowsPath)?.transform;
            if (rows == null) return;
            HomeUiRenderer rowRenderer = Renderer();
            taskPanelRow = rowRenderer.Create("task_progress", rows,
                (path, node) => OnTaskRowButton(path, currentTask));
            RenderTaskRow(rowRenderer, currentTask, readyToClaim);
        }

        private void OnTaskRowButton(string path, OfficialMainlineTaskDefinition task)
        {
            if (task == null) return;
            if (path.IndexOf("ReceiveBtn", StringComparison.Ordinal) >= 0)
            {
                if (taskProgress == null) return;
                ProgressionResult result = taskProgress.ClaimTask(task.classId);
                if (result.IsApplied || result.Status == ProgressionResultStatus.Unchanged)
                {
                    host.RefreshRewards();
                    Notify("已领取主线任务奖励");
                    RefreshTasks();
                }
                else
                    Notify(TaskResultMessage(result));
                return;
            }
            if (path.IndexOf("JumpBtn", StringComparison.Ordinal) >= 0)
            {
                if (taskNavigate != null && CanNavigateMainlineTask(task)) taskNavigate(task);
                return;
            }
            if (path.IndexOf("DetailBtn", StringComparison.Ordinal) >= 0)
                Notify(BuildTaskDetail(task));
        }

        private void RenderTaskRow(HomeUiRenderer renderer, OfficialMainlineTaskDefinition task, bool readyToClaim)
        {
            string titleKey = "task_" + task.classId.ToString(CultureInfo.InvariantCulture) + "_title";
            string progressKey = "task_" + task.classId.ToString(CultureInfo.InvariantCulture) + "_progress";
            renderer.SetText(TaskProgressRoot + "MiddleGo/TitleGo/TitleText",
                catalog.LocalizedText(titleKey, "主线任务 " + task.classId.ToString(CultureInfo.InvariantCulture)));

            ProgressionResult result = taskProgress.GetTaskProgress(task.finishCondition, out long current, out long target);
            if (result.Status == ProgressionResultStatus.Unsupported)
            {
                renderer.SetText(TaskProgressRoot + "Mask/Txt", "任务条件暂未开放：" + result.UnsupportedCondition);
                renderer.SetText(TaskProgressRoot + "MiddleGo/TitleGo/Num", string.Empty);
                renderer.SetText(TaskProgressRoot + "MiddleGo/Slider4/text", string.Empty);
                renderer.SetActive(TaskProgressRoot + "BtnGo/ReceiveBtn", false);
                renderer.SetActive(TaskProgressRoot + "BtnGo/HandInBtn", false);
                renderer.SetActive(TaskProgressRoot + "BtnGo/JumpBtn ", false);
                return;
            }

            renderer.SetText(TaskProgressRoot + "Mask/Txt", FormatTaskText(progressKey, task.finishCondition,
                current, target, includeProgress: true));
            string progressText = FormatCount(current) + "/" + FormatCount(target);
            renderer.SetText(TaskProgressRoot + "MiddleGo/TitleGo/Num", progressText);
            renderer.SetText(TaskProgressRoot + "MiddleGo/Slider4/text", progressText);
            Slider slider = renderer.Find<Slider>(TaskProgressRoot + "MiddleGo/Slider4");
            if (slider != null)
            {
                slider.minValue = 0;
                slider.maxValue = target <= 0 ? 1 : target;
                slider.value = Math.Min(current, slider.maxValue);
            }

            renderer.SetActive(TaskProgressRoot + "BtnGo/ReceiveBtn", readyToClaim);
            renderer.SetActive(TaskProgressRoot + "BtnGo/HandInBtn", false);
            bool canNavigate = CanNavigateMainlineTask(task);
            renderer.SetActive(TaskProgressRoot + "BtnGo/JumpBtn ", !readyToClaim && canNavigate);
            Button receive = renderer.Find<Button>(TaskProgressRoot + "BtnGo/ReceiveBtn");
            if (receive != null) receive.interactable = readyToClaim;
            Button jump = renderer.Find<Button>(TaskProgressRoot + "BtnGo/JumpBtn ");
            if (jump != null) jump.interactable = !readyToClaim && canNavigate;

            if (task.rewards != null && task.rewards.Length == 1)
            {
                OfficialTaskReward reward = task.rewards[0];
                renderer.SetSprite(TaskProgressRoot + "AwardIcon/Content/IconGo/icon", host.ItemIcon(reward.itemId));
                renderer.SetText(TaskProgressRoot + "AwardIcon/Content/BgCountText/CountText", FormatCount(reward.count));
            }
            else
                renderer.SetActive(TaskProgressRoot + "AwardIcon", false);
        }

        private void RenderTaskPlaceholder(HomeUiRenderer renderer, ProgressionResult result)
        {
            renderer.SetText(TaskProgressRoot + "MiddleGo/TitleGo/TitleText", "主线任务暂不可追踪");
            renderer.SetText(TaskProgressRoot + "Mask/Txt", TaskResultMessage(result));
            renderer.SetText(TaskProgressRoot + "MiddleGo/TitleGo/Num", string.Empty);
            renderer.SetText(TaskProgressRoot + "MiddleGo/Slider4/text", string.Empty);
            renderer.SetActive(TaskProgressRoot + "BtnGo/ReceiveBtn", false);
            renderer.SetActive(TaskProgressRoot + "BtnGo/HandInBtn", false);
            renderer.SetActive(TaskProgressRoot + "BtnGo/JumpBtn ", false);
            renderer.SetActive(TaskProgressRoot + "AwardIcon", false);
        }

        private string BuildTaskDetail(OfficialMainlineTaskDefinition task)
        {
            string title = catalog.LocalizedText("task_" + task.classId.ToString(CultureInfo.InvariantCulture) + "_title",
                "主线任务 " + task.classId.ToString(CultureInfo.InvariantCulture));
            string description = catalog.LocalizedText("task_" + task.classId.ToString(CultureInfo.InvariantCulture) + "_desc", string.Empty);
            taskProgress.GetTaskProgress(task.finishCondition, out long current, out long target);
            var message = new StringBuilder(title);
            if (!string.IsNullOrEmpty(description))
                message.Append("\n").Append(FormatTaskText("task_" + task.classId.ToString(CultureInfo.InvariantCulture) + "_desc",
                    task.finishCondition, current, target, includeProgress: false));
            if (task.rewards != null && task.rewards.Length > 0)
            {
                message.Append("\n奖励：");
                for (int index = 0; index < task.rewards.Length; index++)
                {
                    if (index > 0) message.Append("、");
                    OfficialTaskReward reward = task.rewards[index];
                    message.Append(host.ItemName(reward.itemId)).Append(" × ").Append(FormatCount(reward.count));
                }
            }
            return message.ToString();
        }

        private string FormatTaskText(string key, OfficialTaskCondition condition, long current, long target,
            bool includeProgress)
        {
            string template = catalog.LocalizedText(key, string.Empty);
            if (string.IsNullOrEmpty(template)) return FormatCount(current) + "/" + FormatCount(target);
            object[] arguments;
            if (condition.type == "WorldMapProgressCondition")
            {
                string region = catalog.LocalizedText("region_" + condition.mapClassId.ToString(CultureInfo.InvariantCulture)
                    + "_" + condition.region.ToString(CultureInfo.InvariantCulture), "区域 " + condition.region);
                arguments = includeProgress ? new object[] { region } : new object[] { region, target };
            }
            else if (includeProgress)
                arguments = new object[] { current, target };
            else
                arguments = new object[] { current, target };
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, arguments);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        private static string FormatCount(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

        private static string TaskResultMessage(ProgressionResult result)
        {
            if (result == null) return "任务进度不可用";
            if (result.Status == ProgressionResultStatus.Unsupported)
                return "任务条件暂未开放：" + (result.UnsupportedCondition ?? result.Message ?? "未知条件");
            return string.IsNullOrEmpty(result.Message) ? "当前没有可追踪的主线任务" : result.Message;
        }

        private static bool CanNavigateMainlineTask(OfficialMainlineTaskDefinition task)
        {
            if (task?.finishCondition == null) return false;
            switch (task.finishCondition.type)
            {
                case "MapGameTotalCountCondition":
                case "WorldMapProgressCondition":
                case "MapLogicEntityEndByTagCondition":
                case "MapRegionGoddessCondition":
                    return true;
                default:
                    return false;
            }
        }

        private void NavigateMainlineTask(OfficialMainlineTaskDefinition task)
        {
            if (!CanNavigateMainlineTask(task)) return;
            OfficialTaskCondition condition = task.finishCondition;
            if (condition.type == "MapLogicEntityEndByTagCondition")
            {
                if (!string.Equals(condition.tag, "守护者_初始五", StringComparison.Ordinal))
                {
                    Notify("该主线目标的地图跳转尚未接入离线版本");
                    return;
                }
                if (!host.EnterMainland()) Notify("初始岛暂时无法进入");
                return;
            }

            if (condition.mapClassId == 10)
            {
                if (!host.EnterMainland()) Notify("初始岛暂时无法进入");
                return;
            }
            Notify("目标地图 " + condition.mapClassId.ToString(CultureInfo.InvariantCulture) + " 尚未开放");
        }

        private void ClearPinnedTaskRow()
        {
            if (pinnedTaskRow != null) UnityEngine.Object.Destroy(pinnedTaskRow);
            pinnedTaskRow = null;
            pinnedTaskRenderer = null;
        }
    }
}
