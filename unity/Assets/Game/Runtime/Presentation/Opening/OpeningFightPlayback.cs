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
        private const float PlaybackSpeed = 1.5f;

        private readonly OpeningAssets assets;
        private readonly OpeningUiView ui;
        private readonly Action<string> log;
        private readonly Func<bool> showSkipButton;
        private readonly OfficialBattleModelCatalog models;
        private readonly Transform world;
        private readonly Dictionary<int, SkeletonAnimation> units = new Dictionary<int, SkeletonAnimation>();
        private readonly Dictionary<int, OfficialBattleModelBinding> bindings =
            new Dictionary<int, OfficialBattleModelBinding>();
        private readonly Dictionary<int, OpeningRecordEntity> entityState = new Dictionary<int, OpeningRecordEntity>();
        private GameObject hud;
        private GameObject skipButton;
        private bool skipRequested;
        private float scaleFactor;
        private readonly HashSet<string> skippedModels = new HashSet<string>(StringComparer.Ordinal);

        public OpeningFightPlayback(OpeningAssets assets, OpeningUiView ui, OfficialBattleModelCatalog models,
            Transform world, Func<bool> showSkipButton, Action<string> log)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.models = models;
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.showSkipButton = showSkipButton ?? (() => false);
            this.log = log ?? (_ => { });
        }

        public bool SkipRequested => skipRequested;
        public bool Finished { get; private set; }
        public int Round { get; private set; }
        public int SpawnedUnits => units.Count;
        public int PreparedUnits { get; private set; }

        public void RequestFastForward()
        {
            skipRequested = true;
        }

        public IEnumerator Play()
        {
            OpeningRecord record = assets.Record;
            MountHud();
            foreach (OpeningRecordEntity entity in record.Entities)
            {
                entityState[entity.EntityId] = entity;
                TryCreateUnit(entity);
            }
            yield return null;

            if (showSkipButton()) yield return ShowSkipButtonAfterDelay();

            for (int roundIndex = 0; roundIndex < record.Rounds.Count; roundIndex++)
            {
                if (skipRequested) break;
                OpeningRecordRound round = record.Rounds[roundIndex];
                Round = roundIndex;
                float roundStart = Time.unscaledTime;
                foreach (OpeningRecordStep step in round.Steps)
                {
                    if (skipRequested) break;
                    float wait = step.Time / PlaybackSpeed - (Time.unscaledTime - roundStart);
                    if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
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

        private IEnumerator ShowSkipButtonAfterDelay()
        {
            yield return new WaitForSecondsRealtime(SkipButtonDelaySeconds);
            if (skipButton == null) yield break;
            skipButton.SetActive(true);
            Button button = skipButton.GetComponent<Button>() ?? skipButton.GetComponentInChildren<Button>(true);
            if (button == null) yield break;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(RequestFastForward);
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
            skipButton = FindNode(hud, "SkipBtn");
            if (skipButton != null) skipButton.SetActive(false);
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
        }

        private void RemoveEntity(int entityId)
        {
            entityState.Remove(entityId);
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
                case "FightPersonalRoundNewModification":
                case "FightRoundNewModification":
                case "FightRoundUpdateDataModification":
                    PlayActing(step);
                    break;
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

        private void PlayActing(OpeningRecordStep step)
        {
            if (!step.Data.TryGetValue("EntityId", out object raw) || !(raw is Dictionary<string, object> id)) return;
            int entityId = OpeningJson.Int(id, "EntityId");
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

        private void TryCreateUnit(OpeningRecordEntity entity)
        {
            if (entity == null || units.ContainsKey(entity.EntityId)) return;
            if (entity.CharacterType == "Npc")
            {
                // Official renders these two characters from the player appearance pipeline;
                // the offline module has no appearance fighter binding yet.
                log("录像角色单位缺少外观绑定，暂不生成模型: " + entity.EntityId + " (" + entity.Profession + ")");
                return;
            }
            if (entity.MonsterId <= 0 || models == null) return;
            if (!assets.Stage.Models.TryGetValue(entity.MonsterId, out string key) || string.IsNullOrEmpty(key))
            {
                log("官方怪物模型标识缺失: " + entity.MonsterId);
                return;
            }
            PreparedUnits++;
            OfficialBattleModelBinding binding = models.Find(key);
            if (binding == null)
            {
                log("官方战斗模型绑定缺失: " + key);
                return;
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
                // The pinned battle Spine payload ships this model without its atlas
                // texture page; rendering it would show an untextured white mesh.
                if (skippedModels.Add(key)) log("官方怪物模型缺少贴图页，暂不显示: " + key);
                UnityEngine.Object.Destroy(gameObject);
                return;
            }
            gameObject.transform.localPosition = new Vector3(entity.GridX, entity.GridY, 0);
            // Official fight stages place these models at their prefab scale inside a
            // world whose root matrix is offset by thousands of units. The offline arena
            // is grid-metered, so the first model establishes one shared factor that keeps
            // the official relative sizes between monsters.
            if (scaleFactor <= 0f)
            {
                float height = skeleton.Skeleton.Data.Height * binding.scale;
                scaleFactor = height > 0.0001f ? 1.2f / height : 100f;
            }
            gameObject.transform.localScale = Vector3.one * binding.scale * scaleFactor;
            Play(entity.EntityId, skeleton, "idle_01", true);
            units.Add(entity.EntityId, skeleton);
            bindings.Add(entity.EntityId, binding);
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
        }
    }
}
