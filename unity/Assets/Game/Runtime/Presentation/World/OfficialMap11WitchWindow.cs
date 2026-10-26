using System;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    // Local binding of StoryBookDialog, WorldStageInteractionPreviewDialog/Content,
    // their result preview classes and PeriodicityInteractionStageUI. All nodes,
    // states, colors, localization keys and prefab GUIDs are read from the pinned prefab.
    internal sealed class OfficialMap11WitchWindow
    {
        private readonly OfficialMap11UiData data;
        private readonly string uiRoot;
        private readonly int entityClassId;
        public GameObject Root { get; }
        public string Key { get; }

        public OfficialMap11WitchWindow(string key, Transform parent, OfficialMap11UiData data,
            string uiRoot = "Map11/UI/", int entityClassId = 6620)
        {
            this.data = data;
            this.uiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
            this.entityClassId = entityClassId;
            Key = key;
            GameObject prefab = Resources.Load<GameObject>(uiRoot + key);
            if (prefab == null) throw new InvalidDataException("Unbound official witch prefab: " + key);
            Root = UnityEngine.Object.Instantiate(prefab, parent, false);
            Root.name = key;
        }

        public string Text(string key) => data.texts.Single(value => value.key == key).value;
        public string TextOrKey(string key) => data.texts.FirstOrDefault(value => value.key == key)?.value ?? key;
        public OfficialMap11UiComponent Source(string type) => data.components.Single(value => value.panel == Key && value.type == type);

        public Transform Find(string path)
        {
            string prefix = "/" + Key;
            if (path == prefix) return Root.transform;
            if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix + "/", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid official witch UI reference: " + path);
            return Root.transform.Find(path.Substring(prefix.Length + 1))
                ?? throw new InvalidDataException("Missing official witch UI node: " + path);
        }

        public void SetText(string path, string value) => Find(path).GetComponent<TMP_Text>().text = value;
        public void BindButton(string path, Action action)
        {
            Button button = Find(path).GetComponent<Button>() ?? throw new InvalidDataException("Official witch button is missing: " + path);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }

        public void State(OfficialMap11UiComponent source, int state)
        {
            foreach (OfficialMap11ManagedReference reference in source.fields.references?.RefIds ?? Array.Empty<OfficialMap11ManagedReference>())
            {
                OfficialMap11ManagedData value = reference.data;
                if (reference.type.@class == "StateGameObjectConfig")
                    foreach (OfficialMap11ObjectState row in value.mStateGameObjectData.GoConfig)
                        foreach (string path in row.GameObjects) Find(path).gameObject.SetActive(row.State.mValue == state);
                if (reference.type.@class == "StateUIColorConfig")
                    foreach (OfficialMap11ColorState row in value.mStateColorData.GraphicConfigs)
                    {
                        OfficialMap11ColorValue color = row.Color.mSerializeBuffer.FirstOrDefault(item => item.Key.mValue == state);
                        if (color != null) foreach (string path in row.Graphics) Find(path).GetComponent<Graphic>().color = color.Value;
                    }
                if (reference.type.@class == "StateMultiLanguageConfig")
                {
                    OfficialMap11TextStateData row = value.mStateMultiLanguageTextData;
                    OfficialMap11TextValue key = row.Keys.mSerializeBuffer.FirstOrDefault(item => item.Key.mValue == state);
                    if (key != null) SetText(row.Text, Text(key.Value));
                }
            }
        }

        public void Book(int bookId, Action readEnd)
        {
            OfficialMap11UiComponentFields fields = Source("StoryBookDialog").fields;
            OfficialMap11UiComponent title = data.components.Single(value => value.panel == Key
                && value.type == "PanelTitle" && value.path == fields.mStoryTitle);
            SetText(title.fields.TitleTxt, Text("story_book_" + bookId + "_title"));
            SetText(fields.mStoryContent, Text("story_book_" + bookId + "_content"));
            // StoryBookInfo 1..14 has an empty ImagePath: keep the prefab's original image.
            BindButton(fields.mStoryReadEndBtn, readEnd);
        }

        public void Preview(string title, string description, Map11RuntimeWitchResult[] results, Action close)
        {
            OfficialMap11UiComponentFields fields = Source("WorldStageInteractionPreviewDialog").fields;
            EntityIcon();
            SetText(fields.mTitleText, Text(title));
            SetText(fields.mDescContentText, Text(description));
            foreach (OfficialMap11PreviewGroup group in fields.mWorldStageInteractionPreviewGroups)
            {
                OfficialMap11UiComponent content = data.components.Single(value => value.panel == Key
                    && value.path == group.Content && value.type == "WorldStageInteractionPreviewContent");
                OfficialMap11PreviewAsset[] mappings = content.fields.mStagePreviewAssetGuidProxy.mSerializeBuffer;
                Map11RuntimeWitchResult[] visible = results.Where(result => mappings.Any(item => item.Key == result.ComponentType)).ToArray();
                Find(group.Slot).gameObject.SetActive(visible.Length > 0);
                foreach (Map11RuntimeWitchResult result in visible)
                {
                    string guid = mappings.Single(item => item.Key == result.ComponentType).Value.mAssetGuid;
                    string prefab = data.guidPrefabs.Single(item => item.guid == guid).key;
                    var preview = new OfficialMap11WitchWindow(prefab, Find(content.fields.mContentRoot), data, uiRoot, entityClassId);
                    preview.Result(result);
                }
            }
            BindButton(fields.mCloseBtn, close);
        }

        public void EntityIcon()
        {
            Transform slot = Find(Source("WorldDialogEntityContent").fields.mIconSlot.mPreviewSlot);
            GameObject prefab = Resources.Load<GameObject>(uiRoot + entityClassId + "_11_DialogIcon");
            if (prefab == null) throw new InvalidDataException("Official witch dialog icon is missing.");
            UnityEngine.Object.Instantiate(prefab, slot, false);
        }

        private void Result(Map11RuntimeWitchResult result)
        {
            switch (result.ComponentType)
            {
                case 200:
                    Transform awards = Find(Source("WorldStageInteractionAwardPreview").fields.mAwardContent);
                    OfficialMap11UiAssets assets = Resources.Load<OfficialMap11UiAssets>(uiRoot + "DynamicItems");
                    foreach (Map10RuntimeAwardItem award in result.AwardItems)
                    {
                        var wrapper = new OfficialMap11WitchWindow("ItemAwardIcon", awards, data, uiRoot, entityClassId);
                        wrapper.State(wrapper.Source("ItemAwardIcon"), 0);
                        WorldItemIcon icon = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(uiRoot + "ItemIconDefault"),
                            wrapper.Find(wrapper.Source("ItemAwardIcon").fields.Anchor), false).GetComponent<WorldItemIcon>();
                        icon.Init(assets.Item(award.ItemId), assets.ItemQuality(award.ItemId),
                            award.Count.ToString(CultureInfo.InvariantCulture));
                        if (icon.BottomGo != null) icon.BottomGo.SetActive(false);
                    }
                    break;
                case 212:
                    OfficialMap11UiComponent hp = Source("WorldStageInteractionResultModifyHpPercentPreview");
                    State(hp, result.HpPercentChange > 0 ? 1 : 0);
                    string key = result.HpPercentChange > 0 ? "ui_modify_cure_Hp" : "ui_modify_demage_Hp";
                    SetText(hp.fields.mModifyHpPercentDescText, string.Format(CultureInfo.CurrentCulture,
                        Text(key), Math.Abs(result.HpPercentChange).ToString("P0", CultureInfo.CurrentCulture)));
                    break;
                case 235:
                    Transform parent = Find(Source("WorldStageInteractionResultAwardStatusEffectPreview").fields.mRectTransform);
                    foreach (Map11RuntimeStatueStatus status in result.StatusEffects)
                    {
                        var item = new OfficialMap11WitchWindow("WorldStageInteractionResultAwardStatusEffectPreviewItem", parent, data, uiRoot, entityClassId);
                        OfficialMap11UiComponent source = item.Source("WorldStageInteractionResultAwardStatusEffectPreviewItem");
                        item.State(source, status.EffectType);
                        item.SetText(source.fields.mEntryText, string.Format(CultureInfo.CurrentCulture,
                            Text("ui_get_buff_desc"), Text("entry_" + status.EntryId + "_name"), Text("entry_" + status.EntryId + "_desc")));
                    }
                    break;
                case 238:
                    Transform props = Find(Source("WorldStageInteractionResultAwardExtraPropPreview").fields.mRectTransform);
                    foreach (OfficialMainlineExtraProp prop in result.ExtraProps)
                    {
                        var item = new OfficialMap11WitchWindow("WorldStageInteractionResultAwardExtraPropPreviewItem", props, data, uiRoot, entityClassId);
                        string property = prop.PropType == 1 ? "Attack" : prop.PropType == 2 ? "Defence"
                            : prop.PropType == 3 ? "MaxHp" : prop.PropType == 4 ? "Speed"
                            : throw new InvalidDataException("Unbound hero property name: " + prop.PropType);
                        item.SetText(item.Source("WorldStageInteractionResultAwardExtraPropPreviewItem").fields.mExtraPropItemText,
                            string.Format(CultureInfo.CurrentCulture, Text("ui_item_add_prop_effect_text"),
                                Text("PropType." + property), Zjcs.Offline.Progression.OfficialPropFormat.Compact(prop.Value)));
                    }
                    break;
                case 245:
                    SetText(Source("WorldStageInteractionResultSpawnEntityPreview").fields.mSpawnEntityDesc,
                        Text("map_entity_group_explore_desc_" + result.GroupId));
                    break;
                default: throw new InvalidDataException("Unbound official witch result preview: " + result.ComponentType);
            }
        }

        public void Show()
        {
            Root.SetActive(true);
            foreach (WorldStageInteractionPreviewContentLayoutElement layout in Root.GetComponentsInChildren<WorldStageInteractionPreviewContentLayoutElement>())
                layout.Init();
            foreach (WorldPopupWindow popup in Root.GetComponents<WorldPopupWindow>()) popup.Show();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Root.transform);
        }

        public void Destroy() => UnityEngine.Object.Destroy(Root);
    }
}
