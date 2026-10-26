using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineMiniMapModule
        {
            [Serializable] private sealed class MiniMapFilterSave
            {
                public int schema = 1;
                public MiniMapFilterValue[] filters = Array.Empty<MiniMapFilterValue>();
            }
            [Serializable] private sealed class MiniMapFilterValue { public string name; public bool selected; }
            private readonly Dictionary<string, bool> miniMapFilters = new Dictionary<string, bool>();
            private readonly Dictionary<string, GameObject> miniMapFilterItems = new Dictionary<string, GameObject>();
            private GameObject miniMapFilterEntry, miniMapFilterDropdown;
            private OfficialMap11UiComponentFields miniMapFilterFields;

            private Transform FullMiniMapObject(string path) => path == "/MiniMapFullScreenPanel" ? fullMiniMapPanel.transform
                : fullMiniMapPanel.transform.Find(path.Substring("/MiniMapFullScreenPanel/".Length));

            // MiniMapMarkUIFilter.OnEnable / RefreshEntryItems / RefreshFilterItems.
            private void InitializeMiniMapFilter()
            {
                miniMapFilterFields = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel"
                    && value.type == "MiniMapMarkUIFilter").fields;
                miniMapFilterEntry = FullMiniMapObject(miniMapFilterFields.mEntry).gameObject;
                miniMapFilterDropdown = FullMiniMapObject(miniMapFilterFields.mFilter).gameObject;
                miniMapFilterItems.Clear();
                if (miniMapFilters.Count == 0)
                {
                    bool Validate(string json)
                    {
                        try
                        {
                            var value = JsonUtility.FromJson<MiniMapFilterSave>(json);
                            return value != null && value.schema == 1 && value.filters != null
                                && value.filters.All(row => row != null && !string.IsNullOrEmpty(row.name))
                                && value.filters.Select(row => row.name).Distinct().Count() == value.filters.Length;
                        }
                        catch { return false; }
                    }
                    var saved = LocalJsonSaveFile.Read(MiniMapFilterPath, Validate);
                    if (saved.Status == SaveReadStatus.Corrupt) throw new InvalidDataException("小地图筛选存档损坏。");
                    if (saved.Json != null)
                        foreach (var row in JsonUtility.FromJson<MiniMapFilterSave>(saved.Json).filters)
                            miniMapFilters.Add(row.name, row.selected);
                }
                var assets = Resources.Load<OfficialMap11UiAssets>(host.NativeUiResource("DynamicItems"));
                foreach (var mark in miniMapUiData.minimapMarks)
                {
                    // AcquireMark defaults entity marks to true and unselected
                    // resource layers to false, matching the source dictionary lookup.
                    if (!miniMapFilters.ContainsKey(mark.name)) miniMapFilters.Add(mark.name, !int.TryParse(mark.name, out _));
                    if (mark.showOnEntry)
                    {
                        var entry = new OfficialMap11WitchWindow("MarkUIFilterEntryItem",
                            FullMiniMapObject(miniMapFilterFields.mEntryContent), miniMapUiData, host.MapProfile.NativeUiRoot);
                        var fields = entry.Source("MarkUIFilterEntryItem").fields;
                        entry.Find(fields.mIcon).GetComponent<Image>().sprite = assets.MiniMapMark(mark.name);
                        entry.SetText(fields.mDescription, entry.Text(mark.description));
                    }
                    var item = new OfficialMap11WitchWindow("MiniMapMarkFilterItem",
                        FullMiniMapObject(miniMapFilterFields.mFilterContent), miniMapUiData, host.MapProfile.NativeUiRoot);
                    var binding = item.Source("MiniMapMarkFilterItem").fields;
                    item.Find(binding.mIcon).GetComponent<Image>().sprite = assets.MiniMapMark(mark.name);
                    foreach (string path in item.Source("MultiTextsControler").fields.MultiTextList)
                        item.SetText(path, item.Text(mark.description));
                    Button button = item.Find(binding.mTabItem).gameObject.AddComponent<Button>();
                    button.transition = Selectable.Transition.None;
                    button.onClick.AddListener(() =>
                    {
                        miniMapFilters[mark.name] = !miniMapFilters[mark.name];
                        RefreshMiniMapFilter();
                        SaveMiniMapFilter();
                    });
                    miniMapFilterItems.Add(mark.name, item.Root);
                }
                FullMiniMapObject(miniMapFilterFields.mEntryBtn).GetComponent<Button>().onClick.AddListener(() => SetMiniMapFilterExpanded(true));
                FullMiniMapObject(miniMapFilterFields.mButton).GetComponent<Button>().onClick.AddListener(() =>
                {
                    bool selected = !miniMapUiData.minimapMarks.All(value => miniMapFilters[value.name]);
                    foreach (var mark in miniMapUiData.minimapMarks) miniMapFilters[mark.name] = selected;
                    RefreshMiniMapFilter();
                    SaveMiniMapFilter();
                });
                SetMiniMapFilterExpanded(false);
                RefreshMiniMapFilter();
            }

            private void SetMiniMapFilterExpanded(bool expanded)
            {
                miniMapFilterEntry.SetActive(!expanded);
                miniMapFilterDropdown.SetActive(expanded);
            }

            public void UpdateMiniMapFilter()
            {
                if (fullMiniMapPanel == null || miniMapFilterDropdown == null || !miniMapFilterDropdown.activeSelf
                    || !Input.GetMouseButtonDown(0)) return;
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)miniMapFilterDropdown.transform,
                    Input.mousePosition, null)) SetMiniMapFilterExpanded(false);
            }

            // MiniMapMarkUIFilter.GetSelectionType / TabObjectTransition.PlayEffect.
            private void RefreshMiniMapFilter()
            {
                var transition = miniMapUiData.components.Single(value => value.panel == "MiniMapMarkFilterItem"
                    && value.type == "TabObjectTransition").fields;
                foreach (var pair in miniMapFilterItems)
                {
                    Transform Find(string path) => path == "/MiniMapMarkFilterItem" ? pair.Value.transform
                        : pair.Value.transform.Find(path.Substring("/MiniMapMarkFilterItem/".Length));
                    bool selected = miniMapFilters[pair.Key];
                    foreach (string path in transition.myActiveObj) Find(path).gameObject.SetActive(selected);
                    foreach (string path in transition.myDeactiveObj) Find(path).gameObject.SetActive(!selected);
                }
                bool all = miniMapUiData.minimapMarks.All(value => miniMapFilters[value.name]);
                bool none = miniMapUiData.minimapMarks.All(value => !miniMapFilters[value.name]);
                int selection = all ? 1 : none ? 0 : 2;
                foreach (var reference in miniMapFilterFields.references.RefIds)
                {
                    var rows = reference.data.mStateGameObjectData?.GoConfig;
                    if (rows == null) continue;
                    var active = rows.Where(value => value.State.mValue == selection).SelectMany(value => value.GameObjects).ToArray();
                    foreach (string path in rows.SelectMany(value => value.GameObjects).Distinct())
                        FullMiniMapObject(path).gameObject.SetActive(active.Contains(path));
                }
                RefreshNativeMiniMapMarks();
            }

            private string MiniMapFilterPath => Path.Combine(new ProfileSlotCatalog(host.persistentDataPath).GetSlotDirectory(host.slotId), "minimap-filter.json");
            private void SaveMiniMapFilter()
            {
                var saved = new MiniMapFilterSave { filters = miniMapFilters.OrderBy(value => value.Key)
                    .Select(value => new MiniMapFilterValue { name = value.Key, selected = value.Value }).ToArray() };
                LocalJsonSaveFile.Write(MiniMapFilterPath, JsonUtility.ToJson(saved, true), json =>
                {
                    var value = JsonUtility.FromJson<MiniMapFilterSave>(json);
                    return value != null && value.schema == 1 && value.filters != null;
                });
            }
        }
    }
}
