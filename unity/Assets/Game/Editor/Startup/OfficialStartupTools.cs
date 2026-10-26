using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Zjcs.Offline.Startup;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Startup
{
    public static class OfficialStartupTools
    {
        private static string Output = "Assets/Game/Resources/Startup";
        [Serializable] private sealed class Data { public string manifest; public Root[] roots; public Node[] nodes; public Ref[] refs; public Clip[] animations; public Text[] texts; public int default_portrait_id; }
        [Serializable] private sealed class Root { public string name; public int node; }
        [Serializable] private sealed class Node { public string name, transform_json, SafeArea, CanvasWidthAdjuster, text_effect, animator; public bool active, rect; public int parent, id, transform_id, layer, language_sprite_id; public Part[] components; }
        [Serializable] private sealed class Part { public int id; public string type, json; }
        [Serializable] private sealed class Ref { public int id; public string kind, path, name, shader, native_shader, json; public float[] border, pivot; public Vector2[] vertices; public int[] triangles; public float ppu, bias; public bool sRGB, multiAtlas, clearOnBuild; public int width, height, padding, renderMode, population, material_id, texture_id, filter, wrapU, wrapV, aniso; public int[] atlas_ids; public string[] keywords; public Float[] floats; public Tint[] colors; }
        [Serializable] private sealed class Float { public string key; public float value; }
        [Serializable] private sealed class Tint { public string key; public Color value; }
        [Serializable] private sealed class Text { public string key, value; }
        [Serializable] private sealed class Clip { public int node; public string name; public Track[] tracks; }
        [Serializable] private sealed class Track { public string path, property; public Key[] keys; }
        [Serializable] private sealed class Key { public float time, value, inSlope, outSlope; public bool step; }
        private static readonly Regex References = new Regex("\\\"instanceID\\\"\\s*:\\s*(\\d+)");
        private static readonly Regex ObjectFields = new Regex("\\\"([^\\\"]+)\\\"\\s*:\\s*\\{\\s*\\\"instanceID\\\"\\s*:\\s*(\\d+)\\s*\\}");

        public static void Restore() => RestoreNative(false);
        public static void RestoreSaveSelection() => RestoreNative(true);

        private static void RestoreNative(bool saveSelection)
        {
            string Art = saveSelection ? "Assets/Official/SaveSelection" : "Assets/Official/Startup";
            Output = saveSelection ? "Assets/Game/Resources/SaveSelection" : "Assets/Game/Resources/Startup";
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Output + "/Materials");
            Directory.CreateDirectory(Output + "/Sprites");
            AssetDatabase.Refresh();
            Data data = JsonUtility.FromJson<Data>(File.ReadAllText(Art + (saveSelection ? "/save-selection-ui.json" : "/startup-ui.json")));
            if (data.manifest != "854_156383") throw new InvalidOperationException("Startup APK version differs.");
            var bindings = new Dictionary<int, Object>();
            foreach (Ref row in data.refs.Where(r => r.kind == "Sprite" || r.kind == "Texture2D"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(row.path);
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                if (row.kind == "Sprite")
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spriteBorder = new Vector4(row.border[0], row.border[1], row.border[2], row.border[3]);
                    importer.spritePivot = new Vector2(row.pivot[0], row.pivot[1]);
                    importer.spritePixelsPerUnit = row.ppu;
                    importer.alphaIsTransparency = true;
                }
                else
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.isReadable = true;
                    importer.sRGBTexture = row.sRGB;
                    importer.alphaIsTransparency = false;
                    importer.filterMode = (FilterMode)row.filter;
                    importer.wrapModeU = (TextureWrapMode)row.wrapU;
                    importer.wrapModeV = (TextureWrapMode)row.wrapV;
                    importer.anisoLevel = row.aniso;
                    importer.mipMapBias = row.bias;
                }
                importer.SaveAndReimport();
                if (row.kind == "Sprite")
                {
                    string path = Output + "/Sprites/" + row.id + ".asset";
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(row.path);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(row.pivot[0], row.pivot[1]),
                            row.ppu, 0, SpriteMeshType.FullRect, new Vector4(row.border[0], row.border[1], row.border[2], row.border[3]));
                        AssetDatabase.CreateAsset(sprite, path);
                    }
                    sprite.OverrideGeometry(row.vertices, row.triangles.Select(v => (ushort)v).ToArray());
                    bindings[row.id] = sprite;
                    EditorUtility.SetDirty(sprite);
                }
                else bindings[row.id] = AssetDatabase.LoadAssetAtPath<Texture2D>(row.path);
            }
            foreach (Ref row in data.refs.Where(r => r.kind == "Material"))
            {
                Shader shader = Shader.Find(row.native_shader);
                if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException("Startup shader failed: " + row.native_shader);
                string path = Output + "/Materials/" + row.id + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader; mat.name = row.name; mat.shaderKeywords = row.keywords;
                foreach (Float item in row.floats) mat.SetFloat(item.key, item.value);
                foreach (Tint item in row.colors) mat.SetColor(item.key, item.value);
                if (row.texture_id != 0) mat.mainTexture = (Texture2D)bindings[row.texture_id];
                bindings[row.id] = mat;
                EditorUtility.SetDirty(mat);
            }
            foreach (Ref row in data.refs.Where(r => r.kind == "Font"))
            {
                string path = Output + "/Font-" + row.id + ".asset";
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                {
                    Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Official/Commerce/Art/fonts/FZY4K.ttf");
                    font = TMP_FontAsset.CreateFontAsset(source, 40, row.padding, (GlyphRenderMode)row.renderMode,
                        row.width, row.height, (AtlasPopulationMode)row.population, row.multiAtlas);
                    Material temporary = font.material;
                    Texture2D atlas = font.atlasTextures[0];
                    AssetDatabase.CreateAsset(font, path);
                    AssetDatabase.AddObjectToAsset(atlas, font);
                    Object.DestroyImmediate(temporary);
                }
                font.name = row.name;
                JsonUtility.FromJsonOverwrite(row.json, font);
                Texture2D target = font.atlasTextures[0];
                if (target == null)
                {
                    target = new Texture2D(row.width, row.height, TextureFormat.Alpha8, false) { name = row.name + " Atlas" };
                    AssetDatabase.AddObjectToAsset(target, font);
                    font.atlasTextures = new[] { target };
                }
                Texture2D original = (Texture2D)bindings[row.atlas_ids.Single()];
                target.Reinitialize(row.width, row.height, TextureFormat.Alpha8, false);
                target.SetPixels32(original.GetPixels32()); target.Apply(false, false);
                font.material = (Material)bindings[row.material_id];
                font.material.mainTexture = target;
                font.atlasPopulationMode = (AtlasPopulationMode)row.population;
                font.isMultiAtlasTexturesEnabled = row.multiAtlas;
                var serialized = new SerializedObject(font);
                serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                font.ReadFontAssetDefinition();
                if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                {
                    font.TryAddCharacters(string.Concat(data.texts.Select(x => x.value)) + "854.156383 · 离线版本地资源加载失败，请检查游戏文件。启动选择存档已有存档新建存档存档暂无存档点击新建存档开启冒险继续创建继续冒险新角色草稿损坏保留损坏副本并重新开始可读取无法读取返回", out string missing);
                    if (!string.IsNullOrEmpty(missing)) throw new InvalidDataException("Original startup font lacks: " + missing);
                }
                bindings[row.id] = font;
                EditorUtility.SetDirty(font); EditorUtility.SetDirty(target); EditorUtility.SetDirty(font.material);
            }
            var objects = new GameObject[data.nodes.Length];
            var parts = new Dictionary<int, Component>();
            for (int i = 0; i < objects.Length; i++)
            {
                Node n = data.nodes[i];
                GameObject go = n.rect ? new GameObject(n.name, typeof(RectTransform)) : new GameObject(n.name);
                go.SetActive(false);
                go.layer = n.layer;
                if (n.parent >= 0) go.transform.SetParent(objects[n.parent].transform, false);
                objects[i] = go;
                bindings[n.id] = go;
                bindings[n.transform_id] = go.transform;
                TransformFields transform = JsonUtility.FromJson<TransformFields>(n.transform_json);
                go.transform.localPosition = transform.m_LocalPosition;
                go.transform.localRotation = transform.m_LocalRotation;
                go.transform.localScale = transform.m_LocalScale;
                if (go.transform is RectTransform rectTransform)
                {
                    rectTransform.anchorMin = transform.m_AnchorMin; rectTransform.anchorMax = transform.m_AnchorMax;
                    rectTransform.pivot = transform.m_Pivot; rectTransform.sizeDelta = transform.m_SizeDelta;
                    rectTransform.anchoredPosition = transform.m_AnchoredPosition;
                }
                foreach (Part part in n.components)
                {
                    Type type = TypeFor(part.type);
                    Component c = go.AddComponent(type);
                    parts.Add(part.id, c); bindings[part.id] = c;
                }
            }
            string Remap(string json) => References.Replace(json, match =>
            {
                int id = int.Parse(match.Groups[1].Value);
                Object value;
                return "\"instanceID\":" + (bindings.TryGetValue(id, out value) && value != null ? value.GetInstanceID() : 0);
            });
            for (int i = 0; i < objects.Length; i++)
            {
                Node n = data.nodes[i]; GameObject go = objects[i];
                foreach (Part part in n.components)
                {
                    Component target = parts[part.id];
                    if (target is Canvas nativeCanvas)
                    {
                        CanvasFields fields = JsonUtility.FromJson<CanvasFields>(part.json);
                        nativeCanvas.enabled = fields.m_Enabled;
                        nativeCanvas.renderMode = (RenderMode)fields.m_RenderMode;
                        nativeCanvas.planeDistance = fields.m_PlaneDistance;
                        nativeCanvas.pixelPerfect = fields.m_PixelPerfect;
                        nativeCanvas.overrideSorting = fields.m_OverrideSorting;
                        nativeCanvas.overridePixelPerfect = fields.m_OverridePixelPerfect;
                        nativeCanvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)fields.m_AdditionalShaderChannelsFlag;
                        nativeCanvas.sortingLayerID = fields.m_SortingLayerID;
                        nativeCanvas.sortingOrder = fields.m_SortingOrder;
                        nativeCanvas.targetDisplay = fields.m_TargetDisplay;
                    }
                    else if (target is CanvasGroup group)
                    {
                        GroupFields fields = JsonUtility.FromJson<GroupFields>(part.json);
                        group.alpha = fields.m_Alpha; group.interactable = fields.m_Interactable;
                        group.blocksRaycasts = fields.m_BlocksRaycasts; group.ignoreParentGroups = fields.m_IgnoreParentGroups;
                    }
                    else JsonUtility.FromJsonOverwrite(Remap(part.json), target);
                    if (target is RawImage rawImage)
                    {
                        RectFields uv = JsonUtility.FromJson<RawImageFields>(part.json).m_UVRect;
                        rawImage.uvRect = new Rect(uv.x, uv.y, uv.width, uv.height);
                    }
                    var serialized = new SerializedObject(parts[part.id]);
                    foreach (Match field in ObjectFields.Matches(part.json))
                    {
                        SerializedProperty property = serialized.FindProperty(field.Groups[1].Value);
                        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        bindings.TryGetValue(int.Parse(field.Groups[2].Value), out Object value);
                        property.objectReferenceValue = value;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (parts[part.id] is Canvas canvas && n.parent < 0 && go.GetComponent<CanvasScaler>() != null)
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                if (n.language_sprite_id != 0) go.GetComponent<Image>().sprite = (Sprite)bindings[n.language_sprite_id];
                if (!string.IsNullOrEmpty(n.CanvasWidthAdjuster))
                {
                    StartupCanvasWidthAdjuster adjuster = go.AddComponent<StartupCanvasWidthAdjuster>();
                    JsonUtility.FromJsonOverwrite(Remap(n.CanvasWidthAdjuster), adjuster);
                    var serialized = new SerializedObject(adjuster);
                    foreach (Match field in ObjectFields.Matches(n.CanvasWidthAdjuster))
                    {
                        SerializedProperty property = serialized.FindProperty(field.Groups[1].Value);
                        if (property == null) continue;
                        bindings.TryGetValue(int.Parse(field.Groups[2].Value), out Object value);
                        property.objectReferenceValue = value;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (!string.IsNullOrEmpty(n.SafeArea))
                {
                    // Windows has no Android system bars; official default safe
                    // rectangle is the complete adapted parent content.
                    RectTransform rect = (RectTransform)go.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.pivot = Vector2.zero; rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
                }
                if (!string.IsNullOrEmpty(n.text_effect)) RestoreTextEffect(go.GetComponent<TMP_Text>(), n.text_effect, i);
            }
            var clips = new Dictionary<string, AnimationClip>();
            foreach (Clip source in data.animations)
            {
                bool legacy = string.IsNullOrEmpty(data.nodes[source.node].animator);
                var clip = new AnimationClip { name = source.name, legacy = legacy, frameRate = 60 };
                foreach (Track track in source.tracks)
                {
                    GameObject target = string.IsNullOrEmpty(track.path) ? objects[source.node] : objects[source.node].transform.Find(track.path)?.gameObject;
                    Type type = track.property == "m_IsActive" ? typeof(GameObject)
                        : track.property == "m_Alpha" ? typeof(CanvasGroup)
                        : track.property.StartsWith("m_fontColor") ? typeof(TextMeshProUGUI)
                        : track.property.StartsWith("m_Color") ? (target != null && target.GetComponent<TMP_Text>() != null ? typeof(TextMeshProUGUI) : typeof(Image))
                        : track.property.StartsWith("m_SizeDelta") ? typeof(RectTransform) : typeof(Transform);
                    var curve = new AnimationCurve(track.keys.Select(k => new Keyframe(k.time, k.value,
                        k.step ? float.PositiveInfinity : k.inSlope, k.step ? float.PositiveInfinity : k.outSlope)).ToArray());
                    clip.SetCurve(track.path, type, track.property, curve);
                }
                string path = Output + "/" + source.node + "-" + source.name + ".anim";
                AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) AssetDatabase.CreateAsset(clip, path);
                else { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); clip = existing; }
                clips[source.node + "/" + source.name] = clip;
                if (legacy)
                {
                    Animation animation = objects[source.node].GetComponent<Animation>();
                    if (animation == null) animation = objects[source.node].AddComponent<Animation>();
                    animation.playAutomatically = false;
                    animation.AddClip(clip, source.name);
                }
            }
            for (int i = 0; i < objects.Length; i++)
                if (!string.IsNullOrEmpty(data.nodes[i].animator)) RestoreAnimator(objects[i], i, data.nodes[i].animator, clips);
            for (int i = 0; i < objects.Length; i++) objects[i].SetActive(data.nodes[i].active);
            OfficialStartupAssets catalog = null;
            OfficialSaveSelectionAssets saveAssets = null;
            if (saveSelection)
            {
                saveAssets = AssetDatabase.LoadAssetAtPath<OfficialSaveSelectionAssets>(Output + "/OfficialSaveSelectionAssets.asset");
                if (saveAssets == null) { saveAssets = ScriptableObject.CreateInstance<OfficialSaveSelectionAssets>(); AssetDatabase.CreateAsset(saveAssets, Output + "/OfficialSaveSelectionAssets.asset"); }
            }
            else
            {
                catalog = AssetDatabase.LoadAssetAtPath<OfficialStartupAssets>(Output + "/OfficialStartupAssets.asset");
                if (catalog == null) { catalog = ScriptableObject.CreateInstance<OfficialStartupAssets>(); AssetDatabase.CreateAsset(catalog, Output + "/OfficialStartupAssets.asset"); }
            }
            foreach (Root root in data.roots)
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(objects[root.node], Output + "/" + root.name + ".prefab");
                if (prefab == null) throw new InvalidDataException("Original startup prefab could not be saved: " + root.name);
                if (saveSelection && root.name == "PlanesListPanel") saveAssets.panel = prefab;
                else if (saveSelection && root.name == "PlanesRoleItem") saveAssets.roleItem = prefab;
                else if (saveSelection) saveAssets.blurMask = prefab;
                else if (root.name == "BootSplash") catalog.bootSplash = prefab;
                else if (root.name == "ThemeCanvas") catalog.themeCanvas = prefab;
                else if (root.name == "LaunchRoot") catalog.launchCanvas = prefab;
                else catalog.ageTipsDialog = prefab;
                Object.DestroyImmediate(objects[root.node]);
            }
            if (catalog != null)
            {
                catalog.resourceStatus = data.texts.Single(t => t.key == "YooAsset_Init").value;
                catalog.initializationStatus = data.texts.Single(t => t.key == "Game_Initializing").value;
                EditorUtility.SetDirty(catalog);
            }
            if (saveAssets != null)
            {
                saveAssets.photography = AssetDatabase.LoadAssetAtPath<TextAsset>(Art + "/head-photography.json");
                saveAssets.defaultPortrait = (Sprite)bindings[data.default_portrait_id];
                EditorUtility.SetDirty(saveAssets);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Official startup native prefab restoration passed: " + objects.Length + " original nodes.");
        }

        [Serializable] private sealed class TransformFields
        {
            public Vector3 m_LocalPosition, m_LocalScale;
            public Quaternion m_LocalRotation;
            public Vector2 m_AnchorMin, m_AnchorMax, m_Pivot, m_SizeDelta, m_AnchoredPosition;
        }
        [Serializable] private sealed class Controller { public string name, layer; public string[] parameters; public int[] parameter_types; public float weight; public Machine[] machines; }
        [Serializable] private sealed class Machine { public int @default; public State[] states; public Transition[] any; }
        [Serializable] private sealed class State { public string name, clip; public float speed; public Transition[] transitions; }
        [Serializable] private sealed class Transition { public int destination; public float duration, exit_time; public bool @fixed, has_exit; public Condition[] conditions; }
        [Serializable] private sealed class Condition { public int mode; public string parameter; public float threshold; }
        private static void RestoreAnimator(GameObject go, int node, string json, Dictionary<string, AnimationClip> clips)
        {
            Controller source = JsonUtility.FromJson<Controller>(json);
            if (source.machines.Length != 1) throw new InvalidDataException("Unexpected startup animator layers.");
            string path = Output + "/" + node + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (Object child in AssetDatabase.LoadAllAssetsAtPath(path))
                if (child != controller) Object.DestroyImmediate(child, true);
            Machine machine = source.machines[0];
            var sm = new AnimatorStateMachine { name = source.layer, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, controller);
            controller.layers = new[] { new AnimatorControllerLayer { name = source.layer, defaultWeight = source.weight, stateMachine = sm } };
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            for (int p = 0; p < source.parameters.Length; p++)
                controller.AddParameter(source.parameters[p], source.parameter_types != null && source.parameter_types[p] == 4 ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Trigger);
            var transitions = machine.any.Concat(machine.states.SelectMany(s => s.transitions)).ToArray();
            foreach (Condition condition in transitions.SelectMany(t => t.conditions))
            {
                if (condition.mode != (int)AnimatorConditionMode.If && condition.mode != (int)AnimatorConditionMode.IfNot) throw new InvalidDataException("Unexpected original button condition.");
                if (!controller.parameters.Any(p => p.name == condition.parameter)) controller.AddParameter(condition.parameter, AnimatorControllerParameterType.Trigger);
            }
            foreach (string parameter in source.parameters)
                if (!controller.parameters.Any(p => p.name == parameter)) controller.AddParameter(parameter, AnimatorControllerParameterType.Trigger);
            AnimatorState[] states = machine.states.Select(s => sm.AddState(s.name)).ToArray();
            for (int i = 0; i < states.Length; i++)
            {
                State original = machine.states[i];
                states[i].speed = original.speed; states[i].writeDefaultValues = true;
                if (!string.IsNullOrEmpty(original.clip)) states[i].motion = clips[node + "/" + original.clip];
                foreach (Transition t in original.transitions) Bind(states[i].AddTransition(states[t.destination]), t);
            }
            sm.defaultState = states[machine.@default];
            foreach (Transition t in machine.any) Bind(sm.AddAnyStateTransition(states[t.destination]), t);
            go.AddComponent<Animator>().runtimeAnimatorController = controller;
            EditorUtility.SetDirty(controller);
            void Bind(AnimatorStateTransition target, Transition original)
            {
                target.duration = original.duration; target.hasFixedDuration = original.@fixed;
                target.hasExitTime = original.has_exit; target.exitTime = original.exit_time;
                foreach (Condition condition in original.conditions) target.AddCondition((AnimatorConditionMode)condition.mode, condition.threshold, condition.parameter);
            }
        }
        [Serializable] private sealed class CanvasFields
        {
            public bool m_Enabled, m_PixelPerfect, m_ReceivesEvents, m_OverrideSorting, m_OverridePixelPerfect;
            public int m_RenderMode, m_AdditionalShaderChannelsFlag, m_SortingLayerID, m_SortingOrder, m_TargetDisplay;
            public float m_PlaneDistance;
        }
        [Serializable] private sealed class GroupFields
        {
            public float m_Alpha;
            public bool m_Interactable, m_BlocksRaycasts, m_IgnoreParentGroups;
        }
        [Serializable] private sealed class RawImageFields { public RectFields m_UVRect; }
        [Serializable] private sealed class RectFields { public float x, y, width, height; }
        private static Type TypeFor(string name)
        {
            Type startup = typeof(Zjcs.Official.Startup.LaunchPanel).Assembly.GetType("Zjcs.Official.Startup." + name);
            if (startup != null) return startup;
            if (name == "SlicedFilledImage") return typeof(SlicedFilledImage);
            if (name == "MirrorImage") return typeof(MirrorImage);
            if (name == "NoDrawingRayCast") return typeof(NoDrawingRayCast);
            if (name == "TextMeshProUGUI") return typeof(TextMeshProUGUI);
            return typeof(Image).Assembly.GetType("UnityEngine.UI." + name)
                ?? typeof(Canvas).Assembly.GetType("UnityEngine." + name)
                ?? typeof(CanvasGroup).Assembly.GetType("UnityEngine." + name)
                ?? throw new InvalidOperationException("Original UI component has no native implementation: " + name);
        }

        [Serializable] private sealed class TextEffect
        {
            public float dilate, outlineWidth, glowOffset, glowInner, glowOuter, glowPower;
            public int outline, outline2, underlay, glow, gray, linearDodge, useVertexColor;
            public Color outlineColor, glowColor;
        }
        private static void RestoreTextEffect(TMP_Text text, string json, int index)
        {
            TextEffect effect = JsonUtility.FromJson<TextEffect>(json);
            if (effect.outline2 != 0 || effect.underlay != 0 || effect.gray != 0 || effect.linearDodge != 0 || effect.useVertexColor != 0)
                throw new InvalidDataException("Unexpected active original startup text-effect branch.");
            string path = Output + "/Materials/Effect-" + index + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(text.fontSharedMaterial); AssetDatabase.CreateAsset(mat, path); }
            else EditorUtility.CopySerialized(text.fontSharedMaterial, mat);
            mat.SetFloat("_FaceDilate", effect.dilate);
            if (effect.outline != 0)
            {
                mat.EnableKeyword("OUTLINE_ON");
                float factor = text.font.faceInfo.pointSize / text.fontSize / text.font.atlasPadding;
                mat.SetFloat("_OutlineWidth", Mathf.Clamp01(effect.outlineWidth * factor));
                mat.SetColor("_OutlineColor", effect.outlineColor);
            }
            // Both original repair/network labels enable glow with zero power.
            if (effect.glow != 0)
            {
                mat.SetColor("_GlowColor", effect.glowColor);
                mat.SetFloat("_GlowOffset", effect.glowOffset); mat.SetFloat("_GlowInner", effect.glowInner);
                mat.SetFloat("_GlowOuter", effect.glowOuter); mat.SetFloat("_GlowPower", effect.glowPower);
            }
            ShaderUtilities.UpdateShaderRatios(mat);
            text.fontSharedMaterial = mat;
            text.UpdateMeshPadding();
            EditorUtility.SetDirty(mat);
        }
    }
}
