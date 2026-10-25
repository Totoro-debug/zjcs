using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        private const string SourceRoot = "Assets/Official/World/Map/11/UI";
        private const string ResourceRoot = "Assets/Game/Resources/Map11/UI";

        public static void ImportAndValidate()
        {
            Import();
            OfficialMap11Tools.ImportAndValidate();
        }

        [MenuItem("Zjcs/World/Map11/Restore Goddess Prefabs")]
        public static void Import()
        {
            AssetDatabase.Refresh();
            string path = SourceRoot + "/map11-ui.json";
            OfficialMap11UiData data = JsonUtility.FromJson<OfficialMap11UiData>(File.ReadAllText(path));
            if (data.manifest != "854_156383" || data.mapClassId != 11 || data.panels.Length != 8)
                throw new InvalidDataException("Official Map11 goddess UI data is incomplete.");
            Directory.CreateDirectory(ResourceRoot);
            File.Copy(path, ResourceRoot + "/catalog.json", true);
            AssetDatabase.Refresh();
            RestoreCommonSpriteAsset(data.commonSpriteAsset);
            Shader colorShiftShader = Shader.Find("Zjcs/Official/UI/ColorShift");
            if (colorShiftShader == null || ShaderUtil.ShaderHasError(colorShiftShader))
                throw new InvalidDataException("The Windows adaptation of official UI/ColorShift did not compile.");
            string materialPath = ResourceRoot + "/UI-ColorShift.mat";
            Material colorShift = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (colorShift == null)
            {
                colorShift = new Material(colorShiftShader) { name = "UI-ColorShift" };
                AssetDatabase.CreateAsset(colorShift, materialPath);
            }
            colorShift.shader = colorShiftShader;
            colorShift.SetColor("_Color", Color.white);
            colorShift.SetFloat("_ColorMask", 15);
            colorShift.SetFloat("_StencilComp", 8);
            colorShift.SetFloat("_Stencil", 0);
            colorShift.SetFloat("_StencilOp", 0);
            colorShift.SetFloat("_StencilReadMask", 255);
            colorShift.SetFloat("_StencilWriteMask", 255);
            colorShift.SetFloat("_UseUIAlphaClip", 0);
            EditorUtility.SetDirty(colorShift);
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (OfficialMap10UiSpriteRecord record in data.sprites)
            {
                using (SHA256 hash = SHA256.Create())
                    if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(record.unityAssetPath)))
                        .Replace("-", "").ToLowerInvariant() != record.sha256)
                        throw new InvalidDataException("Map11 UI sprite differs from its official source: " + record.uri);
                var importer = (TextureImporter)AssetImporter.GetAtPath(record.unityAssetPath);
                string previousSettings = EditorJsonUtility.ToJson(importer);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = record.pixelsPerUnit;
                importer.spriteBorder = new Vector4(record.border[0], record.border[1], record.border[2], record.border[3]);
                importer.spritePivot = new Vector2(record.pivot[0] / record.width, record.pivot[1] / record.height);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (EditorJsonUtility.ToJson(importer) != previousSettings) importer.SaveAndReimport();
                sprites.Add(record.uri, AssetDatabase.LoadAssetAtPath<Sprite>(record.unityAssetPath));
            }
            string itemAssetsPath = ResourceRoot + "/DynamicItems.asset";
            OfficialMap11UiAssets items = AssetDatabase.LoadAssetAtPath<OfficialMap11UiAssets>(itemAssetsPath);
            if (items == null)
            {
                items = ScriptableObject.CreateInstance<OfficialMap11UiAssets>();
                AssetDatabase.CreateAsset(items, itemAssetsPath);
            }
            items.itemIds = data.itemIcons.Select(value => value.itemId).ToArray();
            items.itemSprites = data.itemIcons.Select(value => sprites[value.sprite]).ToArray();
            items.quality = sprites[data.itemQualitySprite];
            EditorUtility.SetDirty(items);
            GameObject host = new GameObject("Map11PrefabRestoration", typeof(RectTransform));
            try
            {
                // Restore the dynamically loaded icon prefabs before binding their dialog references.
                foreach (OfficialMap10UiPanel panel in data.panels.OrderBy(value => value.key.EndsWith("_DialogIcon") ? 0 : 1))
                {
                    var renderer = new OfficialBattleHudRenderer(OfficialBattleHudCatalog.LoadDefault(),
                        uri => sprites.TryGetValue(uri, out Sprite sprite) ? sprite : null);
                    GameObject root = renderer.CreateSubtree(panel.BuildRoot(), host.transform);
                    Transform Require(string nodePath)
                    {
                        GameObject node = renderer.Find(nodePath);
                        if (node == null) throw new InvalidDataException("Official goddess UI binding is missing: " + nodePath);
                        return node.transform;
                    }
                    T Component<T>(string nodePath) where T : Component =>
                        string.IsNullOrEmpty(nodePath) ? null : Require(nodePath).GetComponent<T>()
                        ?? throw new InvalidDataException("Official UI component is missing: " + nodePath);
                    GoddessTouchDialog.ObjectState[] States(OfficialMap11UiState[] states) => states.Select(state =>
                        new GoddessTouchDialog.ObjectState
                        {
                            state = state.state, objects = state.paths.Select(value => Require(value).gameObject).ToArray()
                        }).ToArray();
                    foreach (OfficialMap11UiComponent component in data.components.Where(value => value.panel == panel.key))
                    {
                        if (component.type == "Image" && component.fields.material?.shader == "UI/ColorShift")
                            Component<Image>(component.path).material = colorShift;
                        if (component.type != "PopupWindow") continue;
                        WorldPopupWindow popup = Require(component.path).gameObject.AddComponent<WorldPopupWindow>();
                        foreach (OfficialMap11UiClip sourceClip in component.fields.clips)
                        {
                            if (sourceClip.unknown.Length != 0)
                                throw new InvalidDataException("Unresolved official popup animation bindings: " + sourceClip.name);
                            var clip = new AnimationClip { name = sourceClip.name, frameRate = 30, wrapMode = sourceClip.loop ? WrapMode.Loop : WrapMode.Once };
                            foreach (OfficialMap11UiTrack track in sourceClip.tracks)
                            {
                                string relative = track.path == component.path ? string.Empty : track.path.Substring(component.path.Length + 1);
                                bool scale = track.property.StartsWith("scale.", StringComparison.Ordinal);
                                if (!scale && track.property != "m_Alpha")
                                    throw new InvalidDataException("Unimplemented official popup curve: " + track.property);
                                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(relative,
                                    scale ? typeof(Transform) : typeof(CanvasGroup), scale ? "m_LocalScale." + track.property.Substring(6) : track.property),
                                    new AnimationCurve(track.keyframes.Select(key => new Keyframe(key.time, key.value, key.inTangent, key.outTangent)).ToArray()));
                            }
                            string clipPath = ResourceRoot + "/" + sourceClip.name + ".anim";
                            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                            if (existing == null) AssetDatabase.CreateAsset(clip, clipPath);
                            else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
                            switch (sourceClip.field)
                            {
                                case "mShowClip": popup.mShowClip = clip; break;
                                case "mHideClip": popup.mHideClip = clip; break;
                                case "mLoopClip": popup.mLoopClip = clip; break;
                                default: throw new InvalidDataException("Unknown official popup clip field: " + sourceClip.field);
                            }
                        }
                    }
                    if (panel.key == "WorldCanvas")
                    {
                        foreach (OfficialMap11UiComponent source in data.components.Where(value =>
                                     value.panel == panel.key && value.type == "PlayerCombatRatingText"))
                            Require(source.path).gameObject.AddComponent<WorldPlayerCombatRatingText>().Bind(
                                Component<TMP_Text>(source.fields.Text), source.fields.mNumberSize);
                        OfficialMap11WorldBindings world = data.worldBindings;
                        GameObject[] Objects(string[] paths) => paths.Select(value => Require(value).gameObject).ToArray();
                        WorldPanel worldPanel = root.AddComponent<WorldPanel>();
                        worldPanel.myWithoutCharacterUIs = Objects(world.withoutCharacter);
                        worldPanel.myWithCharacterUIs = Objects(world.withCharacter);
                        worldPanel.myLeaderUIs = Objects(world.leader);
                        worldPanel.myMemberUIs = Objects(world.member);
                        worldPanel.myHideUI = Objects(world.hideUi);
                        worldPanel.matchContents = Objects(world.idleMatchContents);
                        worldPanel.downloadContents = Objects(world.downloadContents);
                        foreach (OfficialMap11ExploreButtonBinding exploreBinding in world.exploreButtons)
                        {
                            WorldExploreBtn explore = Require(exploreBinding.path).gameObject.AddComponent<WorldExploreBtn>();
                            explore.mIsExploreOneTimes = exploreBinding.isOneTime;
                            explore.IsOneTimesStateSwitcher = States(exploreBinding.states);
                        }
                        var chatActions = Require(data.components.First(value => value.type == "ChatActionQuickMsgEntryPanel").path)
                            .gameObject.AddComponent<ChatActionQuickMsgEntryPanel>();
                        chatActions.mExpendObjs = Objects(world.chatActionExpanded);
                        foreach (string maskPath in world.quickMessageMasks)
                            Require(maskPath).gameObject.AddComponent<ChatQuickMsgEntryPanel>().OpenBtnCDMask = Component<Image>(maskPath);
                        foreach (string buttonPath in world.actionEndButtons)
                        {
                            WorldMapCharActionEndBtn end = Require(buttonPath).gameObject.AddComponent<WorldMapCharActionEndBtn>();
                            end.mEndBtn = Component<Button>(buttonPath);
                        }
                        foreach (OfficialMap11TeamPanelBinding teamPanelBinding in world.teamPanels)
                            Require(teamPanelBinding.path).gameObject.AddComponent<TeamMiniPanel>().mHasTeamStateSwitcher = States(teamPanelBinding.states);
                        foreach (OfficialMap11SafeAreaBinding safe in world.safeAreas)
                        {
                            WorldSafeArea area = Require(safe.path).gameObject.AddComponent<WorldSafeArea>();
                            area.PaddingTop = safe.paddingTop;
                            area.PaddingBottom = safe.paddingBottom;
                            area.enabled = safe.enabled;
                        }
                        if (PrefabUtility.SaveAsPrefabAsset(root, ResourceRoot + "/WorldCanvas.prefab") == null)
                            throw new InvalidOperationException("Could not save the restored official WorldCanvas.");
                        UnityEngine.Object.DestroyImmediate(root);
                        continue;
                    }
                    if (panel.key.EndsWith("_DialogIcon"))
                    {
                        foreach (OfficialMap11UiIconBinding iconBinding in data.iconBindings.Where(value => value.key == panel.key))
                        {
                            GoddessDialogIcon icon = Require(iconBinding.path).gameObject.AddComponent<GoddessDialogIcon>();
                            icon.mActivedStateSwitcher = States(iconBinding.states);
                        }
                        if (PrefabUtility.SaveAsPrefabAsset(root, ResourceRoot + "/" + panel.key + ".prefab") == null)
                            throw new InvalidOperationException("Could not save restored goddess icon: " + panel.key);
                        UnityEngine.Object.DestroyImmediate(root);
                        continue;
                    }
                    if (panel.key == "CostItemConfirmDialog" || panel.key == "ItemIconDefault" || panel.key == "PayMoneyChangeDialog")
                    {
                        BindSupportPrefab(root, panel.key, data, Require, sprites);
                        if (PrefabUtility.SaveAsPrefabAsset(root, ResourceRoot + "/" + panel.key + ".prefab") == null)
                            throw new InvalidOperationException("Could not save restored support prefab: " + panel.key);
                        UnityEngine.Object.DestroyImmediate(root);
                        continue;
                    }
                    OfficialMap11UiBindings binding = data.bindings.Single(value => value.key == panel.key);
                    GoddessTouchDialog dialog = root.AddComponent<GoddessTouchDialog>();
                    dialog.myTouchBtn = Component<Button>(binding.touchButton);
                    dialog.CannotActiveBtn = Component<Button>(binding.cannotButton);
                    dialog.mTransferBtn = Component<Button>(binding.transferButton);
                    dialog.myStageName = Component<TMP_Text>(binding.title);
                    dialog.mConditionDesc = Component<TMP_Text>(binding.conditionText);
                    dialog.mBlessingLevelTxt = Component<TMP_Text>(binding.blessingText);
                    dialog.mPropPrefab = string.IsNullOrEmpty(binding.propTemplate) ? null : Require(binding.propTemplate);
                    dialog.mPropPrefabSlot = string.IsNullOrEmpty(binding.propParent) ? null : Require(binding.propParent);
                    dialog.TreasureAwardSlot = string.IsNullOrEmpty(binding.treasureParent) ? null : Require(binding.treasureParent);
                    dialog.DialogIconSlot = Require(binding.iconSlot);
                    dialog.DialogIconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourceRoot + "/" + binding.iconKey + ".prefab");
                    dialog.ActivedStateSwitcher = States(binding.states);
                    dialog.ConditionStateSwitcher = States(binding.conditionStates);
                    if (PrefabUtility.SaveAsPrefabAsset(root, ResourceRoot + "/" + panel.key + ".prefab") == null)
                        throw new InvalidOperationException("Could not save restored goddess prefab: " + panel.key);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Map11 native prefabs restored: WorldCanvas, 2 goddess panels, 2 dynamic icons, confirmation, exchange and item icon, "
                + data.sprites.Length + " sprites, original component bindings and state lists.");
        }
    }
}
