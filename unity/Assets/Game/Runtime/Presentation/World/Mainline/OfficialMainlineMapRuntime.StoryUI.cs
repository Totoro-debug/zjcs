using System;
using System.Collections;
using UnityEngine;
using Zjcs.Offline.Story;
using Zjcs.Official.StoryMasks;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private static void RestoreStoryNodeParameters(Map10RuntimeStory graph)
        {
            Newtonsoft.Json.Linq.JObject source = null;
            foreach (Map10RuntimeStoryNode node in graph.nodes)
            {
                if (!string.IsNullOrEmpty(node.sourceJson)) continue;
                source ??= Newtonsoft.Json.Linq.JObject.Parse(graph.graphJson);
                var original = source["nodes"]?[node.id.ToString(System.Globalization.CultureInfo.InvariantCulture)];
                if (original == null || (int?)original["NodeId"] != node.id || (string)original["_type"] != node.type)
                    throw new System.IO.InvalidDataException("Original story node parameters differ: " + graph.key + ":" + node.id);
                node.sourceJson = original.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        private StoryUiModule storyUi;
        private NativeStoryUiBindings storyUiBindings;
        private NativeStoryMaskHost storyMasks;

        [Serializable] private sealed class StoryWindowFields { public StoryWindowPrefab WindowPrefab; public int NeedMask, NeedResetMask; }
        [Serializable] private sealed class StoryWindowPrefab { public string mWindowPath; }
        [Serializable] private sealed class StoryUiFields
        {
            public string StoryUIKey;
            public int WaitRegister = 1, EndOnUnregister, ClickTimes = 1, Index, Active, IsOn;
            public int NeedMask;
            public StoryMaskSettingFields ButtonMaskSet, MaskSetting;
        }
        [Serializable] private sealed class StoryMaskSettingFields
        {
            public Vector3 Offset, Scale = Vector3.one;
            public int ShowMask = 1, TipUIKeyId;
            public MaskSetting ToMaskSetting() => new MaskSetting
            { Offset = Offset, Scale = Scale, ShowMask = ShowMask != 0, TipUIKeyId = TipUIKeyId };
        }
        [Serializable] private sealed class StoryBottomBarFields { public int WaitPublicBottomBarNoNull = 1, IsShow = 1; }

        private void StartStoryBottomBarWait(Map10RuntimeStoryNode node)
        {
            var fields = JsonUtility.FromJson<StoryBottomBarFields>(node.sourceJson);
            StartStoryWait(ContinueStoryAfterUi(StoryUi.WaitBottomBar(() =>
                Zjcs.Official.HomeHudUI.PublicBottomBar.Instance
                    ? Zjcs.Official.HomeHudUI.PublicBottomBar.Instance.IsShow : (bool?)null,
                fields.WaitPublicBottomBarNoNull != 0, fields.IsShow != 0), node));
        }

        private StoryUiModule StoryUi
        {
            get
            {
                if (storyUi == null)
                {
                    storyUiBindings = new NativeStoryUiBindings(hud.ui.OverlayRoot);
                    storyMasks = NativeStoryMaskHost.Create(hud.ui.OverlayRoot);
                    storyUi = new StoryUiModule(storyUiBindings.FindWindow, storyUiBindings.Nodes, storyMasks.Manager);
                }
                return storyUi;
            }
        }

        private void BindStoryUiWindow(GameObject window, string prefabName)
        {
            _ = StoryUi;
            storyUiBindings.BindNativeWindow(window, prefabName);
        }

        private void StartStoryWindowWait(Map10RuntimeStoryNode node, bool opened)
        {
            var fields = JsonUtility.FromJson<StoryWindowFields>(node.sourceJson);
            StartStoryWait(ContinueStoryAfterUi(StoryUi.WaitWindow(fields.WindowPrefab.mWindowPath, opened,
                opened && fields.NeedMask != 0 ? 10 : -1,
                opened && fields.NeedResetMask != 0, opened && fields.NeedMask != 0), node));
        }

        private void StartStoryButtonWait(Map10RuntimeStoryNode node)
        {
            var fields = JsonUtility.FromJson<StoryUiFields>(node.sourceJson);
            StartStoryWait(ContinueStoryAfterUi(StoryUi.WaitClick(fields.StoryUIKey, fields.WaitRegister != 0,
                fields.EndOnUnregister != 0, fields.ClickTimes, node.type == "ClickLoopListItemNode"
                    ? target => NativeStoryUiBindings.ListItemButton(target, fields.Index) : null,
                fields.NeedMask != 0, fields.ButtonMaskSet?.ToMaskSetting()), node));
        }

        private IEnumerator ContinueStoryAfterUi(IEnumerator wait, Map10RuntimeStoryNode node)
        {
            Action<int> resume = storyResume;
            try
            {
                while (wait.MoveNext()) yield return wait.Current;
            }
            finally { (wait as IDisposable)?.Dispose(); }
            resume(StoryNext(node, "Exit"));
        }
    }
}
