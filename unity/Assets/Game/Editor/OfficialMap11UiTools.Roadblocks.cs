using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        public static void ImportMap14Roadblocks()
        {
            const string destination = "Assets/Game/Resources/Map14/UI";
            const string animations = destination + "/RoadblockAnimations";
            if (!AssetDatabase.IsValidFolder(animations)) AssetDatabase.CreateFolder(destination, "RoadblockAnimations");
            string raw = File.ReadAllText("Assets/Official/World/Map/14/UI/Roadblock/roadblock-ui.json");
            var data = JsonUtility.FromJson<OfficialMap11UiData>(raw);
            if (data.manifest != "884_156692" || data.mapClassId != 14 || data.panels.Length != 9
                || data.panels.Where(p => p.key.StartsWith("Roadblock")).Sum(p => p.nodes.Length) != 254)
                throw new InvalidDataException("Pinned roadblock hierarchy differs.");
            var document = (Dictionary<string, object>)OfficialEquipmentJson.Parse(raw, true);
            var records = ((List<object>)document["components"]).Cast<Dictionary<string, object>>().ToArray();
            var controllers = ((List<object>)document["nativeControllers"]).Cast<Dictionary<string, object>>().ToArray();
            var handIn = (Dictionary<string, object>)OfficialEquipmentJson.Parse(
                File.ReadAllText("Assets/Official/World/Mainline/HandIn/handin-ui.json"), true);
            AudioClip EventClip(Dictionary<string, object> fields, string name)
            {
                var reference = ((Dictionary<string, object>)fields[name])["WwiseObjectReference"] as Dictionary<string, object>;
                if (reference == null) return null;
                var sourceEvent = (Dictionary<string, object>)reference["event"];
                string key = (string)sourceEvent["objectName"];
                uint id = Convert.ToUInt32(sourceEvent["id"]);
                if (key == "Main_Cursor_Fail" && id == 2458796094u)
                    return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Official/World/Map/14/UI/Roadblock/Main_Cursor_Fail.wav")
                        ?? throw new InvalidDataException("Original failed-click sound is unbound.");
                if ((key != "Main_Cursor_Normal" || id != 3772241779u) && (key != "Main_Cursor_Back" || id != 3686182067u))
                    throw new InvalidDataException("Unverified roadblock UI sound: " + key);
                var record = (Dictionary<string, object>)((Dictionary<string, object>)handIn["audio"])[key];
                string path = (string)record["unityAssetPath"];
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() != (string)record["sha256"])
                        throw new InvalidDataException("Current official cursor sound changed.");
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path) ?? throw new InvalidDataException("Cursor sound is not imported.");
            }
            File.WriteAllText(destination + "/roadblock-texts.json", JsonUtility.ToJson(
                JsonUtility.FromJson<OfficialRoadblockUiData>(raw), true));
            var sprites = ImportSprites(data.sprites);
            var host = new GameObject("OriginalRoadblockRestoration", typeof(RectTransform));
            host.SetActive(false);
            int previousMap = activeMapId;
            activeMapId = 14;
            try
            {
                foreach (var panel in data.panels)
                {
                    var renderer = new OfficialBattleHudRenderer(OfficialBattleHudCatalog.LoadDefault(),
                        uri => sprites.TryGetValue(uri, out Sprite sprite) ? sprite : null);
                    var root = renderer.CreateSubtree(panel.BuildRoot(), host.transform);
                    Transform Require(string path) => renderer.Find(path)?.transform
                        ?? throw new InvalidDataException("Original roadblock node missing: " + path);
                    foreach (var node in panel.nodes.Where(n => n.scripts.Contains("NoDrawingRayCast")))
                    {
                        var target = Require(node.path).gameObject;
                        var old = target.GetComponent<Graphic>();
                        if (old) UnityEngine.Object.DestroyImmediate(old);
                        var graphic = target.AddComponent<Zjcs.Official.UI.NoDrawingRayCast>();
                        var button = target.GetComponent<Button>();
                        if (button) button.targetGraphic = graphic;
                    }
                    BindSupportPrefab(root, panel.key, data, Require, sprites);
                    foreach (var source in data.components.Where(c => c.panel == panel.key))
                    {
                        var fields = (Dictionary<string, object>)records.Single(c =>
                            (string)c["panel"] == source.panel && (string)c["path"] == source.path
                            && (string)c["type"] == source.type)["fields"];
                        if (source.type == "MultiLanguageText")
                            Require(source.path).gameObject.AddComponent<WorldMultiLanguageText>().myKey = source.fields.myKey;
                        if (source.type == "Button")
                        {
                            var button = Require(source.path).GetComponent<Button>();
                            var transition = (Selectable.Transition)Convert.ToInt32(fields["m_Transition"]);
                            if (transition == Selectable.Transition.Animation)
                            {
                                var animator = button.GetComponent<Animator>();
                                if (!animator) animator = button.gameObject.AddComponent<Animator>();
                                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Game/Resources/World/Mainline/HandIn/Btn_Base_Click.controller");
                                if (!animator.runtimeAnimatorController) throw new InvalidDataException("Original button animation is unbound.");
                            }
                            button.transition = transition;
                            button.interactable = Convert.ToInt32(fields["m_Interactable"]) != 0;
                        }
                        if (source.type == "PopupWindow")
                        {
                            RestoreHandInPopup(source, Require, animations, fields);
                            Require(source.path).GetComponent<WorldPopupWindow>().CloseByClickMask = Convert.ToInt32(fields["closeByClickMask"]) != 0;
                        }
                        if (source.type == "ConditionItem")
                        {
                            var condition = Require(source.path).gameObject.AddComponent<WorldConditionItem>();
                            // Text components are assigned after all original nodes have been bound.
                            var configs = new List<IWorldStateConfig>();
                            foreach (var item in ((List<object>)((Dictionary<string, object>)fields["references"])["RefIds"]).Cast<Dictionary<string, object>>())
                            {
                                if ((string)((Dictionary<string, object>)item["type"])["class"] != "StateGameObjectConfig")
                                    throw new InvalidDataException("Unconverted roadblock condition state type.");
                                var states = (Dictionary<string, object>)((Dictionary<string, object>)item["data"])["mStateGameObjectData"];
                                var bindings = new WorldStateGameObjectData { GoConfig = new List<WorldStateGameObjectData.StateGameObjectKV>() };
                                foreach (var state in ((List<object>)states["GoConfig"]).Cast<Dictionary<string, object>>())
                                    bindings.GoConfig.Add(new WorldStateGameObjectData.StateGameObjectKV {
                                        State = Convert.ToInt32(((Dictionary<string, object>)state["State"])["mValue"]),
                                        GameObjects = ((List<object>)state["GameObjects"]).Cast<string>().Select(p => Require(p).gameObject).ToList() });
                                configs.Add(WorldStateGameObjectConfig.FromBindings(bindings));
                            }
                            condition.mStateSwitcher = WorldStateSwitcher<bool>.FromBindings(Require(source.path).gameObject, configs);
                        }
                    }
                    foreach (var source in data.components.Where(c => c.panel == panel.key && c.type == "ConditionItem"))
                    {
                        var fields = (Dictionary<string, object>)records.Single(c => (string)c["panel"] == source.panel
                            && (string)c["path"] == source.path && (string)c["type"] == source.type)["fields"];
                        Require(source.path).GetComponent<WorldConditionItem>().mDesc = Require((string)fields["mDesc"]).GetComponent<WorldMultiLanguageText>();
                    }
                    foreach (var source in controllers.Concat(records.Where(c => (string)c["type"] == "RuleDialog" || (string)c["type"] == "TipInfo"))
                        .Where(c => (string)c["panel"] == panel.key))
                    {
                        var fields = (Dictionary<string, object>)source["fields"];
                        if ((string)source["type"] == "MaintainGlobalScaleUI")
                        {
                            var scale = Require((string)source["path"]).gameObject.AddComponent<WorldMaintainGlobalScaleUI>();
                            EditorJsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new FixedScale {
                                mFixedGlobalScale = Vector((Dictionary<string, object>)fields["mFixedGlobalScale"]) }), scale);
                        }
                        if ((string)source["type"] == "ButtonSound")
                            Require((string)source["path"]).gameObject.AddComponent<WorldButtonSound>().ClickEvent = EventClip(fields, "ClickEvent");
                        if ((string)source["type"] == "PopupWindowSound")
                        {
                            var sound = Require((string)source["path"]).gameObject.AddComponent<WorldPopupWindowSound>();
                            sound.ShowSound = EventClip(fields, "ShowSound"); sound.HideSound = EventClip(fields, "HideSound");
                        }
                        if ((string)source["type"] == "PreviewTool") Require((string)source["path"]).gameObject.AddComponent<WorldPreviewTool>();
                        if ((string)source["type"] == "TipInfo" || (string)source["type"] == "RuleDialog")
                        {
                            var component = (string)source["type"] == "TipInfo"
                                ? (Component)Require((string)source["path"]).gameObject.AddComponent<Zjcs.Official.ProfessionUI.TipInfo>()
                                : Require((string)source["path"]).gameObject.AddComponent<Zjcs.Official.ProfessionUI.RuleDialog>();
                            var serialized = new SerializedObject(component);
                            string[] names = component is Zjcs.Official.ProfessionUI.TipInfo
                                ? new[] { "mInfoText", "mImage" } : new[] { "mTitle", "mTipSlot", "mTipInfoPrefab" };
                            foreach (string name in names)
                                serialized.FindProperty(name).objectReferenceValue = name == "mTipInfoPrefab"
                                    ? AssetDatabase.LoadAssetAtPath<GameObject>(destination + "/TipInfo1.prefab")
                                    : name == "mTipSlot" ? (UnityEngine.Object)Require((string)fields[name])
                                    : name == "mImage" ? Require((string)fields[name]).GetComponent<Image>()
                                    : Require((string)fields[name]).GetComponent<WorldMultiLanguageText>();
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                        }
                        if ((string)source["type"] == "UIBloomGroup")
                        {
                            var group = Require((string)source["path"]).gameObject.AddComponent<WorldUIBloomGroup>();
                            group.Masks = ((List<object>)fields["Masks"]).Cast<string>().Select(p => Require(p).GetComponent<Image>()).ToArray();
                            group.IsOverrideMask = Convert.ToInt32(fields["IsOverrideMask"]) != 0;
                            group.OccluderCount = Convert.ToInt32(fields["OccluderCount"]);
                        }
                    }
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                            throw new InvalidDataException("Roadblock native binding left a missing script.");
                    if (panel.key == "RoadblockTeamPropStageDialog") BindStagePrefab(panel.key, data, Require);
                    foreach (var record in ((List<object>)document["animators"]).Cast<Dictionary<string, object>>().Where(r => (string)r["panel"] == panel.key))
                    {
                        var animator = Require((string)record["path"]).GetComponent<Animator>();
                        if (!animator) animator = Require((string)record["path"]).gameObject.AddComponent<Animator>();
                        var settings = (Dictionary<string, object>)record["settings"];
                        // Engine-owned hierarchy/sampling flags are computed by Unity.
                        // Apply serialized Animator settings without invoking its
                        // unsupported native JSON-overwrite implementation.
                        var serializedAnimator = new SerializedObject(animator);
                        foreach (var setting in settings)
                        {
                            var property = serializedAnimator.FindProperty(setting.Key);
                            if (property == null) continue;
                            if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = Convert.ToBoolean(setting.Value);
                            else if (property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Enum)
                                property.intValue = Convert.ToInt32(setting.Value);
                        }
                        serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
                        if (record["controllerSource"] != null)
                            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Game/Resources/World/Mainline/HandIn/Btn_Base_Click.controller");
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, destination + "/" + panel.key + ".prefab");
                    UnityEngine.Object.DestroyImmediate(root);
                }
                AssetDatabase.SaveAssets();
                ImportRoadblockDisplays(document, sprites);
                Debug.Log("Original Map14 roadblock Windows hierarchy imported.");
            }
            finally { activeMapId = previousMap; UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void ImportRoadblockDisplays(Dictionary<string, object> document, Dictionary<string, Sprite> sprites)
        {
            const string assetPath = "Assets/Game/Resources/Map14/Native/roadblock-display-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<OfficialRoadblockDisplayCatalog>(assetPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<OfficialRoadblockDisplayCatalog>(); AssetDatabase.CreateAsset(catalog, assetPath); }
            catalog.manifest = "884_156692";
            catalog.activationSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Official/World/Map/14/UI/Roadblock/Map_Entity_Activate_ElementDoor.wav")
                ?? throw new InvalidDataException("Verified roadblock activation sound is absent.");
            catalog.entries = new[] { 8625, 8626, 8634, 8637, 8633 }.Select(id => new OfficialRoadblockDisplayCatalog.Entry {
                classId = id,
                dialogIcon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/Map14/UI/" + id + "_14_DialogIcon.prefab"),
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/14/NativeRoadblocks/Prefabs/Map/Map_14/EntityDisplays/WorldDoor/" + id + "_14_view.prefab")
                    ?? throw new InvalidDataException("Verified roadblock display missing: " + id) }).ToArray();
            catalog.backgrounds = ((List<object>)document["backgrounds"]).Cast<Dictionary<string, object>>().Select(r =>
                new OfficialRoadblockDisplayCatalog.Background { areaTag = (string)r["areaTag"], sprite = sprites[(string)r["uri"]] }).ToArray();
            catalog.areas = ((List<object>)document["areas"]).Cast<Dictionary<string, object>>().Select(r =>
                new OfficialRoadblockDisplayCatalog.Area { area = Convert.ToInt32(r["area"]), areaTag = (string)r["areaTag"] }).ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }
    }
}
