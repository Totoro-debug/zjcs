using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private const string SettingsRoot = "/SettingsDialog/BgDialog1/Content/";
        private const string SettingsTabGroup = SettingsRoot + "TopTabGo/TopTabGroupGO/TabGroup";
        private const string SettingsContentRoot = SettingsRoot + "ContentGo/";
        private const string SettingsFrameGroup =
            SettingsContentRoot + "QualityContent/ViewPort/Content/SettingsQualityGroup_Frame";
        private const string SettingsNumberGroup =
            SettingsContentRoot + "OtherContent/ViewPort/Content/SettingsLongNumDisplayGroup";

        private static readonly string[] SettingsTabs =
        {
            "GameGeneralTab", "QualityTab", "SoundTab", "OtherTab"
        };

        private static readonly string[] SettingsPages =
        {
            "GameGeneralContent", "QualityContent", "SoundContent", "OtherContent"
        };

        public void OpenSettings()
        {
            ToggleFunctions(false);
            GameObject panel = OpenDialog("settings", (renderer, root) => BuildSettings(renderer));
            if (panel == null) return;
            if (host.HomeSettings.LoadStatus == Zjcs.Offline.State.SaveReadStatus.Recovered)
                Notify("设置已从本地备份恢复");
            else if (host.HomeSettings.LoadStatus == Zjcs.Offline.State.SaveReadStatus.Corrupt)
                Notify("设置文件损坏，当前使用默认值");
            host.PlaySettingsClick();
        }

        private void BuildSettings(HomeUiRenderer renderer)
        {
            GameObject messageTab = renderer.Find(SettingsTabGroup + "/MessagePushTab");
            if (messageTab != null) messageTab.SetActive(false);
            renderer.SetActive(SettingsContentRoot + "MessagePushContent", false);

            ToggleGroup tabGroup = renderer.Find<ToggleGroup>(SettingsTabGroup);
            if (tabGroup == null) tabGroup = renderer.Find(SettingsTabGroup).AddComponent<ToggleGroup>();
            tabGroup.allowSwitchOff = false;
            for (int i = 0; i < SettingsTabs.Length; i++)
            {
                int tabIndex = i;
                string tabPath = SettingsTabGroup + "/" + SettingsTabs[i];
                Toggle tab = renderer.Find<Toggle>(tabPath);
                if (tab == null) throw new InvalidOperationException("Official Settings tab is missing: " + tabPath);
                tab.group = tabGroup;
                tab.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn) return;
                    SelectSettingsTab(renderer, tabIndex);
                    host.PlaySettingsClick();
                });
            }
            SelectSettingsTab(renderer, 0);

            renderer.SetActive(SettingsContentRoot
                + "GameGeneralContent/ViewPort/Content/MessagePanel", false);
            DisableSettingsControls(renderer,
                SettingsContentRoot + "GameGeneralContent/ViewPort/Content/LanguagePanel");

            foreach (string group in new[]
            {
                "SettingsQualityGroup_Resolution ", "SettingsQualityGroup_Graph",
                "SettingsQualityGroup_Effect", "SettingsQualityGroup_Physic",
                "SettingsQualityGroup_GridOverlayCount", "SettingsQualityGroup_BloomLevel"
            })
                renderer.SetActive(SettingsContentRoot + "QualityContent/ViewPort/Content/" + group, false);
            BindFrameRate(renderer);
            BindVolumes(renderer);

            renderer.SetActive(SettingsContentRoot + "OtherContent/ViewPort/Content/SettingsMessagePushGroup", false);
            renderer.SetActive(SettingsContentRoot + "OtherContent/ViewPort/Content/SettingsPredownloadGroup", false);
            renderer.SetActive(SettingsContentRoot + "OtherContent/ViewPort/Content/FightRecordGroup", false);
            BindNumberDisplay(renderer);
        }

        private void SelectSettingsTab(HomeUiRenderer renderer, int selected)
        {
            for (int i = 0; i < SettingsTabs.Length; i++)
            {
                Toggle toggle = renderer.Find<Toggle>(SettingsTabGroup + "/" + SettingsTabs[i]);
                if (toggle != null) toggle.SetIsOnWithoutNotify(i == selected);
                renderer.SetActive(SettingsTabGroup + "/" + SettingsTabs[i] + "/Selected", i == selected);
                renderer.SetActive(SettingsContentRoot + SettingsPages[i], i == selected);
            }
        }

        private void BindFrameRate(HomeUiRenderer renderer)
        {
            int selected = host.HomeSettings.FrameRate == 30 ? 1 : 2;
            ConfigureSettingsOptions(renderer, SettingsFrameGroup, 2, selected, index =>
            {
                int value = index == 1 ? 30 : 60;
                if (host.HomeSettings.TrySetFrameRate(value)) return;
                ConfigureSettingsOptions(renderer, SettingsFrameGroup, 2,
                    host.HomeSettings.FrameRate == 30 ? 1 : 2, null);
                Notify("帧率设置保存失败，已恢复原值");
            });
        }

        private void BindNumberDisplay(HomeUiRenderer renderer)
        {
            ConfigureSettingsOptions(renderer, SettingsNumberGroup, 2,
                host.HomeSettings.CompactNumbers ? 2 : 1, index =>
                {
                    if (host.HomeSettings.TrySetCompactNumbers(index == 2))
                    {
                        host.RefreshCounts();
                        return;
                    }
                    ConfigureSettingsOptions(renderer, SettingsNumberGroup, 2,
                        host.HomeSettings.CompactNumbers ? 2 : 1, null);
                    Notify("数值显示设置保存失败，已恢复原值");
                });
        }

        private void ConfigureSettingsOptions(HomeUiRenderer renderer, string groupPath,
            int visibleCount, int selected, Action<int> changed)
        {
            string optionsPath = groupPath + "/QualityOptions";
            GameObject options = renderer.Find(optionsPath);
            if (options == null) throw new InvalidOperationException("Official Settings options are missing: " + optionsPath);
            ToggleGroup group = options.GetComponent<ToggleGroup>();
            if (group == null) group = options.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            for (int index = 1; index <= 4; index++)
            {
                string path = optionsPath + "/SettingsOption" + index;
                GameObject option = renderer.Find(path);
                if (option == null) continue;
                bool visible = index <= visibleCount;
                option.SetActive(visible);
                Toggle toggle = option.GetComponent<Toggle>();
                if (toggle == null) continue;
                toggle.group = group;
                toggle.interactable = visible;
                toggle.SetIsOnWithoutNotify(visible && index == selected);
                toggle.onValueChanged.RemoveAllListeners();
                if (visible && changed != null)
                {
                    int optionIndex = index;
                    toggle.onValueChanged.AddListener(isOn =>
                    {
                        if (!isOn) return;
                        host.PlaySettingsClick();
                        changed(optionIndex);
                    });
                }
            }
        }

        private void BindVolumes(HomeUiRenderer renderer)
        {
            BindVolume(renderer, "AllGo", null, true, "/AllGo/Text");
            BindVolume(renderer, "BGMGo", HomeAudioChannel.Music, false, "/BGMGo/Text");
            BindVolume(renderer, "SFXGo", HomeAudioChannel.Sound, false, "/SFXGo/Text");
            BindVolume(renderer, "VoiceGo", HomeAudioChannel.Voice, false, "/VoiceGo/Text");
        }

        private void BindVolume(HomeUiRenderer renderer, string group, HomeAudioChannel? channel,
            bool allSound, string labelSuffix)
        {
            string path = SettingsContentRoot + "SoundContent/ViewPort/Content/SoundSettings1/" + group;
            Slider slider = renderer.Find<Slider>(path + "/Slider");
            TMP_Text label = renderer.Find<TMP_Text>(SettingsContentRoot
                + "SoundContent/ViewPort/Content/SoundSettings1" + labelSuffix);
            if (slider == null || label == null)
                throw new InvalidOperationException("Official Settings volume control is incomplete: " + group);
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            float value = allSound ? host.HomeSettings.AllSound : channel.Value switch
            {
                HomeAudioChannel.Music => host.HomeSettings.Music,
                HomeAudioChannel.Sound => host.HomeSettings.Sound,
                _ => host.HomeSettings.Voice
            };
            slider.SetValueWithoutNotify(value);
            SetVolumeLabel(label, value);
            slider.onValueChanged.AddListener(next =>
            {
                bool saved = allSound
                    ? host.HomeSettings.TrySetAllSound(next)
                    : host.HomeSettings.TrySetVolume(channel.Value, next);
                if (!saved)
                {
                    float current = allSound ? host.HomeSettings.AllSound : channel.Value switch
                    {
                        HomeAudioChannel.Music => host.HomeSettings.Music,
                        HomeAudioChannel.Sound => host.HomeSettings.Sound,
                        _ => host.HomeSettings.Voice
                    };
                    slider.SetValueWithoutNotify(current);
                    SetVolumeLabel(label, current);
                    Notify("音量设置保存失败，已恢复原值");
                    return;
                }
                SetVolumeLabel(label, next);
            });
        }

        private static void SetVolumeLabel(TMP_Text label, float value)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
            try { label.text = string.Format(CultureInfo.InvariantCulture, label.text, percent); }
            catch (FormatException) { label.text = percent.ToString(CultureInfo.InvariantCulture) + "%"; }
        }

        private static void DisableSettingsControls(HomeUiRenderer renderer, string rootPath)
        {
            GameObject root = renderer.Find(rootPath);
            if (root == null) return;
            foreach (Selectable control in root.GetComponentsInChildren<Selectable>(true))
                control.interactable = false;
        }
    }
}
