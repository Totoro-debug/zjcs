using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.PlayerPlay;
using Zjcs.Official.World.NativeMap12Labels;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private GameObject map12SteleDialog;
        private GameObject map12SteleNpcPicker;
        private OfflineNpcAssistanceProgress map12NpcProgress;
        private FightOfficialNpcAssistanceCatalog map12NpcCatalog;
        private Map12SteleDialogServices map12SteleDialogServices;
        private Dictionary<string, string> map12TeamInviteTexts;
        private Dictionary<string, string> map12NpcAssistanceNames;
        private TMP_FontAsset map12TeamInviteChineseFont;

        private void ShowMap12SteleDialog(Map10RuntimeEntity entity)
        {
            if (map12SteleDialog != null) return;
            OfficialMap12Stele stele = FindMap12Stele(entity);
            Map12LabelData labels = JsonUtility.FromJson<Map12LabelData>(
                Resources.Load<TextAsset>("Map12/Native/region-label-data").text);
            OfficialMap11UiAssets assets = Resources.Load<OfficialMap11UiAssets>(NativeUiResource("DynamicItems"));
            GameObject prefab = Resources.Load<GameObject>(NativeUiResource("BattleSteleWorldStageDialog"));
            if (labels == null || assets == null || prefab == null)
                throw new InvalidOperationException("Original Map12 battle stele dialog bindings are missing.");

            var root = Instantiate(prefab, hud.ui.OverlayRoot, false);
            map12SteleDialog = root;
            foreach (WorldMultiLanguageText text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                text.Localizer = NativeUi.Text;
            var host = map12SteleDialogServices = new Map12SteleDialogServices(this, entity, stele);
            var source = new WorldBattleSteleInitData
            {
                StageId = stele.StageId,
                PeriodicityGroupId = stele.PeriodicityGroupId,
                EntityLevel = stele.RegionLevel,
                EntitySubRank = stele.RegionSubRank,
                RegionSubRank = stele.RegionSubRank,
                MonsterLevelAdd = stele.MonsterLevelAdd,
                TeamMaxPlayer = map12Steles.Data.TeamMaxPlayer,
                RecommendCombatRatingIndex = map12Steles.Data.RecommendCombatRatingIndex,
                SupportTeam = stele.SupportTeam,
                CombatRatingLevelValues = stele.CombatRatingLevelValues,
                RuleIds = stele.RuleIds
            };
            var stage = new WorldStageUiContext
            {
                NameKey = "entity_" + entity.EntityClassId + "_name",
                DescriptionKey = "entity_" + entity.EntityClassId + "_desc",
                DialogIconPrefab = Resources.Load<GameObject>(NativeUiResource(entity.EntityClassId + "_DialogIcon")),
                Background = assets.mapBackground,
                IsActive = () => !closed && battleController == null && !IsEntityLocked(entity),
                StartStage = _ => StartEntityStage(entity, null),
                Close = CloseMap12SteleDialog
            };
            WorldBattleSteleContextFactory.Bind(stage, source, labels, host);
            root.GetComponent<StageDialog>().Init(stage);
            root.GetComponent<WorldPopupWindow>().Show();
        }

        private void CloseMap12SteleDialog()
        {
            if (map12SteleDialog == null) return;
            GameObject root = map12SteleDialog;
            map12SteleDialog = null;
            map12SteleDialogServices = null;
            root.GetComponent<WorldPopupWindow>().Hide(() => Destroy(root));
        }

        private void OpenMap12SteleNpcPicker(Map10RuntimeEntity entity, OfficialMap12Stele stele)
        {
            if (map12SteleNpcPicker != null) return;
            map12NpcProgress ??= OfflineNpcAssistanceProgress.Open(new ProfileSlotCatalog(persistentDataPath), slotId);
            map12NpcCatalog ??= OfficialCombatAssetCatalog.LoadDefault().LoadNpcAssistance();
            var models = OfficialBattleModelCatalog.LoadDefault();
            var portraits = OfficialBattleNpcPortraitCatalog.Load();
            GameObject panelPrefab = Resources.Load<GameObject>("Map12/Team/TeamInvitePanel");
            GameObject rowPrefab = Resources.Load<GameObject>("Map12/Team/TeamInvitePanelRobotItem");
            TextAsset inviteTextAsset = Resources.Load<TextAsset>("Map12/Team/invite-texts");
            TextAsset npcNamesAsset = Resources.Load<TextAsset>("Map12/Team/npc-assistance-texts");
            map12TeamInviteChineseFont ??= Resources.Load<TMP_FontAsset>("Combat/FZY4K-Dynamic");
            if (panelPrefab == null || rowPrefab == null || inviteTextAsset == null || npcNamesAsset == null
                || map12TeamInviteChineseFont == null)
                throw new InvalidOperationException("Original NPC invitation prefabs are missing.");

            Map12TeamInviteTextCatalog inviteTextCatalog = JsonUtility.FromJson<Map12TeamInviteTextCatalog>(inviteTextAsset.text);
            if (inviteTextCatalog?.texts == null)
                throw new InvalidOperationException("Original NPC invitation localization is missing.");
            map12TeamInviteTexts = inviteTextCatalog.texts.ToDictionary(value => value.key, value => value.value,
                StringComparer.Ordinal);
            Map12TeamInviteTextCatalog npcNameCatalog = JsonUtility.FromJson<Map12TeamInviteTextCatalog>(npcNamesAsset.text);
            if (npcNameCatalog?.texts == null)
                throw new InvalidOperationException("Original NPC assistance names are missing.");
            map12NpcAssistanceNames = npcNameCatalog.texts.ToDictionary(value => value.key, value => value.value,
                StringComparer.Ordinal);
            var staging = new GameObject("Map12SteleInviteStaging", typeof(RectTransform));
            staging.transform.SetParent(hud.ui.OverlayRoot, false);
            staging.SetActive(false);
            GameObject panel = Instantiate(panelPrefab, staging.transform, false);
            Canvas canvas = panel.GetComponent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Original NPC invitation canvas is missing.");
            DisableMap12OnlineInviteLogic(panel);
            foreach (CanvasGroup group in panel.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            GameObject rowTemplate = Instantiate(rowPrefab, panel.transform, false);
            rowTemplate.name = "OfflineNpcAssistRowTemplate";
            DisableMap12OnlineInviteLogic(rowTemplate);
            rowTemplate.SetActive(false);
            foreach (WorldMultiLanguageText text in panel.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                text.Localizer = Map12SteleInviteText;
                text.Refresh();
            }
            foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = map12TeamInviteChineseFont;
                label.fontSharedMaterial = map12TeamInviteChineseFont.material;
            }
            SetMap12SteleNamedText(panel.transform, "TitleTxt", "选择助战伙伴");
            var root = panel.transform.Find("BgPanel1/Content/MiddleGo");
            Transform pageContent = panel.transform.Find("BgPanel1/Content");
            if (pageContent == null) throw new InvalidOperationException("Original invitation content root is missing.");
            foreach (string page in new[] { "ChallengeGo", "FriendOnlineGo", "CompanionGo", "RoomOnlineGo" })
                SetMap12SteleActive(root, page, false);
            SetMap12SteleActive(root, "FriendRobotGo", true);
            HideMap12RobotOnlineEmptyState(panel.transform.Find("BgPanel1/Content/MiddleGo/FriendRobotGo"));
            SetMap12SteleActive(panel.transform, "BgPanel1/Content/BottomGo/TabGroup_11", false);
            SetMap12SteleButton(panel.transform, "CloseBtn_dark", CloseMap12SteleNpcPicker);

            Transform content = panel.transform.Find("BgPanel1/Content/MiddleGo/FriendRobotGo/Scroll/Viewport/Content");
            Transform viewport = panel.transform.Find("BgPanel1/Content/MiddleGo/FriendRobotGo/Scroll/Viewport");
            LoopVerticalScrollRect scroll = panel.transform.Find("BgPanel1/Content/MiddleGo/FriendRobotGo/Scroll")?.GetComponent<LoopVerticalScrollRect>();
            if (content == null || viewport == null || scroll == null)
                throw new InvalidOperationException("Original NPC invitation list bindings are incomplete.");
            scroll.content = content as RectTransform;
            scroll.viewport = viewport as RectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = LoopScrollRectBase.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            var listRect = content as RectTransform;

            IReadOnlyList<int> candidates = map12NpcCatalog.TeamInviteNpcClassIds();
            var friendships = (progressionSave.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            int maxNpcs = Math.Max(0, map12Steles.Data.TeamMaxPlayer - 1);
            string entryId = Map12SteleAssistKey(stele.PlacementId);
            int[] selected = map12NpcProgress.Selected(WorldBattleSteleTeamTarget.TargetCatalog, entryId).ToArray();
            var retained = new List<int>();
            var rows = new List<Map12SteleNpcEntry>(candidates.Count);
            int selectedCount = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                int npcId = candidates[index];
                bool met = map12NpcCatalog.AssistConditionMet(npcId, progression.Player.Level,
                    friendships, progressionSave.StoryCount);
                bool encountered = friendships.ContainsKey(npcId);
                bool hasModel = OfficialMap12NpcBattleModelView.HasBinding(npcId);
                bool available = hasModel && map12NpcCatalog.CanInviteTeamNpc(npcId, encountered, met);
                bool isSelected = selected.Contains(npcId) && available && selectedCount < maxNpcs;
                if (isSelected) { selectedCount++; retained.Add(npcId); }
                rows.Add(new Map12SteleNpcEntry(npcId, available, isSelected, encountered, met, hasModel));
            }
            if (!selected.SequenceEqual(retained))
                map12NpcProgress.SetSelection(WorldBattleSteleTeamTarget.TargetCatalog, entryId, retained, maxNpcs);
            if (listRect != null) listRect.sizeDelta = new Vector2(0f, candidates.Count * 126f);
            if (scroll.DefaultPrefabSource == null)
                scroll.DefaultPrefabSource = new LoopScrollSinglePrefabSource { Prefab = rowTemplate };
            else scroll.DefaultPrefabSource.Prefab = rowTemplate;
            scroll.dataSource = new Map12SteleNpcDataSource((row, index) =>
            {
                if (index < 0 || index >= rows.Count) return;
                Map12SteleNpcEntry value = rows[index];
                BindMap12SteleNpcRow(row, value.NpcId, value.Available, value.Selected,
                    value.Encountered, value.ConditionMet, value.ModelAvailable, maxNpcs, stele, portraits);
            });
            scroll.totalCount = rows.Count;
            Button confirm = FindMap12SteleButton(panel.transform, "CommonBtn_12");
            if (confirm == null) throw new InvalidOperationException("Original NPC invitation confirmation button is missing.");
            confirm.onClick.RemoveAllListeners();
            confirm.onClick.AddListener(() =>
            {
                CloseMap12SteleNpcPicker();
                map12SteleDialogServices?.RaiseTeamUpdated();
            });
            SetFirstMap12SteleText(confirm.transform, Map12SteleInviteText("ui_confirm_text"));
            foreach (WorldMultiLanguageText text in panel.GetComponentsInChildren<WorldMultiLanguageText>(true))
                if (text.myKey == "ui_team_robot_selected_count_text")
                    text.SetKey(text.myKey, retained.Count, maxNpcs);
            SetMap12SteleNamedText(panel.transform, "AssistCountTxt", "助战伙伴 " + retained.Count + " / " + maxNpcs);
            panel.transform.SetParent(hud.ui.OverlayRoot, false);
            Destroy(staging);
            panel.transform.SetAsLastSibling();
            map12SteleNpcPicker = panel;
            panel.GetComponent<WorldPopupWindow>()?.Show();
            Canvas.ForceUpdateCanvases();
            scroll.RefillCells();
        }

        private event Action Map12SteleTeamChanged;

        private void BindMap12SteleNpcRow(Transform rowTransform, int npcId, bool available,
            bool selected, bool encountered, bool conditionMet, bool modelAvailable, int maxNpcs,
            OfficialMap12Stele stele, OfficialBattleNpcPortraitCatalog portraits)
        {
            GameObject row = rowTransform.gameObject;
            row.name = "Npc_" + npcId;
            DisableMap12OnlineInviteLogic(row);
            foreach (WorldMultiLanguageText text in row.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                text.Localizer = Map12SteleInviteText;
                text.Refresh();
            }
            foreach (TMP_Text label in row.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = map12TeamInviteChineseFont;
                label.fontSharedMaterial = map12TeamInviteChineseFont.material;
            }
            Sprite portrait = portraits.Find(npcId);
            Graphic portraitGraphic = row.GetComponentsInChildren<Graphic>(true)
                .FirstOrDefault(value => value.gameObject.name == "HeadImg");
            string nameKey = map12NpcCatalog.NpcNameKey(npcId);
            if (!map12NpcAssistanceNames.TryGetValue(nameKey, out string npcName))
                npcName = nameKey;
            if (portrait == null)
            {
                Debug.LogWarning("Original NPC assistance portrait is absent from the selected APK and cloud resources: "
                    + map12NpcCatalog.NpcIconPath(npcId), row);
                if (portraitGraphic != null)
                {
                    portraitGraphic.enabled = false;
                    var fallbackObject = new GameObject("OfflineNpcNameFallback", typeof(RectTransform),
                        typeof(TextMeshProUGUI));
                    RectTransform fallbackRect = (RectTransform)fallbackObject.transform;
                    fallbackRect.SetParent(portraitGraphic.transform, false);
                    fallbackRect.anchorMin = Vector2.zero;
                    fallbackRect.anchorMax = Vector2.one;
                    fallbackRect.offsetMin = Vector2.zero;
                    fallbackRect.offsetMax = Vector2.zero;
                    var fallback = fallbackObject.GetComponent<TextMeshProUGUI>();
                    fallback.font = map12TeamInviteChineseFont;
                    fallback.fontSharedMaterial = map12TeamInviteChineseFont.material;
                    fallback.text = npcName;
                    fallback.color = Color.white;
                    fallback.fontSize = 16;
                    fallback.enableAutoSizing = true;
                    fallback.fontSizeMin = 8;
                    fallback.fontSizeMax = 16;
                    fallback.alignment = TextAlignmentOptions.Center;
                    fallback.enableWordWrapping = true;
                    fallback.raycastTarget = false;
                }
            }
            else if (portraitGraphic is Image portraitImage)
            {
                portraitImage.sprite = portrait;
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;
            }
            else if (portraitGraphic is RawImage portraitRawImage)
            {
                portraitRawImage.texture = portrait.texture;
                Rect rect = portrait.textureRect;
                portraitRawImage.uvRect = new Rect(rect.x / portrait.texture.width, rect.y / portrait.texture.height,
                    rect.width / portrait.texture.width, rect.height / portrait.texture.height);
                portraitRawImage.raycastTarget = false;
            }
            TMP_Text name = row.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(value => value.gameObject.name == "NameTxt");
            if (name != null)
            {
                name.font = map12TeamInviteChineseFont;
                name.fontSharedMaterial = map12TeamInviteChineseFont.material;
                name.text = npcName;
            }
            SetMap12SteleActive(row.transform, "LimitedGo", !available);
            SetMap12SteleActive(row.transform, "ImgSelected", selected);
            bool friendshipBlocked = !encountered && map12NpcCatalog.RequiresEncounter(npcId);
            SetMap12SteleActive(row.transform, "FriendshipLockGo", friendshipBlocked);
            SetMap12SteleNamedText(row.transform, "TipText", !modelAvailable ? "本地战斗模型未安装"
                : friendshipBlocked ? "尚未相识" : !conditionMet ? "未满足助战条件" : string.Empty);
            Toggle toggle = row.GetComponent<Toggle>() ?? row.GetComponentInChildren<Toggle>(true);
            if (toggle == null) toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic ??= row.GetComponentsInChildren<Graphic>(true)
                .FirstOrDefault(value => value.name == "ImgBg")
                ?? row.GetComponentsInChildren<Graphic>(true).FirstOrDefault(value => value.name == "ImgSelected");
            if (toggle.targetGraphic == null) throw new InvalidOperationException("Original NPC row has no target graphic.");
            foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = graphic == toggle.targetGraphic;
            toggle.transition = Selectable.Transition.None;
            toggle.SetIsOnWithoutNotify(selected);
            toggle.enabled = true;
            toggle.interactable = available || selected;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(isOn =>
            {
                string entryId = Map12SteleAssistKey(stele.PlacementId);
                List<int> next = map12NpcProgress.Selected(WorldBattleSteleTeamTarget.TargetCatalog, entryId).ToList();
                if (isOn)
                {
                    if (next.Contains(npcId) || !available) return;
                    if (next.Count >= maxNpcs)
                    {
                        hud.ShowOfficialNotice("最多选择 " + maxNpcs + " 名助战伙伴");
                        toggle.SetIsOnWithoutNotify(false);
                        return;
                    }
                    next.Add(npcId);
                }
                else next.Remove(npcId);
                map12NpcProgress.SetSelection(WorldBattleSteleTeamTarget.TargetCatalog, entryId, next, maxNpcs);
                CloseMap12SteleNpcPicker();
                OpenMap12SteleNpcPicker(mapEntities[stele.PlacementId], stele);
            });
        }

        private readonly struct Map12SteleNpcEntry
        {
            public readonly int NpcId;
            public readonly bool Available;
            public readonly bool Selected;
            public readonly bool Encountered;
            public readonly bool ConditionMet;
            public readonly bool ModelAvailable;

            public Map12SteleNpcEntry(int npcId, bool available, bool selected, bool encountered,
                bool conditionMet, bool modelAvailable)
            {
                NpcId = npcId;
                Available = available;
                Selected = selected;
                Encountered = encountered;
                ConditionMet = conditionMet;
                ModelAvailable = modelAvailable;
            }
        }

        private sealed class Map12SteleNpcDataSource : LoopScrollDataSource
        {
            private readonly Action<Transform, int> bind;

            public Map12SteleNpcDataSource(Action<Transform, int> bind) => this.bind = bind;

            public void ProvideData(Transform transform, int index) => bind(transform, index);
        }

        private void CloseMap12SteleNpcPicker()
        {
            if (map12SteleNpcPicker == null) return;
            GameObject root = map12SteleNpcPicker;
            map12SteleNpcPicker = null;
            var popup = root.GetComponent<WorldPopupWindow>();
            if (popup != null) popup.Hide(() => Destroy(root));
            else Destroy(root);
        }

        private static string Map12SteleAssistKey(int placementId) => "map12-stele:" + placementId;

        private string Map12SteleInviteText(string key)
        {
            if (map12TeamInviteTexts != null && map12TeamInviteTexts.TryGetValue(key, out string value)) return value;
            return NativeUi.Text(key);
        }

        private static void HideMap12RobotOnlineEmptyState(Transform robotPanel)
        {
            if (robotPanel == null) return;
            foreach (WorldMultiLanguageText text in robotPanel.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                if (text.myKey != "teaminvitepanel_no_friend") continue;
                Transform emptyState = text.transform;
                while (emptyState.parent != null && emptyState != robotPanel && !emptyState.name.StartsWith("EmptyTip", StringComparison.Ordinal))
                    emptyState = emptyState.parent;
                if (emptyState.name.StartsWith("EmptyTip", StringComparison.Ordinal))
                    emptyState.gameObject.SetActive(false);
            }
        }

        private static void DisableMap12OnlineInviteLogic(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Type type = behaviour.GetType();
                bool onlineLogic = type.FullName == "Zjcs.Official.TeamUI.TeamInvitePanel";
                for (Type current = type; current != null; current = current.BaseType)
                    onlineLogic |= current.FullName == "Zjcs.Official.TeamUI.TeamInviteSubPanelBase"
                        || current.FullName == "Zjcs.Official.TeamUI.TeamInviteItemBase";
                onlineLogic |= type.FullName == "Zjcs.Official.TeamUI.PlayerCompanionTag"
                    || type.FullName == "Zjcs.Official.TeamUI.TeamPlayerIconMeHpSliderNormal"
                    || type.FullName == "Zjcs.Official.ProfessionUI.AppearanceUI"
                    || type.FullName == "Zjcs.Official.ProfessionUI.AppearanceUIPartHandler"
                    || type.FullName == "Zjcs.Official.ProfessionUI.AppearanceSkinClip";
                if (onlineLogic) UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }

        private static void SetMap12SteleActive(Transform root, string path, bool active)
        {
            Transform node = root == null ? null : root.Find(path);
            if (node != null) node.gameObject.SetActive(active);
        }

        private static void SetMap12SteleButton(Transform root, string path, Action action)
        {
            Button button = FindMap12SteleButton(root, path);
            if (button == null && root != null)
            {
                Transform named = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == path && value.gameObject.activeSelf);
                if (named != null) button = named.GetComponent<Button>() ?? named.GetComponentInChildren<Button>(true);
            }
            if (button == null) throw new InvalidOperationException("Original Map12 button is missing: " + path);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }

        private static Button FindMap12SteleButton(Transform root, string path)
        {
            Transform node = root == null ? null : root.Find(path);
            if (node == null && root != null && !path.Contains("/"))
                node = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(value =>
                    value.name == path && value.gameObject.activeSelf);
            return node == null ? null : node.GetComponent<Button>() ?? node.GetComponentInChildren<Button>(true);
        }

        private static void SetMap12SteleNamedText(Transform root, string name, string value)
        {
            if (root == null) return;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (string.IsNullOrEmpty(name) || text.name == name) { text.text = value; if (!string.IsNullOrEmpty(name)) return; }
        }

        private static void SetFirstMap12SteleText(Transform root, string value)
        {
            TMP_Text text = root == null ? null : root.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = value;
        }

        [Serializable]
        private sealed class Map12TeamInviteTextCatalog
        {
            public string manifest;
            public Map12TeamInviteText[] texts;
        }

        [Serializable]
        private sealed class Map12TeamInviteText
        {
            public string key;
            public string value;
        }

        private sealed class Map12SteleDialogServices : IWorldBattleSteleServices, IPlayerPlayServices,
            IPlayerPlayStateSource
        {
            private readonly OfficialMainlineMapRuntime host;
            private readonly Map10RuntimeEntity entity;
            private readonly OfficialMap12Stele stele;
            private readonly PlayerPlayStateModule playState;
            public Map12SteleDialogServices(OfficialMainlineMapRuntime host, Map10RuntimeEntity entity,
                OfficialMap12Stele stele)
            {
                this.host = host; this.entity = entity; this.stele = stele;
                playState = new PlayerPlayStateModule(this);
            }
            public int PlayerSubRank => host.progression.Player.SubRank;
            public long PlayerCombatRating => host.progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating;
            public int? TeamMembers
            {
                get
                {
                    var selected = host.map12NpcProgress?.Selected(WorldBattleSteleTeamTarget.TargetCatalog,
                        Map12SteleAssistKey(stele.PlacementId));
                    return selected == null || selected.Count == 0 ? (int?)null : selected.Count + 1;
                }
            }
            public event Action TeamPlayerAdded, TeamPlayerRemoved, TeamUpdated;
            public PlayerPlayStateModule PlayStateModule => playState;
            IPlayerPlayStateSource IPlayerPlayServices.MainPlayer => this;
            public IEnumerable<IPlayerPlayState> GetStates() => Array.Empty<IPlayerPlayState>();
            public PlayerPlayStateExitHandleBase GetExitHandle(IPlayerPlayState state, IPlayerPlayAction action,
                IPlayerPlayActionEnv env) => null;
            public void Notify(PlayerPlayText text) => Notify(text.Key);
            public void Error(string message) => Debug.LogError(message, host);
            public PlayerPlayStageInfo GetStageInfo(int stageId)
            {
                if (!host.stages.TryGetValue(stageId, out Map10RuntimeStage stage))
                    throw new InvalidOperationException("Original Map12 stele stage is missing: " + stageId);
                return new PlayerPlayStageInfo(stele.SupportTeam, stage.JoinInMiddle, stage.Background,
                    stage.LeaveInPreparing, stage.LeaveInPlaying);
            }
            public string Compact(long value) => WorldBattleReportNumberFormat.Compact(value);
            public void OpenTeam(WorldBattleSteleTeamTarget target)
            {
                if (target.StageId != stele.StageId || target.GroupId != stele.PeriodicityGroupId)
                    throw new InvalidOperationException("Original Map12 stele team target changed.");
                host.OpenMap12SteleNpcPicker(entity, stele);
            }
            public void Confirm(string key, Action accepted) => host.ConfirmMap12Stele(key, accepted);
            public void Notify(string key) => host.hud.ShowOfficialNotice(host.NativeUi.Text(key));
            public void PopulateRewardPreview(Transform slot, int[] ruleIds, int level, int subRank)
            {
                if (!ruleIds.SequenceEqual(stele.RuleIds)) throw new InvalidOperationException("Map12 stele reward rules changed.");
                var assets = Resources.Load<OfficialMap11UiAssets>(host.NativeUiResource("DynamicItems"));
                OfficialMap12SteleRewardView.Populate(slot, host.map12Steles, stele, assets,
                    host.MapProfile.NativeUiRoot, host.NativeUi.Text,
                    item => host.hud.ShowOfficialNotice(host.NativeUi.Text("item_" + item + "_name")));
            }
            public void RaiseTeamUpdated() => TeamUpdated?.Invoke();
        }

        private void ConfirmMap12Stele(string key, Action accepted)
        {
            GameObject prefab = Resources.Load<GameObject>(NativeUiResource("ConfirmDialog"));
            if (prefab == null) throw new InvalidOperationException("Original confirmation dialog is missing.");
            GameObject root = Instantiate(prefab, hud.ui.OverlayRoot, false);
            foreach (WorldMultiLanguageText text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                text.Localizer = NativeUi.Text;
            WorldConfirmDialog dialog = root.GetComponent<WorldConfirmDialog>();
            if (dialog == null) throw new InvalidOperationException("Original confirmation component is missing.");
            dialog.Init(key, accepted);
            dialog.CloseWindow = () => dialog.PopupWindow.Hide(() => Destroy(root));
            dialog.PopupWindow.Show();
        }

        private void AddMap12SteleNpcUnits(FightBattleCheckpoint checkpoint, Map10RuntimeBattleScene scene,
            Map10RuntimeFactionInfo playerFaction, int moveMask, OfficialMap12Stele stele)
        {
            if (stele == null) return;
            map12NpcProgress ??= OfflineNpcAssistanceProgress.Open(new ProfileSlotCatalog(persistentDataPath), slotId);
            map12NpcCatalog ??= OfficialCombatAssetCatalog.LoadDefault().LoadNpcAssistance();
            IReadOnlyList<int> selected = map12NpcProgress.Selected(WorldBattleSteleTeamTarget.TargetCatalog,
                Map12SteleAssistKey(stele.PlacementId));
            int maximum = Math.Max(0, map12Steles.Data.TeamMaxPlayer - 1);
            if (selected.Count > maximum) throw new InvalidOperationException("Map12 stele NPC roster exceeds the original team capacity.");
            if (selected.Count == 0) return;
            var friendships = (progressionSave.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            IReadOnlyList<int> candidates = map12NpcCatalog.TeamInviteNpcClassIds();
            foreach (int npcId in selected)
            {
                bool met = map12NpcCatalog.AssistConditionMet(npcId, progression.Player.Level,
                    friendships, progressionSave.StoryCount);
                if (!candidates.Contains(npcId) || !OfficialMap12NpcBattleModelView.HasBinding(npcId)
                    || !map12NpcCatalog.CanInviteTeamNpc(npcId, friendships.ContainsKey(npcId), met))
                    throw new InvalidOperationException("Selected Map12 stele NPC no longer meets the original assistance conditions: " + npcId);
            }
            if (playerFaction.EntryPosList == null || playerFaction.EntryPosList.Length == 0)
                throw new InvalidDataException("Original Map12 stele scene has no player entry position.");
            Map10RuntimePoint entry = playerFaction.EntryPosList[0];
            var occupied = new HashSet<FightGridPoint>(checkpoint.Units.Select(unit => new FightGridPoint(unit.X, unit.Y)));
            var positions = (scene.FightGridGroundComponent.PosInfos ?? Array.Empty<Map10RuntimeBattleCell>())
                .Where(cell => cell != null && cell.Height < 999)
                .Select(cell => new FightGridPoint(cell.Pos.X, cell.Pos.Y))
                .Distinct()
                .OrderBy(point => Math.Abs(point.X - entry.X) + Math.Abs(point.Y - entry.Y))
                .ThenBy(point => point.Y).ThenBy(point => point.X)
                .Where(point => checkpoint.Grid.CanStand(point, moveMask, occupied))
                .Take(selected.Count).ToArray();
            if (positions.Length != selected.Count)
                throw new InvalidDataException("Original Map12 stele scene has too few NPC entry positions.");
            for (int index = 0; index < selected.Count; index++)
            {
                FightGridPoint position = positions[index];
                FightBattleUnitState npc = map12NpcCatalog.CreateTeamInviteUnit(checkpoint.Units.Count + 1, 1,
                    Math.Max(1, progression.Player.Level), Math.Max(0, progression.Player.Rank),
                    Math.Max(0, progression.Player.SubRank), position.X, position.Y, moveMask, selected[index]);
                checkpoint.Units.Add(npc);
                occupied.Add(position);
            }
        }
    }
}
