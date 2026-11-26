using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeStoryDialog;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        private event Action FruitTreeRewardCollectedForStory;
        private bool IsFruitTreeUnlockStoryRunning => furnitureStories != null && furnitureStories.IsFruitTreeUnlock;

        private sealed partial class FurnitureStories
        {
            private WorldTabItem waitingTab;
            private Toggle waitingToggle;
            private UnityAction<bool> waitingTabChanged;
            private Action waitingFruitReward;
            internal bool IsFruitTreeUnlock => IsRunning && IsFruitTreeStory(activeStory);
            private static bool IsFruitTreeStory(string name) => name == "FruitTreeUnlock1" || name == "FruitTreeUnlock2";

            private GameObject FindStoryNode(string key)
            {
                if (key == "PublicBottomBar.HomeTabItem")
                    return PublicBottomBar.Instance?.TabDatas.FirstOrDefault(row => row.TabType == PublicBottomBar.TabType.Home)?.Toggle?.gameObject
                        ?? home.hud?.FindStoryUiNode(key);
                return home.storage?.StoryNodes.GetNode(key);
            }

            private bool TryExecuteWaitNode(HomeFurnitureStoryGraph.Node node, Action<int?> completed)
            {
                switch (node.Type)
                {
                    case "WaitPublicBottomBarActiveNode":
                        StartRoutine(WaitBottomBar(node.Data, () => completed(null)));
                        return true;
                    case "WaitStoryUIActiveNode":
                        StartRoutine(WaitStoryUiActive(node.Data, () => completed(null)));
                        return true;
                    case "WaitTabItemStateNode":
                        StartRoutine(WaitTab(node.Data, () => completed(null)));
                        return true;
                    case "PlayMaskNode":
                        GameObject target = FindStoryNode((string)node.Data["StoryUIKey"]);
                        if (target)
                        {
                            var manager = EnsureMaskHost().Manager;
                            manager.Reset();
                            var setting = ReadMaskSetting(node.Data["MaskSetting"]);
                            var rect = target.GetComponent<RectTransform>();
                            if (rect) manager.OpenRectMask(rect, setting);
                            else manager.OpenRectMask(target, setting);
                        }
                        completed(null);
                        return true;
                    case "CloseMaskNode":
                        EnsureMaskHost().Manager.CloseRectMask();
                        completed(null);
                        return true;
                    case "WaitGotFruitTreeRewardNode":
                        StartRoutine(WaitFruitReward(node.Data, () => completed(null)));
                        return true;
                    default: return false;
                }
            }

            private IEnumerator WaitBottomBar(JObject data, Action completed)
            {
                bool show = (int?)data["IsShow"] != 0;
                bool HasBar() => PublicBottomBar.Instance || home.hud?.StoryBottomBarRoot;
                bool IsShown() => PublicBottomBar.Instance ? PublicBottomBar.Instance.IsShow
                    : home.hud?.StoryBottomBarRoot && home.hud.StoryBottomBarRoot.activeInHierarchy;
                if (!HasBar() || IsShown() != show)
                {
                    var manager = EnsureMaskHost().Manager;
                    manager.Reset();
                    if ((int?)data["IsShowMask"] == 1) manager.SetInvisibleMask(true);
                    if (!HasBar() && (int?)data["WaitPublicBottomBarNoNull"] == 0)
                    {
                        completed();
                        yield break;
                    }
                    while (!HasBar()) yield return null;
                    while (IsShown() != show) yield return null;
                }
                completed();
            }

            private IEnumerator WaitStoryUiActive(JObject data, Action completed)
            {
                var manager = EnsureMaskHost().Manager;
                manager.Reset();
                if ((int?)data["NeedMask"] == 1) manager.SetInvisibleMask(true);
                string key = (string)data["StoryUIKey"];
                bool active = (int?)data["Active"] == 1;
                GameObject target = FindStoryNode(key);
                if (!target && !active) { completed(); yield break; }
                while (!target) { yield return null; target = FindStoryNode(key); }
                while (target.activeSelf != active) yield return null;
                completed();
            }

            private IEnumerator WaitTab(JObject data, Action completed)
            {
                GameObject target = FindStoryNode((string)data["StoryUIKey"]);
                WorldTabItem tab = target ? target.GetComponent<WorldTabItem>() : null;
                Toggle toggle = target ? target.GetComponent<Toggle>() : null;
                if ((!tab && !toggle) || (tab ? tab.isOn : toggle.isOn) == ((int?)data["IsOn"] == 1)) { completed(); yield break; }
                var manager = EnsureMaskHost().Manager;
                manager.Reset();
                if ((int?)data["NeedMask"] == 1)
                    manager.OpenRectMask(target.GetComponent<RectTransform>(), ReadMaskSetting(data["TabMaskSet"]));
                bool met = false;
                waitingTab = tab;
                waitingToggle = toggle;
                waitingTabChanged = value => met = value == ((int?)data["IsOn"] == 1);
                if (tab) tab.OnValueChanged.AddListener(waitingTabChanged);
                else toggle.onValueChanged.AddListener(waitingTabChanged);
                while (!met) yield return null;
                ClearStoryWaitSubscriptions();
                completed();
            }

            private IEnumerator WaitFruitReward(JObject data, Action completed)
            {
                bool received = false;
                waitingFruitReward = () => received = true;
                home.FruitTreeRewardCollectedForStory += waitingFruitReward;
                var manager = EnsureMaskHost().Manager;
                manager.Reset();
                if ((int?)data["NeedMask"] == 1) manager.SetInvisibleMask(true);
                while (!received) yield return null;
                ClearStoryWaitSubscriptions();
                completed();
            }

            private void ClearStoryWaitSubscriptions()
            {
                if (waitingTab && waitingTabChanged != null) waitingTab.OnValueChanged.RemoveListener(waitingTabChanged);
                if (waitingToggle && waitingTabChanged != null) waitingToggle.onValueChanged.RemoveListener(waitingTabChanged);
                waitingTab = null;
                waitingToggle = null;
                waitingTabChanged = null;
                if (waitingFruitReward != null) home.FruitTreeRewardCollectedForStory -= waitingFruitReward;
                waitingFruitReward = null;
            }
        }
    }
}
