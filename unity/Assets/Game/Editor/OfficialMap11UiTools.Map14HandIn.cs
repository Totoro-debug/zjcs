using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
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
        private static Material RestoreHandInGrayMaterial(Dictionary<string, object> record, string destination)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>((string)record["shaderPath"]);
            if (shader == null || shader.name != (string)record["shaderName"] || ShaderUtil.ShaderHasError(shader))
                throw new InvalidDataException("Official UI/Gray Windows shader is unavailable.");
            string path = destination + "/UIGray.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = "UIGray" }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            foreach (var property in (Dictionary<string, object>)record["floats"])
                material.SetFloat(property.Key, Convert.ToSingle(property.Value));
            foreach (var property in (Dictionary<string, object>)record["colors"])
            {
                var values = (List<object>)property.Value;
                material.SetColor(property.Key, new Color(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]),
                    Convert.ToSingle(values[2]), Convert.ToSingle(values[3])));
            }
            material.shaderKeywords = ((List<object>)record["keywords"]).Cast<string>().ToArray();
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void ImportMap14HandIn()
        {
            const string sourcePath = "Assets/Official/World/Map/14/UI/HandIn/handin-ui.json";
            const string destination = "Assets/Game/Resources/Map14/UI/HandIn";
            AssetDatabase.Refresh();
            string raw = File.ReadAllText(sourcePath);
            var data = JsonUtility.FromJson<OfficialMap11UiData>(raw);
            if (data.manifest != "871_156598" || data.mapClassId != 14 || data.panels.Length != 2
                || data.panels.Single(p => p.key == "DialogHandInItemDialog").nodes.Length != 39
                || data.panels.Single(p => p.key == "ItemIconDefault").nodes.Length != 19
                || data.components.Length != 94 || data.sprites.Length != 18)
                throw new InvalidDataException("Pinned Map14 hand-in hierarchy is incomplete.");
            var document = (Dictionary<string, object>)OfficialEquipmentJson.Parse(raw, true);
            var rawComponents = ((List<object>)document["components"]).Cast<Dictionary<string, object>>().ToArray();
            AudioClip EventClip(Dictionary<string, object> fields, string name)
            {
                var value = (Dictionary<string, object>)fields[name];
                var reference = value["WwiseObjectReference"] as Dictionary<string, object>;
                if (reference == null) return null;
                var sourceEvent = (Dictionary<string, object>)reference["event"];
                string key = (string)sourceEvent["objectName"];
                uint id = Convert.ToUInt32(sourceEvent["id"]);
                if ((key != "Main_Cursor_Normal" || id != 3772241779u) && (key != "Main_Cursor_Back" || id != 3686182067u))
                    throw new InvalidDataException("Unverified official hand-in sound: " + key);
                var audio = (Dictionary<string, object>)((Dictionary<string, object>)document["audio"])[key];
                string path = (string)audio["unityAssetPath"];
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() != (string)audio["sha256"])
                        throw new InvalidDataException("Official hand-in audio differs from verified current bank: " + key);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path)
                    ?? throw new InvalidDataException("Official hand-in audio is unbound: " + key);
            }
            ValidateHandInButtonController((Dictionary<string, object>)document["buttonAnimation"], destination);
            var sprites = ImportSprites(data.sprites);
            GameObject host = new GameObject("Map14HandInRestoration", typeof(RectTransform));
            try
            {
                foreach (var panel in data.panels)
                {
                    var renderer = new OfficialBattleHudRenderer(OfficialBattleHudCatalog.LoadDefault(),
                        uri => sprites.TryGetValue(uri, out Sprite sprite) ? sprite : null);
                    GameObject root = renderer.CreateSubtree(panel.BuildRoot(), host.transform);
                    Transform Require(string path) => renderer.Find(path)?.transform
                        ?? throw new InvalidDataException("Original Map14 hand-in node is missing: " + path);
                    foreach (var node in panel.nodes.Where(n => n.scripts.Contains("NoDrawingRayCast")))
                    {
                        var target = Require(node.path).gameObject;
                        var placeholder = target.GetComponent<Graphic>();
                        if (placeholder != null) UnityEngine.Object.DestroyImmediate(placeholder);
                        target.AddComponent<Zjcs.Official.UI.NoDrawingRayCast>();
                    }
                    BindSupportPrefab(root, panel.key, data, Require, sprites);
                    foreach (var component in data.components.Where(c => c.panel == panel.key))
                    {
                        if (component.type == "Image" || component.type == "MirrorImage")
                            Require(component.path).GetComponent<Image>().enabled = component.fields.m_Enabled != 0;
                        if (component.type == "ScrollRect")
                        {
                            var scroll = Require(component.path).GetComponent<ScrollRect>();
                            scroll.content = (RectTransform)Require(component.fields.m_Content);
                            scroll.viewport = (RectTransform)Require(component.fields.m_Viewport);
                        }
                        if (component.type == "MultiLanguageText")
                        {
                            var text = Require(component.path).gameObject.AddComponent<WorldMultiLanguageText>();
                            text.myKey = component.fields.myKey;
                        }
                        if (component.type == "MaintainGlobalScaleUI")
                        {
                            var scale = Require(component.path).gameObject.AddComponent<WorldMaintainGlobalScaleUI>();
                            var fields = Fields(component);
                            EditorJsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new FixedScale {
                                mFixedGlobalScale = Vector((Dictionary<string, object>)fields["mFixedGlobalScale"]) }), scale);
                        }
                        if (component.type == "AwardPanelLayoutElement")
                        {
                            var fields = Fields(component);
                            var layout = Require(component.path).gameObject.AddComponent<Zjcs.Official.WorldUI.AwardPanelLayoutElement>();
                            var serialized = new SerializedObject(layout);
                            serialized.FindProperty("mContentLayoutGroup").objectReferenceValue =
                                Require((string)fields["mContentLayoutGroup"]).GetComponent<GridLayoutGroup>();
                            foreach (string name in new[] { "mMinRow", "mMaxHeight", "mLayoutPriority", "mCurrentRow" })
                                serialized.FindProperty(name).intValue = Convert.ToInt32(fields[name], CultureInfo.InvariantCulture);
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                        }
                        if (component.type == "Button")
                        {
                            var button = Require(component.path).GetComponent<Button>();
                            var fields = Fields(component);
                            string target = (string)fields["m_TargetGraphic"];
                            button.targetGraphic = string.IsNullOrEmpty(target) ? null : Require(target).GetComponent<Graphic>();
                            button.transition = (Selectable.Transition)Convert.ToInt32(fields["m_Transition"]);
                            button.interactable = Convert.ToInt32(fields["m_Interactable"]) != 0;
                            var navigation = button.navigation;
                            navigation.mode = (Navigation.Mode)Convert.ToInt32(((Dictionary<string, object>)fields["m_Navigation"])["m_Mode"]);
                            button.navigation = navigation;
                            if (button.transition == Selectable.Transition.Animation)
                            {
                                var animator = button.gameObject.AddComponent<Animator>();
                                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(destination + "/Btn_Base_Click.controller");
                                if (animator.runtimeAnimatorController == null)
                                    throw new InvalidDataException("Official button controller is unbound.");
                            }
                        }
                        if (component.type == "ButtonSound")
                            Require(component.path).gameObject.AddComponent<WorldButtonSound>().ClickEvent = EventClip(Fields(component), "ClickEvent");
                        if (component.type == "PopupWindowSound")
                        {
                            var sound = Require(component.path).gameObject.AddComponent<WorldPopupWindowSound>();
                            sound.ShowSound = EventClip(Fields(component), "ShowSound");
                            sound.HideSound = EventClip(Fields(component), "HideSound");
                        }
                        if (component.type == "UIBloomGroup")
                        {
                            var fields = Fields(component);
                            var group = Require(component.path).gameObject.AddComponent<WorldUIBloomGroup>();
                            group.Masks = ((List<object>)fields["Masks"]).Cast<string>().Select(path => Require(path).GetComponent<Image>()).ToArray();
                            group.IsOverrideMask = Convert.ToInt32(fields["IsOverrideMask"]) != 0;
                            group.OccluderCount = Convert.ToInt32(fields["OccluderCount"]);
                        }
                        if (component.type == "PopupWindow") RestoreHandInPopup(component, Require, destination, Fields(component));
                    }
                    foreach (var component in data.components.Where(c => c.panel == panel.key))
                    {
                        if (component.type == "PreviewTool") Require(component.path).gameObject.AddComponent<WorldPreviewTool>();
                        if (component.type == "ItemProgress")
                        {
                            var fields = Fields(component);
                            var progress = Require(component.path).gameObject.AddComponent<WorldItemProgress>();
                            Color ReadColor(string name)
                            {
                                var color = (Dictionary<string, object>)fields[name];
                                return new Color(Convert.ToSingle(color["r"]),Convert.ToSingle(color["g"]),Convert.ToSingle(color["b"]),Convert.ToSingle(color["a"]));
                            }
                            progress.mDefaultEnoughColor=ReadColor("mDefaultEnoughColor");
                            progress.mDefaultLackColor=ReadColor("mDefaultLackColor");
                            progress.mDefaultTextColor=ReadColor("mDefaultTextColor");
                            progress.mLeftCountText=Require((string)fields["mLeftCountText"]).GetComponent<WorldMultiLanguageText>();
                            progress.mRightCountText=Require((string)fields["mRightCountText"]).GetComponent<WorldMultiLanguageText>();
                            progress.mIsHasLeftTextUnit=Convert.ToInt32(fields["mIsHasLeftTextUnit"])!=0;
                            progress.mIsHasRightTextUnit=Convert.ToInt32(fields["mIsHasRightTextUnit"])!=0;
                            var references=(List<object>)((Dictionary<string,object>)fields["references"])["RefIds"];
                            if (references.Count!=1) throw new InvalidDataException("Unconverted original item progress state configuration.");
                            var configRecord=(Dictionary<string,object>)references[0];
                            if ((string)((Dictionary<string,object>)configRecord["type"])["class"]!="StateGameObjectConfig")
                                throw new InvalidDataException("Original item progress state type differs.");
                            var stateData=(Dictionary<string,object>)((Dictionary<string,object>)configRecord["data"])["mStateGameObjectData"];
                            var values=new WorldStateGameObjectData { GoConfig=new List<WorldStateGameObjectData.StateGameObjectKV>() };
                            foreach(var value in ((List<object>)stateData["GoConfig"]).Cast<Dictionary<string,object>>())
                                values.GoConfig.Add(new WorldStateGameObjectData.StateGameObjectKV {
                                    State=Convert.ToInt32(((Dictionary<string,object>)value["State"])["mValue"]),
                                    GameObjects=((List<object>)value["GameObjects"]).Cast<string>().Select(path=>Require(path).gameObject).ToList() });
                            void Field(object target,string name,object value)
                            {
                                var field=target.GetType().GetField(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                                if(field==null)throw new InvalidDataException("Original state-switcher field is absent: "+name);
                                field.SetValue(target,value);
                            }
                            var config=new WorldStateGameObjectConfig();Field(config,"mStateGameObjectData",values);
                            progress.mStateSwitcher=new WorldStateSwitcher<WorldItemProgress.ProgressType>();
                            Field(progress.mStateSwitcher,"mStateConfigs",new List<IWorldStateConfig>{config});
                            Field(progress.mStateSwitcher,"mOwner",Require((string)((Dictionary<string,object>)fields["mStateSwitcher"])["mOwner"]).gameObject);
                        }
                        if (component.type == "BgDialogElement")
                        {
                            var fields = Fields(component);
                            var background = Require(component.path).gameObject.AddComponent<WorldBgDialogElement>();
                            background.TitleText = Require((string)fields["TitleText"]).GetComponent<WorldMultiLanguageText>();
                            background.TitleIcon = fields["TitleIcon"] == null ? null : Require((string)fields["TitleIcon"]).GetComponent<Image>();
                        }
                        if (component.type == "CountRedDotImage")
                            Require(component.path).gameObject.AddComponent<WorldCountRedDotImage>().mCountLanguage =
                                Require((string)Fields(component)["mCountLanguage"]).GetComponent<WorldMultiLanguageText>();
                    }
                    if (panel.key == "DialogHandInItemDialog")
                    {
                        var fields = rawComponents.Single(c => (string)c["panel"] == panel.key && (string)c["type"] == panel.key);
                        var values = (Dictionary<string, object>)fields["fields"];
                        var view = root.AddComponent<OfficialMap14HandInDialogView>();
                        view.grayImageMaterial = RestoreHandInGrayMaterial((Dictionary<string, object>)document["grayMaterial"], destination);
                        view.mSubmitItemSlot = Require((string)values["mSubmitItemSlot"]);
                        view.mCancelBtn = Require((string)values["mCancelBtn"]).GetComponent<Button>();
                        view.mComfirmBtn = Require((string)values["mComfirmBtn"]).GetComponent<Button>();
                    }
                    RestoreHandInAnimatorSettings(document, panel.key, Require, destination);
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                            throw new InvalidDataException("Restored hand-in prefab has a missing original script: " + node.name);
                    foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (script.GetType().Assembly.GetName().Name == "Assembly-CSharp")
                            throw new InvalidDataException("AssetRipper dummy script remains in hand-in prefab: " + script.GetType().FullName);
                    if (PrefabUtility.SaveAsPrefabAsset(root, destination + "/" + panel.key + ".prefab") == null)
                        throw new InvalidOperationException("Map14 official hand-in prefab could not be saved.");
                    UnityEngine.Object.DestroyImmediate(root);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("Map14 official hand-in prefabs restored: 58 nodes, 94 component records, 18 sprites and original popup/button animations.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            Dictionary<string, object> Fields(OfficialMap11UiComponent c) => (Dictionary<string, object>)
                rawComponents.Single(v => (string)v["panel"] == c.panel && (string)v["path"] == c.path && (string)v["type"] == c.type)["fields"];
        }

        [Serializable] private sealed class FixedScale { public Vector3 mFixedGlobalScale; }
        private static Vector3 Vector(Dictionary<string, object> fields) => new Vector3(
            Convert.ToSingle(fields["x"]), Convert.ToSingle(fields["y"]), Convert.ToSingle(fields["z"]));

        private static void RestoreHandInPopup(OfficialMap11UiComponent source, Func<string, Transform> require, string destination, Dictionary<string, object> fields)
        {
            var popup = require(source.path).gameObject.AddComponent<WorldPopupWindow>();
            foreach (var record in source.fields.clips)
            {
                if (record.unknown.Length != 0) throw new InvalidDataException("Unresolved official hand-in popup curve.");
                var rawClip = ((List<object>)fields["clips"]).Cast<Dictionary<string, object>>().Single(c => (string)c["field"] == record.field);
                var clip = new AnimationClip { name = record.name, frameRate = Convert.ToSingle(rawClip["frameRate"]) };
                foreach (var track in record.tracks)
                {
                    string relative = track.path == source.path ? "" : track.path.Substring(source.path.Length + 1);
                    string property = track.property;
                    Type type;
                    if (property.StartsWith("scale.")) { type = typeof(Transform); property = "m_LocalScale." + property.Substring(6); }
                    else if (property == "m_Alpha") type = typeof(CanvasGroup);
                    else throw new InvalidDataException("Unconverted official hand-in curve: " + property);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(relative, type, property),
                        new AnimationCurve(track.keyframes.Select(k => new Keyframe(k.time,k.value,k.inTangent,k.outTangent)).ToArray()));
                }
                string path = destination + "/" + record.name + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) AssetDatabase.CreateAsset(clip, path);
                else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
                if (record.field == "mShowClip") popup.mShowClip = clip;
                else if (record.field == "mHideClip") popup.mHideClip = clip;
                else if (record.field == "mLoopClip") popup.mLoopClip = clip;
            }
        }
    }
}
