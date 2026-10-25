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
        private readonly Dictionary<int, bool> timelineActing = new Dictionary<int, bool>();
        private readonly Dictionary<int, float> nextActionTime = new Dictionary<int, float>();
        private int actingEntityId;
        private bool timelineUnavailable;
        private OfficialBattleHudCatalog timelineCatalog;
        private RectTransform damageLayer;
        private OpeningBattlePresentation presentation;
        private OpeningBattleActionPlayer actionPlayer;
        private readonly Dictionary<int, Transform> actorRoots = new Dictionary<int, Transform>();
        private readonly Dictionary<int, float> roundIntervals = new Dictionary<int, float>();
        private OpeningSkillEffectPlayer skillEffects;
        private int completedEffects, completedSystems, completedSoundEvents;
        private readonly List<Material> modelMaterials = new List<Material>();
        private bool skipRequested;
        private bool skipButtonPending;
        private float skipButtonAvailableAt;
        private readonly HashSet<string> skippedModels = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, Dictionary<string, object>> modelTransforms =
            new Dictionary<int, Dictionary<string, object>>();

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
            var transforms = OpeningJson.Object(OpeningJson.Parse(System.IO.File.ReadAllBytes(
                System.IO.Path.Combine(Application.streamingAssetsPath, "Opening", "model-transforms.json"))),
                "opening model transforms");
            if (OpeningJson.String(transforms, "manifest") != OpeningAssets.Manifest)
                throw new FormatException("Opening model transforms are not pinned.");
            foreach (object value in OpeningJson.Array(transforms["units"], "model transforms"))
            {
                var entry = OpeningJson.Object(value, "model transform");
                modelTransforms.Add(OpeningJson.Int(entry, "class_id"), entry);
            }
        }

        public bool SkipRequested => skipRequested;
        public bool Finished { get; private set; }
        public int Round { get; private set; }
        public int SpawnedUnits => units.Count;
        public int PreparedUnits { get; private set; }
        public int TimelineItems => timelineItems.Count;
        public int DamageNumbers { get; private set; }
        public int SkillEffectsSpawned => skillEffects != null ? skillEffects.SpawnedEffects : completedEffects;
        public int SkillParticleSystems => skillEffects != null ? skillEffects.SpawnedSystems : completedSystems;
        public int SkillSoundEvents => skillEffects != null ? skillEffects.SpawnedSounds : completedSoundEvents;
        public int SkillSoundsPlaying => skillEffects != null ? skillEffects.PlayingSounds : 0;
        public int ActiveStatusEffects => skillEffects != null ? skillEffects.SpawnedStatusEffects : 0;
        public int StatusVisualsSpawned { get; private set; }
        public int StatusAnimations { get; private set; }
        public int ActionAnimations { get; private set; }
        public int DieActions { get; private set; }
        public int DeathHolds { get; private set; }
        public int StackedDamages { get; private set; }
        public OpeningBattleActionPlayer Actions => actionPlayer;
        private readonly Dictionary<int, List<float>> recentDamage = new Dictionary<int, List<float>>();

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
            MountPresentation();
            MountHud();
            try { skillEffects = OpeningSkillEffectPlayer.LoadDefault(log); }
            catch (Exception error) { log("开场技能特效载荷不可用: " + error.Message); }
            foreach (OpeningRecordEntity entity in record.Entities)
            {
                entityState[entity.EntityId] = entity;
                TryCreateUnit(entity);
            }
            FrameCamera();
            UpdateRoundText();
            RebuildTimeline();
            yield return null;
        }

        public IEnumerator Play()
        {
            OpeningRecord record = assets.Record;
            if (units.Count == 0 && PreparedUnits == 0)
            {
                MountPresentation();
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
            if (skillEffects != null)
            {
                completedEffects = skillEffects.SpawnedEffects;
                completedSystems = skillEffects.SpawnedSystems;
                completedSoundEvents = skillEffects.SpawnedSounds;
            }
            skillEffects?.Dispose();
            skillEffects = null;
            foreach (Transform root in actorRoots.Values)
                if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            actorRoots.Clear();
            if (presentation != null) UnityEngine.Object.Destroy(presentation.gameObject);
            if (damageLayer != null) UnityEngine.Object.Destroy(damageLayer.gameObject);
            units.Clear();
            foreach (Material material in modelMaterials) UnityEngine.Object.Destroy(material);
            modelMaterials.Clear();
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

        private void MountPresentation()
        {
            if (presentation != null) return;
            var go = new GameObject("OpeningBattlePresentation");
            go.transform.SetParent(world, false);
            presentation = go.AddComponent<OpeningBattlePresentation>();
            presentation.Initialize(battlefield.StageCamera);
            actionPlayer = go.AddComponent<OpeningBattleActionPlayer>();
            actionPlayer.Initialize();
            actionPlayer.AnimationPlayed += (_, __) => ActionAnimations++;
            foreach (OpeningRecordEntity entity in assets.Record.Entities)
                foreach (var pair in entity.RoundTimes)
                { nextActionTime[pair.Key] = pair.Value; roundIntervals[pair.Key] = pair.Value; }
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
            timelineContainer = ui.Find(presentation.TimelinePath)?.transform;
            if (timelineContainer == null) log("官方录像行动时间轴容器缺失，跳过时间轴");
            else timelineContainer.gameObject.SetActive(true);
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
            string effectType = entity.Components.Contains("FightSkillComponent") ? "cast"
                : entity.Components.Contains("FightDamageComponent") ? "damage"
                : entity.Components.Contains("FightStatusComponent") ? "status" : string.Empty;
            if (skillEffects != null && battlefield != null && effectType == "cast")
            {
                SkeletonAnimation sourceUnit = FindUnit(entity.SourceEntityId);
                SkeletonAnimation targetUnit = FindUnit(entity.TargetEntityId);
                Vector3 skillPosition = battlefield.CellToWorld(entity.GridX, entity.GridY);
                Vector3 sourcePosition = sourceUnit != null ? sourceUnit.transform.position : skillPosition;
                Vector3 targetPosition = targetUnit != null ? targetUnit.transform.position
                    : battlefield.CellToWorld(entity.TargetGridX, entity.TargetGridY);
                skillEffects.Play("cast", entity.ClassId, skillPosition, battlefield.Root.transform,
                    entity.EntityId, sourceUnit, targetUnit, battlefield.StageCamera,
                    sourcePosition, targetPosition, entity.SkillRotation);
                // MyFightSkill.OnStart plays the skill's own playable. Action 20001
                // only owns its lifetime and has no myEffect animation prefab.
                SchedulePlayableAnimations("cast", entity.ClassId, entity.EntityId, entity.SourceEntityId);
            }
            if (raw.TryGetValue("ComponentList", out object components) && components is List<object> list)
            {
                foreach (object item in list)
                {
                    if (!(item is Dictionary<string, object> component)) continue;
                    string type = OpeningJson.String(component, "$type");
                    if (type.Contains("FightDamageComponent"))
                    {
                        ShowDamage(component);
                        if (skillEffects != null && battlefield != null)
                        {
                            Dictionary<string, object> result = OpeningJson.Object(component["DamageResult"], "damage result");
                            Dictionary<string, object> position = OpeningJson.Object(result["Pos"], "damage position");
                            int targetId = ReadEntityReference(component, "TargetId");
                            int sourceId = ReadEntityReference(component, "SkillSrcEntityId");
                            SkeletonAnimation targetUnit = FindUnit(targetId);
                            SkeletonAnimation sourceUnit = FindUnit(sourceId);
                            Vector3 targetPosition = battlefield.CellToWorld(OpeningJson.Int(position, "X"),
                                OpeningJson.Int(position, "Y"));
                            Vector3 sourcePosition = sourceUnit != null ? sourceUnit.transform.position : targetPosition;
                            skillEffects.Play("damage", entity.ClassId,
                                targetPosition, battlefield.Root.transform, entity.EntityId, sourceUnit, targetUnit,
                                battlefield.StageCamera, sourcePosition, targetPosition,
                                sourceUnit != null ? Quaternion.Euler(0f, 0f, sourceUnit.transform.eulerAngles.z)
                                    : Quaternion.identity);
                        }
                    }
                    else if (type.Contains("ActionComponent"))
                    {
                        if (entity.ClassId != 20001)
                            PlayAction(component, entity.ClassId, effectType, entity.EntityId);
                    }
                }
                foreach (object item in list)
                {
                    if (!(item is Dictionary<string, object> component)
                        || !OpeningJson.String(component, "$type").Contains("FightActionSkillMoveComponent")) continue;
                    var action = list.Find(value => value is Dictionary<string, object> candidate
                        && OpeningJson.String(candidate, "$type").StartsWith("Common.ActionComponent,")) as Dictionary<string, object>;
                    int unitId = ReadEntityReference(action, "ApplyId");
                    if (actorRoots.TryGetValue(unitId, out Transform actor) && entityState.TryGetValue(unitId, out var state))
                        actionPlayer.MoveAlong(entity.EntityId, entity.ClassId, actor, state, component, assets.Record.FightSpeed);
                }
                if (effectType == "status") PlayStatusEntity(entity, list);
            }
        }

        private SkeletonAnimation FindUnit(int entityId)
        {
            return entityId > 0 && units.TryGetValue(entityId, out SkeletonAnimation unit) ? unit : null;
        }

        private static int ReadEntityReference(Dictionary<string, object> component, string key)
        {
            return component != null && component.TryGetValue(key, out object raw) && raw is Dictionary<string, object> reference
                ? OpeningJson.Int(reference, "EntityId") : 0;
        }

        private void SchedulePlayableAnimations(string type, int classId, int owner, int unitId)
        {
            if (skillEffects == null || !units.TryGetValue(unitId, out var unit)
                || !bindings.TryGetValue(unitId, out var binding)) return;
            var events = new List<Dictionary<string, object>>(skillEffects.GetEvents(type, classId));
            if (type == "cast") events.AddRange(actionPlayer.GetEvents(classId));
            foreach (var entry in events)
            {
                string kind = OpeningJson.String(entry, "kind");
                if (kind != "override" && kind != "animator") continue;
                var source = OpeningJson.Object(entry["source_event"], "animator event");
                var fields = OpeningJson.Object(source["fields"], "animator fields");
                string trigger = OpeningJson.String(fields, "myTriggerParam");
                string animation = kind == "override" ? OpeningJson.String(source, "animation") : trigger;
                // Full rig-qualified override names must resolve before controller aliases.
                string clip = ResolveAlias(binding, animation) ?? ResolveAlias(binding, animation + "_01");
                if (clip == null) { LogUnsupportedPlayableAnimation(type, classId); continue; }
                actionPlayer.PlayAnimation(owner, unitId, classId, unit, clip, ResolveAlias(binding, "idle_01"),
                    (float)OpeningJson.Number(entry, "eventTime"),
                    Mathf.Max(.01f, (float)OpeningJson.Number(entry, "eventSpeed", 1)));
            }
        }

        /// <summary>
        /// The record carries the official action type for each acting unit. Only the
        /// unambiguous ones map onto the converted Spine animation names.
        /// </summary>
        private void PlayAction(Dictionary<string, object> component, int classId, string effectType, int ownerEntityId)
        {
            string actionType = OpeningJson.String(component, "ActionType");
            string alias;
            switch (actionType)
            {
                case "Skill": alias = "skill_01"; break;
                case "AirCombo": alias = "attack_01"; break;
                case "Move": alias = "run_01"; break;
                case "Die": alias = "die_01"; DieActions++; break;
                default: alias = null; break;
            }
            if (!component.TryGetValue("ApplyId", out object applied) || !(applied is Dictionary<string, object> id)) return;
            int unitId = OpeningJson.Int(id, "EntityId");
            if (!units.TryGetValue(unitId, out SkeletonAnimation unit) || unit == null) return;
            if (!bindings.TryGetValue(unitId, out OfficialBattleModelBinding binding)) return;
            string playableClip = null;
            float playableDuration = 0f;
            bool configured = !string.IsNullOrEmpty(effectType)
                && TryResolvePlayableAnimation(effectType, classId, binding, out playableClip, out playableDuration);
            if (!configured && !string.IsNullOrEmpty(effectType))
                LogUnsupportedPlayableAnimation(effectType, classId);
            string clip = configured ? playableClip
                : string.IsNullOrEmpty(alias) ? null : ResolveAlias(binding, alias);
            if (clip == null)
            {
                if (!string.IsNullOrEmpty(effectType) && skillEffects != null)
                    LogUnsupportedPlayableAnimation(effectType, classId);
                return;
            }
            unit.AnimationState.SetAnimation(0, clip, configured && playableDuration < 0f);
            string idle = ResolveAlias(binding, "idle_01");
            if (idle != null && (!configured || playableDuration >= 0f))
                unit.AnimationState.AddAnimation(0, idle, true,
                    configured && playableDuration > 0f ? playableDuration : 0f);
            if (configured && effectType == "status") StatusAnimations++;
            ActionAnimations++;
        }

        private bool TryResolvePlayableAnimation(string effectType, int classId,
            OfficialBattleModelBinding binding, out string clip, out float duration)
        {
            clip = null;
            duration = 0f;
            if (skillEffects == null) return false;
            foreach (Dictionary<string, object> entry in skillEffects.GetEvents(effectType, classId))
            {
                if (!entry.TryGetValue("source_event", out object rawSource)
                    || !(rawSource is Dictionary<string, object> source)) continue;
                if (OpeningJson.String(entry, "kind") == "override")
                {
                    string animation = OpeningJson.String(source, "animation");
                    int slash = animation.LastIndexOf('/');
                    string name = slash >= 0 ? animation.Substring(slash + 1) : animation;
                    clip = ResolveAlias(binding, name) ?? ResolveAlias(binding, animation);
                    if (clip != null) return true;
                }
                if (OpeningJson.String(entry, "kind") != "animator"
                    || !source.TryGetValue("fields", out object rawFields)
                    || !(rawFields is Dictionary<string, object> fields)) continue;
                string trigger = OpeningJson.String(fields, "myTriggerParam");
                if (string.IsNullOrEmpty(trigger)) continue;
                string suffixedTrigger = trigger + "_01";
                clip = ResolveAlias(binding, trigger) ?? ResolveAlias(binding, suffixedTrigger);
                if (clip == null && binding.skeleton != null)
                {
                    var skeletonData = binding.skeleton.GetSkeletonData(true);
                    if (skeletonData != null)
                    {
                        if (skeletonData.FindAnimation(trigger) != null) clip = trigger;
                        else if (skeletonData.FindAnimation(suffixedTrigger) != null) clip = suffixedTrigger;
                    }
                }
                if (clip == null) continue;
                duration = (float)OpeningJson.Number(fields, "myDuration");
                return true;
            }
            return false;
        }

        private void LogUnsupportedPlayableAnimation(string effectType, int classId)
        {
            string key = effectType + ":" + classId;
            if (!skippedModels.Add("playable:" + key)) return;
            foreach (Dictionary<string, object> entry in skillEffects.GetEvents(effectType, classId))
            {
                if (OpeningJson.String(entry, "kind") != "animator"
                    || !entry.TryGetValue("source_event", out object raw)
                    || !(raw is Dictionary<string, object> source)
                    || !source.TryGetValue("fields", out object fieldsValue)
                    || !(fieldsValue is Dictionary<string, object> fields)) continue;
                string trigger = OpeningJson.String(fields, "myTriggerParam");
                if (!string.IsNullOrEmpty(trigger))
                    log("官方开场 Animator 参数没有对应的 Spine 动画: " + key + "/" + trigger);
            }
        }

        private void PlayStatusEntity(OpeningRecordEntity entity, List<object> components)
        {
            if (skillEffects == null) return;
            int targetId = 0;
            foreach (object item in components)
            {
                if (!(item is Dictionary<string, object> component)
                    || !OpeningJson.String(component, "$type").Contains("ActionComponent")) continue;
                if (component.TryGetValue("ApplyId", out object rawId)
                    && rawId is Dictionary<string, object> id)
                    targetId = OpeningJson.Int(id, "EntityId");
                break;
            }
            if (targetId <= 0 || !units.TryGetValue(targetId, out SkeletonAnimation unit) || unit == null) return;
            if (skillEffects.PlayStatus(entity.EntityId, entity.ClassId, unit)) StatusVisualsSpawned++;
        }

        private string ResolveAlias(OfficialBattleModelBinding binding, string alias)
        {
            foreach (OfficialBattleAnimationAlias animation in binding.animations)
                if (animation.name == alias) return animation.spineName;
            return binding.skeleton != null && binding.skeleton.GetSkeletonData(true).FindAnimation(alias) != null
                ? alias : null;
        }

        private static readonly string[] BattleNumberSymbols = { "", "万", "亿", "兆", "京" };

        /// <summary>Official zh_CN battle number formatting (NumberFormatHelper.FormatBattleByFourWidth).</summary>
        public static string FormatBattleNumber(long value)
        {
            double unitBase = 10000.0;
            for (int index = 0; index < BattleNumberSymbols.Length; index++)
            {
                double threshold = Math.Pow(unitBase, index + 1) * 100.0;
                if (Math.Abs((double)value) >= threshold) continue;
                double unit = Math.Pow(unitBase, index);
                double scaled = value / unit;
                if (Math.Floor(scaled) >= unitBase * 100.0)
                    return ((int)(scaled / unitBase)).ToString() + BattleNumberSymbols[index + 1];
                if (scaled < 10.0)
                    return ((float)(int)(100.0 * scaled) / 100f).ToString("0.##") + BattleNumberSymbols[index];
                if (scaled < 100.0)
                    return ((float)(int)(10.0 * scaled) / 10f).ToString("0.#") + BattleNumberSymbols[index];
                return ((int)scaled).ToString() + BattleNumberSymbols[index];
            }
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
            int targetId = 0;
            if (component.TryGetValue("TargetId", out object targetValue)
                && targetValue is Dictionary<string, object> targetRef)
                targetId = OpeningJson.Int(targetRef, "EntityId");
            presentation.ApplyDamage(targetId, value);
            if(value<0 && timelineById.TryGetValue(targetId,out GameObject hitItem) && hitItem!=null)
                hitItem.GetComponent<OfficialBattleHudAnimationPlayer>().Play("/GameRoundCharacterItem","UIGameRound_Hurt");
            int stackIndex = NextDamageStack(targetId);
            if (stackIndex > 0) StackedDamages++;
            Vector3 anchor = battlefield.GridToWorld(OpeningJson.Int(position, "X"), OpeningJson.Int(position, "Y"));
            presentation.ShowDamage(damageLayer, anchor, targetId, value, element, type, crit, block, immunity);
            DamageNumbers++;
        }

        /// <summary>Official DamageUISpacing stacking: a second hit on the same unit shifts down.</summary>
        private int NextDamageStack(int targetId)
        {
            if (targetId <= 0) return 0;
            if (!recentDamage.TryGetValue(targetId, out List<float> times))
            {
                times = new List<float>();
                recentDamage[targetId] = times;
            }
            float now = Time.time;
            times.RemoveAll(value => now - value > 0.6f);
            int index = times.Count;
            times.Add(now);
            return index;
        }

        private void RemoveEntity(int entityId)
        {
            actionPlayer?.StopOwner(entityId);
            skillEffects?.StopEntity(entityId);
            skillEffects?.StopStatus(entityId);
            entityState.Remove(entityId);
            nextActionTime.Remove(entityId);
            presentation?.RemoveUnit(entityId);
            if (timelineById.TryGetValue(entityId, out GameObject timelineItem))
            {
                if (timelineItem != null)
                {
                    timelineItem.GetComponent<OfficialBattleHudAnimationPlayer>().Play("/GameRoundCharacterItem","UIGameRound_Dead");
                    timelineItem.AddComponent<OpeningDelayedDestroy>().Delay=.5f;
                }
                timelineById.Remove(entityId);
                timelineActing.Remove(entityId);
                timelineItems.Remove(timelineItem);
            }
            if (units.TryGetValue(entityId, out SkeletonAnimation defeated) && defeated != null)
            {
                string die = bindings.TryGetValue(entityId, out OfficialBattleModelBinding binding)
                    ? ResolveAlias(binding, "die_01") : null;
                if (die != null) { defeated.AnimationState.SetAnimation(0, die, false); DeathHolds++; }
                if (actorRoots.TryGetValue(entityId, out Transform actor))
                {
                    actor.gameObject.AddComponent<OpeningDelayedDestroy>();
                    actorRoots.Remove(entityId);
                }
                units.Remove(entityId);
                bindings.Remove(entityId);
            }
            if(actingEntityId==entityId)actingEntityId=0;
            RebuildTimeline();
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
            RebuildTimeline();
        }

        private void RebuildTimeline()
        {
            if (timelineContainer == null || timelineUnavailable) return;
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
                if (left == actingEntityId) return -1;
                if (right == actingEntityId) return 1;
                float leftTime = nextActionTime.TryGetValue(left, out float l) ? l : float.MaxValue;
                float rightTime = nextActionTime.TryGetValue(right, out float r) ? r : float.MaxValue;
                int compare = leftTime.CompareTo(rightTime);
                return compare != 0 ? compare : left.CompareTo(right);
            });
            if(actingEntityId==0 && ids.Count>0)actingEntityId=ids[0];
            float width = ((RectTransform)timelineContainer).rect.width;
            if (width <= 1f) width = 800f;
            float totalTime = 0f;
            foreach (float interval in roundIntervals.Values) totalTime=Mathf.Max(totalTime,interval);
            var offsets = new float[ids.Count];
            for (int i=1;i<ids.Count;i++)
            {
                float gap=nextActionTime[ids[i]]-nextActionTime[ids[i-1]];
                offsets[i]=offsets[i-1]+Mathf.Max(gap,totalTime*presentation.TimelineMinDist);
            }
            float extent=ids.Count==0?totalTime:Mathf.Max(totalTime,offsets[ids.Count-1]);
            for (int index = 0; index < ids.Count; index++)
            {
                bool created = !timelineById.TryGetValue(ids[index],out GameObject item) || item==null;
                if (created)
                {
                try { item = renderer.Create("round_character", timelineContainer); }
                catch (Exception error)
                {
                    timelineUnavailable = true;
                    log("官方行动时间轴条目创建失败: " + error.Message);
                    return;
                }
                presentation.BindPortrait(item, entityState[ids[index]]);
                renderer.InitializeAnimations("round_character", item);
                timelineItems.Add(item);
                timelineById[ids[index]] = item;
                }
                item.name = "RoundCharacter_" + ids[index];
                item.SetActive(true);
                var rect = (RectTransform)item.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                float x=Mathf.Lerp(width*.5f,-width*.5f,extent<=0?0:offsets[index]/extent);
                if (created) rect.anchoredPosition=new Vector2(x,0);
                else (item.GetComponent<OpeningTimelineMotion>() ?? item.AddComponent<OpeningTimelineMotion>())
                    .MoveTo(x,width,presentation.TimelineDuration);
                rect.SetSiblingIndex(ids.Count-index-1);
            }
            ApplyActingHighlight();
        }

        private void ApplyActingHighlight()
        {
            foreach (KeyValuePair<int, GameObject> entry in timelineById)
            {
                if (entry.Value == null) continue;
                bool acting = entry.Key == actingEntityId;
                if(timelineActing.TryGetValue(entry.Key,out bool previous) && previous==acting)continue;
                timelineActing[entry.Key]=acting;
                var player=entry.Value.GetComponent<OfficialBattleHudAnimationPlayer>();
                player.Play("/GameRoundCharacterItem", acting ? "UIGameRound_Acting" : "UIGameRound_Idle");
            }
        }

        private void PlayActing(OpeningRecordStep step)
        {
            if (!step.Data.TryGetValue("RoundEntityId", out object raw) || !(raw is Dictionary<string, object> id)) return;
            int entityId = OpeningJson.Int(id, "EntityId");
            actingEntityId = entityId;
            RebuildTimeline();
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
                    animations = npcCatalog.Animations(entity.NpcClassId),
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
            var actor = new GameObject("RecordActor_" + entity.EntityId);
            actor.transform.SetParent(world,false);
            actor.transform.localPosition=new Vector3(entity.GridX,entity.GridY,0);
            actor.transform.localScale=new Vector3(entity.FlipX ? -1 : 1,1,1);
            var gameObject = new GameObject("RecordUnit_" + entity.EntityId);
            gameObject.transform.SetParent(actor.transform, false);
            SkeletonAnimation skeleton = gameObject.AddComponent<SkeletonAnimation>();
            skeleton.skeletonDataAsset = binding.skeleton;
            skeleton.initialSkinName = binding.skin;
            skeleton.Initialize(true);
            skeleton.AnimationState.TimeScale=assets.Record.FightSpeed;
            if (entity.NpcClassId > 0) OpeningNpcModelCatalog.LoadDefault().ApplySkins(entity.NpcClassId, skeleton.Skeleton);
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
            if (!modelTransforms.TryGetValue(entity.ClassId, out var modelTransform))
                throw new InvalidOperationException("Official opening model transform missing: " + entity.ClassId);
            // Keep the full fight prefab chain, including model-specific foot offsets.
            // SkeletonDataAsset's import scale and prefab scale are separate fields.
            gameObject.transform.localPosition = ModelVector(modelTransform["position"]);
            gameObject.transform.localScale = ModelVector(modelTransform["scale"]);
            // Converted PNGs retain the official straight alpha; the generic Spine
            // material importer defaults to premultiplied input. Restore the prefab field.
            if (modelTransform.TryGetValue("straight_alpha", out object alphaInput) && alphaInput != null)
            foreach (AtlasAssetBase atlas in skeleton.SkeletonDataAsset.atlasAssets)
                foreach (Material original in atlas.Materials)
                {
                    if (original == null || skeleton.CustomMaterialOverride.ContainsKey(original)) continue;
                    var material = new Material(original);
                    float straightAlpha = (float)OpeningJson.Number(modelTransform, "straight_alpha");
                    material.SetFloat("_StraightAlphaInput", straightAlpha);
                    if (straightAlpha > 0f) material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                    else material.DisableKeyword("_STRAIGHT_ALPHA_INPUT");
                    skeleton.CustomMaterialOverride.Add(original, material);
                    modelMaterials.Add(material);
                }
            // The official stage renders its ground through sorting layers up to 21;
            // fighters must sort in front of that scenery.
            foreach (MeshRenderer renderer in gameObject.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sortingOrder = 30000 - entity.GridY * 2;
            actorRoots.Add(entity.EntityId,actor.transform);
            presentation.AddUnit(entity,actor.transform);
            skillEffects?.CreateModelPresentation(entity.ClassId,skeleton,actor.transform);
            units.Add(entity.EntityId, skeleton);
            bindings.Add(entity.EntityId, binding);
            Play(entity.EntityId, skeleton, "idle_01", true);
        }

        private static Vector3 ModelVector(object value)
        {
            var fields = OpeningJson.Array(value, "model vector");
            return new Vector3(Convert.ToSingle(fields[0]), Convert.ToSingle(fields[1]), Convert.ToSingle(fields[2]));
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
