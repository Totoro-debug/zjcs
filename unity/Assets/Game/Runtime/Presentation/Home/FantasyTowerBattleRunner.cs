using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Presentation.Character;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    public sealed class FantasyTowerBattleRunner : MonoBehaviour, IOfficialBattleModeAdapter
    {
        private HomeFeatureRuntime host;
        private OfflineFantasyTowerSession progress;
        private OfficialFantasyTowerCatalog catalog;
        private OfficialBattleRuntimeController controller;
        private FantasyTowerAudioPlayer towerAudio;
        private Transform battleRoot;
        private int entryIndex, layer;
        private float continueTimer;
        private bool autoPush, resultHandled, disposed, backgroundExecution;

        public event Action<int, int, bool> LayerCompleted;
        public event Action<bool> PushStateChanged;
        public bool IsAutoPushEnabled => autoPush;

        public void Begin(HomeFeatureRuntime runtime, int towerEntryIndex)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            entryIndex = towerEntryIndex;
            catalog = host.FantasyTowerCatalog;
            progress = host.FantasyTower;
            towerAudio = host.GetComponent<FantasyTowerAudioPlayer>();
            if (towerAudio == null) towerAudio = host.gameObject.AddComponent<FantasyTowerAudioPlayer>();
            towerAudio.Initialize();
            FantasyTowerEntrySave save = progress.GetEntry(entryIndex);
            autoPush = save.autoPushEnabled;
            layer = catalog.CurrentLayer(progress, entryIndex);
            if (layer <= 0) throw new InvalidOperationException("This official Fantasy Tower entry is complete.");
            StartCurrentLayer();
        }

        public void BeginAutoNext(HomeFeatureRuntime runtime, int towerEntryIndex)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            entryIndex = towerEntryIndex;
            catalog = host.FantasyTowerCatalog;
            progress = host.FantasyTower;
            FantasyTowerResult enabled = progress.SetAutoPush(entryIndex, true);
            if (enabled != FantasyTowerResult.Applied && enabled != FantasyTowerResult.Unchanged)
                throw new InvalidOperationException("Fantasy Tower auto push is not unlocked for this entry.");
            autoPush = true;
            layer = catalog.CurrentLayer(progress, entryIndex) - 1;
            if (layer < 1) throw new InvalidOperationException("This official Fantasy Tower entry is complete.");
            continueTimer = 5f;
        }

        public FantasyTowerResult SetAutoPush(bool enabled)
        {
            FantasyTowerResult result = progress.SetAutoPush(entryIndex, enabled);
            if (result == FantasyTowerResult.Applied || result == FantasyTowerResult.Unchanged)
            {
                autoPush = enabled;
                if (!enabled)
                {
                    continueTimer = 0f;
                    if (backgroundExecution) SetBackgroundExecution(false);
                    if (controller == null) Destroy(this);
                }
                PushStateChanged?.Invoke(enabled);
            }
            return result;
        }

        public bool SetBackgroundExecution(bool enabled)
        {
            if (enabled == backgroundExecution) return true;
            if (enabled && (!autoPush || (controller == null && continueTimer <= 0f)
                || (controller != null && controller.Checkpoint == null))) return false;
            if (enabled)
            {
                FantasyTowerResult started = progress.BeginBackgroundPush(entryIndex,
                    DateTimeOffset.Now.ToUnixTimeSeconds());
                if (started != FantasyTowerResult.Applied && started != FantasyTowerResult.Unchanged)
                    return false;
                if (controller != null)
                {
                    if (progress.SaveBackgroundCheckpoint(entryIndex, controller.Checkpoint) != FantasyTowerResult.Applied)
                        return false;
                    progress.ClearForegroundCheckpoint(entryIndex);
                }
                backgroundExecution = true;
                if (battleRoot != null) battleRoot.gameObject.SetActive(false);
                controller?.SetHudVisible(false);
                return true;
            }
            FantasyTowerResult batch = progress.FinishBackgroundPushBatch(entryIndex,
                DateTimeOffset.Now.ToUnixTimeSeconds());
            if (batch != FantasyTowerResult.Applied && batch != FantasyTowerResult.Unchanged)
                return false;
            backgroundExecution = false;
            if (battleRoot != null) battleRoot.gameObject.SetActive(true);
            controller?.SetHudVisible(true);
            if (controller?.Checkpoint != null)
            {
                progress.ClearBackgroundCheckpoint(entryIndex);
                progress.SaveForegroundCheckpoint(entryIndex, controller.Checkpoint);
            }
            return true;
        }

        private void StartCurrentLayer()
        {
            if (!backgroundExecution && entryIndex != 2)
                towerAudio.Play("Note_FantasyLadder_Flash");
            FantasyTowerLayerInfo info = catalog.GetLayer(entryIndex, layer);
            if (!catalog.TryGetBattle(info.Id, out Map10RuntimeStage stage,
                    out Map10RuntimeRoster roster, out Map10RuntimeBattleScene scene))
                throw new InvalidOperationException("Official Fantasy Tower battle data is missing for layer " + layer);
            FantasyTowerEntrySave save = progress.GetEntry(entryIndex);
            FightBattleCheckpoint checkpoint = progress.LoadForegroundCheckpoint(entryIndex, stage.Id);
            bool restore = checkpoint != null;
            if (!restore) checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest
            {
                Stage = stage, Roster = roster, Scene = scene,
                Player = host.Profile.Save.Player, World = host.Profile.Save.World,
                Progression = host.DailyDungeonProgression,
                RandomSeed = StableSeed(host.ActiveProfileSlotId, stage.Id),
                UseMonsterProperties = true
            });
            MountBattle(checkpoint, scene, stage, restore);
            if (backgroundExecution) progress.SaveBackgroundCheckpoint(entryIndex, checkpoint);
            else progress.SaveForegroundCheckpoint(entryIndex, checkpoint);
            if (backgroundExecution)
            {
                battleRoot.gameObject.SetActive(false);
                controller.SetHudVisible(false);
            }
            resultHandled = false;
            continueTimer = 0f;
        }

        private void MountBattle(FightBattleCheckpoint checkpoint, Map10RuntimeBattleScene scene,
            Map10RuntimeStage stage, bool restore)
        {
            if (controller != null) throw new InvalidOperationException("Fantasy Tower battle is already mounted.");
            disposed = false;
            Transform playerTransform = host.HomePlayerTransform;
            if (playerTransform == null) throw new InvalidOperationException("Official player model is unavailable.");
            battleRoot = new GameObject("FantasyTowerBattleWorld").transform;
            FightBattleUnitState player = checkpoint.Units.First(value => value.Id == 1);
            battleRoot.position = playerTransform.position - new Vector3(
                scene.FightGridGroundComponent.OriginPos.X + player.X,
                scene.FightGridGroundComponent.OriginPos.Y + player.Y,
                scene.FightGridGroundComponent.OriginPos.Z);

            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = combatAssets.Load();
            controller = gameObject.AddComponent<OfficialBattleRuntimeController>();
            controller.Begin(new OfficialBattleRequest
            {
                Checkpoint = checkpoint,
                WorldRoot = battleRoot,
                PlayerAppearance = new OfficialBattlePlayerAppearanceSource(1, 1,
                    () => host.Profile.Save.Player, id => host.DailyDungeonProgression.Catalog.ItemType(id)),
                EnvironmentGameId = scene.GameId,
                HudParent = host.Hud.CanvasTransform,
                TimelinePortrait = OfficialBattleCharacterPortraits.ForProfile(battleRoot.gameObject,
                    host.DailyDungeonProgression, host.ActiveProfileSlotId, host.SavesRoot),
                HostPresentation = new OfficialBattleHostPresentation(host.Scene.Camera,
                    host.Scene.Root.transform, host.Hud.CanvasTransform, host.SetBattlePresentationActive),
                ModeAdapter = this,
                SpeedConditionMet = IsBattleSpeedConditionMet,
                AnimationGroupResolver = unit => OfficialCharacterAnimationProfile.GroupForProfession(unit.Profession),
                CombatAssets = combatAssets,
                RestoreCheckpoint = restore,
                StatueProgress = host.Profile,
                Options = OfficialMapBattleSessionOptionsBuilder.Create(stage, true, 3, 3, player.X, player.Y)
            });
        }

        private void Update()
        {
            if (resultHandled) return;
            if (continueTimer > 0f)
            {
                continueTimer -= Time.deltaTime;
                if (continueTimer <= 0f && autoPush)
                {
                    host.Hud.CloseDialog();
                    layer++;
                    StartCurrentLayer();
                }
                else if (continueTimer <= 0f) Destroy(this);
                return;
            }
            if (controller == null) return;
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            if (checkpoint == null) return;
            controller.Tick(Time.deltaTime);
        }

        public void OnBattleCompleted(FightBattleCheckpoint checkpoint)
        {
            Complete(checkpoint != null && checkpoint.Outcome != null && checkpoint.Outcome.Won);
        }

        public void OnBattleStepCompleted(FightBattleExecutionResult result)
        {
            FightBattleCheckpoint checkpoint = result?.Checkpoint;
            if (checkpoint == null) return;
            if (backgroundExecution) progress.SaveBackgroundCheckpoint(entryIndex, checkpoint);
            else progress.SaveForegroundCheckpoint(entryIndex, checkpoint);
        }

        public FightBattleReport FinalReport { get; private set; }
        public void OnBattleReportReady(FightBattleReport report) { FinalReport = report; }

        public void OnBattleHudControlClicked(string path) { }

        private void Complete(bool won)
        {
            if (resultHandled) return;
            resultHandled = true;
            FantasyTowerLayerInfo info = catalog.GetLayer(entryIndex, layer);
            if (!backgroundExecution && entryIndex == 2)
                towerAudio.Play("Note_FantasyLadder_Elevator");
            List<FantasyTowerReward> rewards = won
                ? host.RollFantasyTowerRewards(info.Id, "fantasy-tower:stage:" + entryIndex + ":" + info.Id)
                : new List<FantasyTowerReward>();
            if (backgroundExecution)
            {
                FantasyTowerResult backgroundSaved = progress.CompleteBackgroundLayer(entryIndex, layer,
                    info.Id, won, rewards, DateTimeOffset.Now.ToUnixTimeSeconds());
                if (backgroundSaved != FantasyTowerResult.Applied)
                    throw new InvalidOperationException("Fantasy Tower background result could not be committed: " + backgroundSaved);
                bool hasNext = won && catalog.CurrentLayer(progress, entryIndex) > 0;
                DisposeBattle();
                autoPush = hasNext;
                if (hasNext)
                {
                    backgroundExecution = true;
                    continueTimer = 5f;
                    resultHandled = false;
                }
                else
                {
                    backgroundExecution = false;
                    host.Hud.ShowFantasyTowerPendingResult();
                    Destroy(this);
                }
                return;
            }
            FantasyTowerResult saved = progress.CompleteForegroundBattle(entryIndex, layer,
                info.Id, won, rewards, host.Profile);
            if (saved != FantasyTowerResult.Applied)
                throw new InvalidOperationException("Fantasy Tower layer result could not be committed: " + saved);
            if (won) host.RefreshAfterFantasyTowerReward();
            LayerCompleted?.Invoke(entryIndex, layer, won);
            DisposeBattle();
            bool hasNextLayer = won && catalog.CurrentLayer(progress, entryIndex) > 0;
            host.Hud.ShowFantasyTowerBattleResult(entryIndex, layer, won,
                autoPush && hasNextLayer, rewards);
            if (autoPush && hasNextLayer)
            {
                continueTimer = 5f;
                resultHandled = false;
            }
            else if (won && !hasNextLayer)
                progress.SetAutoPush(entryIndex, false);
            if (continueTimer <= 0f) Destroy(this);
        }

        private void OnDestroy() => DisposeBattle();

        private bool IsBattleSpeedConditionMet(int conditionId)
        {
            var context = new OfficialConditionContext { Level = host.Level };
            foreach (int mapClassId in host.Profile.Save.Player.UnlockedMapClassIds ?? new List<int>())
                context.UnlockedMapClassIds.Add(mapClassId);
            return host.DailyDungeonProgression.Catalog.EvaluateCondition(conditionId, context);
        }

        private void DisposeBattle()
        {
            if (disposed) return;
            disposed = true;
            if (controller != null)
            {
                controller.DisposeSession();
                Destroy(controller); controller = null;
            }
            if (battleRoot != null) { Destroy(battleRoot.gameObject); battleRoot = null; }
        }

        private static int StableSeed(string slot, int stage)
        {
            unchecked { int hash = 17; foreach (char value in slot) hash = hash * 31 + value; return hash * 31 + stage; }
        }
    }
}
