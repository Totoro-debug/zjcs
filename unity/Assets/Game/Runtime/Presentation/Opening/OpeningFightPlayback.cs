using System;
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Opening
{
    /// <summary>
    /// Replays the pinned CreatePlayerFightRecord through the client message order:
    /// start messages, then each round's steps at the record's own relative times.
    /// </summary>
    public sealed class OpeningFightPlayback
    {
        private const float SkipButtonDelaySeconds = 0.5f;

        private readonly OpeningAssets assets;
        private readonly OpeningUiView ui;
        private readonly Action<string> log;
        private readonly Func<bool> showSkipButton;
        private readonly OfficialBattleModelCatalog models;
        private readonly Transform world;
        private readonly OpeningBattlefield battlefield;
        private readonly Dictionary<int, SkeletonAnimation> units = new Dictionary<int, SkeletonAnimation>();
        private readonly Dictionary<int, OfficialBattleModelBinding> bindings =
            new Dictionary<int, OfficialBattleModelBinding>();
        private readonly Dictionary<int, OpeningRecordEntity> entityState = new Dictionary<int, OpeningRecordEntity>();
        private GameObject hud;
        private GameObject skipButton;
        private TMPro.TMP_Text roundText;
        private string roundTemplate = string.Empty;
        private Transform timelineContainer;
        private readonly List<GameObject> timelineItems = new List<GameObject>();
        private readonly Dictionary<int, GameObject> timelineById = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, float> nextActionTime = new Dictionary<int, float>();
        private int actingEntityId;
        private bool timelineUnavailable;
        private OfficialBattleHudCatalog timelineCatalog;
        private RectTransform damageLayer;
        private TMPro.TMP_FontAsset damageFont;
        private bool damageFontLoaded;
        private bool skipRequested;
        private bool skipButtonPending;
        private float skipButtonAvailableAt;
        private float scaleFactor;
        private readonly HashSet<string> skippedModels = new HashSet<string>(StringComparer.Ordinal);

        public OpeningFightPlayback(OpeningAssets assets, OpeningUiView ui, OfficialBattleModelCatalog models,
            Transform world, OpeningBattlefield battlefield, Func<bool> showSkipButton, Action<string> log)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.models = models;
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.battlefield = battlefield;
            this.showSkipButton = showSkipButton ?? (() => false);
            this.log = log ?? (_ => { });
        }

        public bool SkipRequested => skipRequested;
        public bool Finished { get; private set; }
        public int Round { get; private set; }
        public int SpawnedUnits => units.Count;
        public int PreparedUnits { get; private set; }
        public int TimelineItems => timelineItems.Count;
        public int DamageNumbers { get; private set; }
        public int ActionAnimations { get; private set; }
        public int DieActions { get; private set; }
        public int DeathHolds { get; private set; }

        public void RequestFastForward()
        {
            skipRequested = true;
        }

        /// <summary>
        /// Official order: the stage scene loads, then GAME_ENTITY_LIST places every
        /// unit, and only then does the story controller play BeforeFight.
        /// </summary>
        public IEnumerator Prepare()
        {
            OpeningRecord record = assets.Record;
            MountHud();
            foreach (OpeningRecordEntity entity in record.Entities)
            {
                entityState[entity.EntityId] = entity;
                TryCreateUnit(entity);
            }
            FrameCamera();
            yield return null;
        }

        public IEnumerator Play()
        {
            OpeningRecord record = assets.Record;
            if (units.Count == 0 && PreparedUnits == 0)
            {
                MountHud();
                foreach (OpeningRecordEntity entity in record.Entities)
                {
                    entityState[entity.EntityId] = entity;
                    TryCreateUnit(entity);
                }
                FrameCamera();
                yield return null;
            }

            skipButtonPending = showSkipButton();
            skipButtonAvailableAt = Time.time + SkipButtonDelaySeconds;

            for (int roundIndex = 0; roundIndex < record.Rounds.Count; roundIndex++)
            {
                if (skipRequested) break;
                OpeningRecordRound round = record.Rounds[roundIndex];
                float roundStart = Time.time;
                foreach (OpeningRecordStep step in round.Steps)
                {
                    if (skipRequested) break;
                    float dispatchAt = roundStart + step.Time / record.FightSpeed;
                    while (Time.time < dispatchAt)
                    {
                        ShowSkipButtonWhenReady();
                        yield return null;
                    }
                    ShowSkipButtonWhenReady();
                    Dispatch(step);
                }
                yield return null;
            }

            if (skipRequested) ApplyFastForwardSnapshot(record);

            Finished = true;
            Dispose();
        }

        public void Dispose()
        {
            foreach (SkeletonAnimation unit in units.Values)
                if (unit != null) UnityEngine.Object.Destroy(unit.gameObject);
            units.Clear();
            bindings.Clear();
            entityState.Clear();
            if (hud != null) UnityEngine.Object.Destroy(hud);
            hud = null;
            skipButton = null;
        }

        private void FrameCamera()
        {
            if (battlefield == null || battlefield.StageCamera == null) return;
            Camera camera = battlefield.StageCamera;
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (OpeningRecordEntity entity in entityState.Values)
            {
                if (entity.GridX == 0 && entity.GridY == 0) continue;
                center += new Vector3(entity.GridX, entity.GridY, 0f);
                count++;
            }
            if (count > 0) center /= count;
            else center = new Vector3(battlefield.Size.x * 0.5f, battlefield.Size.y * 0.5f, 0f);
            Vector3 position = camera.transform.position;
            Vector3 gridCenter = battlefield.GridToWorld(center.x, center.y);
            camera.transform.position = new Vector3(gridCenter.x, gridCenter.y, position.z);
        }

        private void UpdateRoundText()
        {
            if (roundText == null) return;
            int total = assets.Record.Rounds.Count;
            int current = Mathf.Clamp(Round + 1, 1, Mathf.Max(1, total));
            if (string.IsNullOrEmpty(roundTemplate)) roundText.text = current + "/" + total;
            else roundText.text = roundTemplate.Replace("{0}", current.ToString())
                .Replace("{1}", total.ToString());
        }

        private void ShowSkipButtonWhenReady()
        {
            if (!skipButtonPending || Time.time < skipButtonAvailableAt || skipButton == null) return;
            skipButtonPending = false;
            skipButton.SetActive(true);
        }

        private void MountHud()
        {
            if (!assets.Ui.Prefabs.ContainsKey("record_fight_hud"))
            {
                log("官方录像战斗面板未随载荷交付，跳过 HUD");
                return;
            }
            hud = ui.Create("record_fight_hud");
            hud.SetActive(true);
            foreach (TMPro.TMP_Text text in hud.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (!text.name.EndsWith("RoundText", StringComparison.Ordinal)) continue;
                roundText = text;
                roundTemplate = text.text ?? string.Empty;
                break;
            }
            if (roundText == null) log("官方录像回合文本节点缺失");
            var layerObject = new GameObject("OpeningDamageLayer", typeof(RectTransform));
            layerObject.transform.SetParent(ui.Root, false);
            damageLayer = (RectTransform)layerObject.transform;
            damageLayer.anchorMin = Vector2.zero;
            damageLayer.anchorMax = Vector2.one;
            damageLayer.offsetMin = Vector2.zero;
            damageLayer.offsetMax = Vector2.zero;
            timelineContainer = ui.Find("/FightMainPanelRecordFight/Content/Top/FixedContent/RoundUI/Linear")?.transform;
            if (timelineContainer == null) log("官方录像行动时间轴容器缺失，跳过时间轴");
            skipButton = FindNode(hud, "SkipBtn");
            if (skipButton != null)
            {
                skipButton.SetActive(false);
                Button button = skipButton.GetComponent<Button>() ?? skipButton.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(RequestFastForward);
                }
            }
        }

        private static GameObject FindNode(GameObject root, string suffix)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.EndsWith(suffix, StringComparison.Ordinal)) return child.gameObject;
            return null;
        }

        private void Dispatch(OpeningRecordStep step)
        {
            switch (step.MessageType)
            {
                case "GAME_ENTITY_ADD":
                    if (step.Data.TryGetValue("Entity", out object raw) && raw is Dictionary<string, object> entity)
                        AddEntity(entity);
                    break;
                case "GAME_ENTITY_REMOVE":
                    if (step.Data.TryGetValue("EntityId", out object removed) && removed is Dictionary<string, object> id)
                        RemoveEntity(OpeningJson.Int(id, "EntityId"));
                    break;
                case "GAME_MODIFACTION_RUN":
                    ApplyModification(step);
                    break;
                case "GAME_PLAY_STATUS_CHANGE":
                    break;
                case "GAME_FASTFORWARD":
                    skipRequested = true;
                    break;
                case "GAME_MODIFACTION_RUN_BATTLE_END":
                case "GAME_MODIFACTION_RUN_END":
                    break;
                default:
                    log("录像步骤类型未处理: " + step.MessageType);
                    break;
            }
        }

        private void AddEntity(Dictionary<string, object> raw)
        {
            OpeningRecordEntity entity = OpeningRecordEntity.Parse(raw);
            entityState[entity.EntityId] = entity;
            TryCreateUnit(entity);
            if (raw.TryGetValue("ComponentList", out object components) && components is List<object> list)
            {
                foreach (object item in list)
                {
                    if (!(item is Dictionary<string, object> component)) continue;
                    string type = OpeningJson.String(component, "$type");
                    if (type.Contains("FightDamageComponent")) ShowDamage(component);
                    else if (type.Contains("ActionComponent")) PlayAction(component);
                }
            }
        }

        /// <summary>
        /// The record carries the official action type for each acting unit. Only the
        /// unambiguous ones map onto the converted Spine animation names.
        /// </summary>
        private void PlayAction(Dictionary<string, object> component)
        {
            string actionType = OpeningJson.String(component, "ActionType");
            string alias;
            switch (actionType)
            {
                case "Skill": alias = "skill_01"; break;
                case "AirCombo": alias = "attack_01"; break;
                case "Move": alias = "run_01"; break;
                case "Die": alias = "die_01"; DieActions++; break;
                default: return;
            }
            if (!component.TryGetValue("ApplyId", out object applied) || !(applied is Dictionary<string, object> id)) return;
            int unitId = OpeningJson.Int(id, "EntityId");
            if (!units.TryGetValue(unitId, out SkeletonAnimation unit) || unit == null) return;
            if (!bindings.TryGetValue(unitId, out OfficialBattleModelBinding binding)) return;
            string clip = ResolveAlias(binding, alias);
            if (clip == null) return;
            unit.AnimationState.SetAnimation(0, clip, false);
            string idle = ResolveAlias(binding, "idle_01");
            if (idle != null) unit.AnimationState.AddAnimation(0, idle, true, 0f);
            ActionAnimations++;
        }

        private string ResolveAlias(OfficialBattleModelBinding binding, string alias)
        {
            foreach (OfficialBattleAnimationAlias animation in binding.animations)
                if (animation.name == alias) return animation.spineName;
            return binding.skeleton != null && binding.skeleton.GetSkeletonData(true).FindAnimation(alias) != null
                ? alias : null;
        }

        private void ShowDamage(Dictionary<string, object> component)
        {
            if (damageLayer == null || battlefield == null || battlefield.StageCamera == null) return;
            if (!component.TryGetValue("DamageResult", out object resultValue)
                || !(resultValue is Dictionary<string, object> result)) return;
            long value = (long)OpeningJson.Number(result, "DamageValue");
            if (value == 0) return;
            string type = OpeningJson.String(result, "ResultType");
            string element = OpeningJson.String(result, "SkillElementType");
            bool crit = false, block = false, immunity = false;
            if (result.TryGetValue("DamageTypeData", out object flagsValue)
                && flagsValue is Dictionary<string, object> flags)
            {
                crit = OpeningJson.Bool(flags, "IsCrit");
                block = OpeningJson.Bool(flags, "IsBlock");
                immunity = OpeningJson.Bool(flags, "IsImmunity");
                if (OpeningJson.Bool(flags, "IsDodge")) return;
            }
            if (!result.TryGetValue("Pos", out object posValue) || !(posValue is Dictionary<string, object> position)) return;
            Vector3 world = battlefield.GridToWorld(OpeningJson.Int(position, "X"),
                OpeningJson.Int(position, "Y")) + new Vector3(0f, 0.7f, 0f);
            Vector3 screen = battlefield.StageCamera.WorldToScreenPoint(world);
            if (screen.z < 0f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(damageLayer, screen, null,
                    out Vector2 local)) return;
            var labelObject = new GameObject("OpeningDamage", typeof(RectTransform));
            labelObject.transform.SetParent(damageLayer, false);
            var label = labelObject.AddComponent<TMPro.TextMeshProUGUI>();
            label.font = ResolveDamageFont();
            label.fontSize = 36f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.raycastTarget = false;
            bool cure = string.Equals(type, "Cure", StringComparison.Ordinal);
            long shown = Math.Abs(value);
            label.text = cure ? "+" + shown : shown.ToString();
            string variant = immunity ? "immunity" : crit ? "crit" : block ? "block" : "normal";
            Color color = battlefield.ResolveDamageColor(element, variant, type);
            labelObject.AddComponent<OpeningDamageFloat>().Initialize(label, local, color);
            DamageNumbers++;
        }

        private TMPro.TMP_FontAsset ResolveDamageFont()
        {
            if (damageFontLoaded) return damageFont;
            damageFontLoaded = true;
            try
            {
                OfficialBattleHudCatalog catalog = OfficialBattleHudCatalog.LoadDefault();
                if (catalog != null) damageFont = catalog.Font;
            }
            catch (Exception error)
            {
                log("官方伤害数字字体不可用，使用 TMP 默认字体: " + error.Message);
            }
            if (damageFont == null) damageFont = TMPro.TMP_Settings.defaultFontAsset;
            return damageFont;
        }

        private void RemoveEntity(int entityId)
        {
            entityState.Remove(entityId);
            nextActionTime.Remove(entityId);
            if (units.TryGetValue(entityId, out SkeletonAnimation defeated) && defeated != null)
            {
                // Official plays a death animation before the unit disappears; keep the
                // model for a short while instead of destroying it on the remove message.
                if (bindings.TryGetValue(entityId, out OfficialBattleModelBinding deathBinding))
                {
                    string die = ResolveAlias(deathBinding, "die_01");
                    if (die != null)
                    {
                        defeated.AnimationState.SetAnimation(0, die, false);
                        defeated.gameObject.AddComponent<OpeningDelayedDestroy>();
                        DeathHolds++;
                    }
                }
                units.Remove(entityId);
                bindings.Remove(entityId);
                return;
            }
            if (timelineById.TryGetValue(entityId, out GameObject timelineItem))
            {
                if (timelineItem != null) UnityEngine.Object.Destroy(timelineItem);
                timelineById.Remove(entityId);
                timelineItems.Remove(timelineItem);
            }
            if (!units.TryGetValue(entityId, out SkeletonAnimation unit)) return;
            if (unit != null) UnityEngine.Object.Destroy(unit.gameObject);
            units.Remove(entityId);
            bindings.Remove(entityId);
        }

        private void ApplyModification(OpeningRecordStep step)
        {
            string type = step.DataType;
            switch (type)
            {
                case "FightRoundActingDataModification":
                    PlayActing(step);
                    break;
                case "FightRoundNewModification":
                    Round = OpeningJson.Int(step.Data, "GlobalRound");
                    UpdateRoundText();
                    RebuildTimeline();
                    break;
                case "FightRoundUpdateDataModification":
                    ApplyRoundUpdate(step);
                    break;
                case "FightPersonalRoundNewModification":
                case "FightRootStatusDataModification":
                    break;
                case "FightStatusInvokeModification":
                case "FightRoleEnemyInViewModification":
                case "FightRoundRemoveDataModification":
                case "FightRoundFinishModification":
                case "FightRoleResilienceModification":
                    break;
                default:
                    break;
            }
        }

        private void ApplyRoundUpdate(OpeningRecordStep step)
        {
            if (!step.Data.TryGetValue("Data", out object raw) || !(raw is Dictionary<string, object> data)) return;
            if (!data.TryGetValue("EntityId", out object idValue) || !(idValue is Dictionary<string, object> id)) return;
            int entityId = OpeningJson.Int(id, "EntityId");
            if (entityId <= 0) return;
            nextActionTime[entityId] = (float)OpeningJson.Number(data, "NextTime");
        }

        private void RebuildTimeline()
        {
            if (timelineContainer == null || timelineUnavailable) return;
            foreach (GameObject item in timelineItems) if (item != null) UnityEngine.Object.Destroy(item);
            timelineItems.Clear();
            timelineById.Clear();
            if (timelineCatalog == null)
            {
                try { timelineCatalog = OfficialBattleHudCatalog.LoadDefault(); }
                catch (Exception error)
                {
                    timelineUnavailable = true;
                    log("官方行动时间轴资源不可用: " + error.Message);
                    return;
                }
                if (timelineCatalog == null) { timelineUnavailable = true; return; }
            }
            var renderer = new OfficialBattleHudRenderer(timelineCatalog);
            // Only rendered fighters belong on the action timeline; record effect
            // entities also live in entityState and must not create entries.
            var ids = new List<int>(units.Keys);
            ids.Sort((left, right) =>
            {
                float leftTime = nextActionTime.TryGetValue(left, out float l) ? l : float.MaxValue;
                float rightTime = nextActionTime.TryGetValue(right, out float r) ? r : float.MaxValue;
                int compare = leftTime.CompareTo(rightTime);
                return compare != 0 ? compare : left.CompareTo(right);
            });
            float width = ((RectTransform)timelineContainer).rect.width;
            if (width <= 1f) width = 800f;
            for (int index = 0; index < ids.Count; index++)
            {
                GameObject item;
                try { item = renderer.Create("round_character", timelineContainer); }
                catch (Exception error)
                {
                    timelineUnavailable = true;
                    log("官方行动时间轴条目创建失败: " + error.Message);
                    return;
                }
                item.name = "RoundCharacter_" + ids[index];
                item.SetActive(true);
                var rect = (RectTransform)item.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-width * 0.5f + (index + 0.5f) * width / ids.Count, 0f);
                timelineItems.Add(item);
                timelineById[ids[index]] = item;
            }
            ApplyActingHighlight();
        }

        private void ApplyActingHighlight()
        {
            foreach (KeyValuePair<int, GameObject> entry in timelineById)
            {
                if (entry.Value == null) continue;
                bool acting = entry.Key == actingEntityId;
                foreach (Transform child in entry.Value.GetComponentsInChildren<Transform>(true))
                    if (child.name == "WeGo") child.gameObject.SetActive(acting);
            }
        }

        private void PlayActing(OpeningRecordStep step)
        {
            if (!step.Data.TryGetValue("RoundEntityId", out object raw) || !(raw is Dictionary<string, object> id)) return;
            int entityId = OpeningJson.Int(id, "EntityId");
            actingEntityId = entityId;
            ApplyActingHighlight();
            if (units.TryGetValue(entityId, out SkeletonAnimation unit) && unit != null)
                Play(entityId, unit, "attack_01", false);
        }

        private void ApplyFastForwardSnapshot(OpeningRecord record)
        {
            // FastForward dispatches only BATTLE_END/RUN_END/FASTFORWARD; the final entity
            // snapshot in the record's fast-forward message is applied here.
            foreach (OpeningRecordRound round in record.Rounds)
                foreach (OpeningRecordStep step in round.Steps)
                {
                    if (step.MessageType != "GAME_FASTFORWARD") continue;
                    if (!step.Data.TryGetValue("EcEntities", out object raw) || !(raw is List<object> list)) continue;
                    var present = new HashSet<int>();
                    foreach (object item in list)
                    {
                        OpeningRecordEntity entity = OpeningRecordEntity.Parse(OpeningJson.Object(item, "snapshot entity"));
                        present.Add(entity.EntityId);
                        entityState[entity.EntityId] = entity;
                        if (!units.ContainsKey(entity.EntityId)) TryCreateUnit(entity);
                    }
                    foreach (int existing in new List<int>(units.Keys))
                        if (!present.Contains(existing)) RemoveEntity(existing);
                }
        }

        /// <summary>
        /// The opening record's NPC characters are official NPC Spine models. Their
        /// skeletons live in the shared battle Spine payload but are not monster
        /// identities, so the opening catalog binds them directly by NpcClassId.
        /// </summary>
        private void OpenNpcBinding(OpeningRecordEntity entity, out OfficialBattleModelBinding binding,
            out string key)
        {
            binding = null;
            key = string.Empty;
            OpeningNpcModelCatalog npcCatalog = OpeningNpcModelCatalog.LoadDefault();
            if (npcCatalog != null && npcCatalog.TryGet(entity.NpcClassId, out Spine.Unity.SkeletonDataAsset skeleton,
                    out string skin, out float scale))
            {
                key = "opening_npc_" + entity.NpcClassId;
                binding = new OfficialBattleModelBinding
                {
                    key = key,
                    skin = skin,
                    scale = scale,
                    skeleton = skeleton,
                    animations = Array.Empty<OfficialBattleAnimationAlias>(),
                };
                PreparedUnits++;
                return;
            }
            if (battlefield != null && battlefield.TryGetNpcModel(entity.NpcClassId, out key))
            {
                PreparedUnits++;
                binding = models.Find(key);
            }
        }

        private void TryCreateUnit(OpeningRecordEntity entity)
        {
            if (entity == null || units.ContainsKey(entity.EntityId)) return;
            if (models == null) return;
            string key;
            OfficialBattleModelBinding binding;
            if (entity.CharacterType == "Npc")
            {
                OpenNpcBinding(entity, out binding, out key);
                if (binding == null)
                {
                    log("录像剧情角色缺少官方 NPC 骨骼绑定: " + entity.EntityId + " ("
                        + entity.NpcClassId + " " + entity.Profession + ")");
                    return;
                }
            }
            else
            {
                if (entity.MonsterId <= 0) return;
                if (!assets.Stage.Models.TryGetValue(entity.MonsterId, out key) || string.IsNullOrEmpty(key))
                {
                    log("官方怪物模型标识缺失: " + entity.MonsterId);
                    return;
                }
                PreparedUnits++;
                binding = models.Find(key);
                if (binding == null)
                {
                    log("官方战斗模型绑定缺失: " + key);
                    return;
                }
            }
            var gameObject = new GameObject("RecordUnit_" + entity.EntityId);
            gameObject.transform.SetParent(world, false);
            SkeletonAnimation skeleton = gameObject.AddComponent<SkeletonAnimation>();
            skeleton.skeletonDataAsset = binding.skeleton;
            skeleton.initialSkinName = binding.skin;
            skeleton.Initialize(true);
            if (skeleton.Skeleton == null)
            {
                log("官方战斗骨骼初始化失败: " + key);
                UnityEngine.Object.Destroy(gameObject);
                return;
            }
            if (!HasBoundTexture(skeleton.SkeletonDataAsset))
            {
                // The pinned battle Spine payload ships most models without their atlas
                // texture page; rendering it would show an untextured white mesh.
                if (skippedModels.Add(key)) log("官方战斗模型缺少贴图页，暂不显示: " + key);
                UnityEngine.Object.Destroy(gameObject);
                return;
            }
            // The unit root already carries the stage root plus the official grid origin.
            gameObject.transform.localPosition = new Vector3(entity.GridX, entity.GridY, 0);
            // The converted SkeletonDataAssets already carry the official import scale
            // (0.01), so the pose bounds are measured instead of re-applying the catalog
            // scale. The first model establishes one shared world factor that keeps the
            // official relative sizes between units.
            if (scaleFactor <= 0f)
            {
                float height = MeasureHeight(skeleton.Skeleton);
                scaleFactor = height > 0.0001f ? 1.2f / height : 1f;
            }
            gameObject.transform.localScale = Vector3.one * scaleFactor;
            // The official stage renders its ground through sorting layers up to 21;
            // fighters must sort in front of that scenery.
            foreach (MeshRenderer renderer in gameObject.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sortingOrder = 30000 - entity.GridY * 2;
            units.Add(entity.EntityId, skeleton);
            bindings.Add(entity.EntityId, binding);
            Play(entity.EntityId, skeleton, "idle_01", true);
        }

        private static float MeasureHeight(Spine.Skeleton skeleton)
        {
            if (skeleton == null) return 0f;
            float[] buffer = null;
            skeleton.GetBounds(out _, out _, out _, out float height, ref buffer);
            return height;
        }

        private static bool HasBoundTexture(SkeletonDataAsset data)
        {
            if (data == null || data.atlasAssets == null) return false;
            foreach (Spine.Unity.AtlasAssetBase atlas in data.atlasAssets)
            {
                if (atlas == null) continue;
                foreach (Material material in atlas.Materials)
                    if (material != null && material.GetTexture("_MainTex") != null) return true;
            }
            return false;
        }

        private void Play(int entityId, SkeletonAnimation skeleton, string alias, bool loop)
        {
            if (skeleton == null || !bindings.TryGetValue(entityId, out OfficialBattleModelBinding binding)) return;
            foreach (OfficialBattleAnimationAlias animation in binding.animations)
            {
                if (animation.name != alias) continue;
                skeleton.AnimationState.SetAnimation(0, animation.spineName, loop);
                return;
            }
            // NPC skeletons are not part of the monster alias table; their official
            // clips carry the same names, so use the exact clip when present.
            if (skeleton.Skeleton != null && skeleton.Skeleton.Data.FindAnimation(alias) != null)
                skeleton.AnimationState.SetAnimation(0, alias, loop);
        }
    }
}
