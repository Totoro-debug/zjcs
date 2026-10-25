using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleRuntimeController : MonoBehaviour
    {
        private FightBattleExecutor executor;
        private OfficialBattleSceneView sceneView;
        private OfficialBattleHudView hudView;
        private GameObject hudRoot;
        private Func<FightBattleUnitState, Sprite> timelinePortraitResolver;
        private Func<FightBattleUnitState, Sprite> timelineFactionResolver;
        private bool stepping;
        private FightBattleSpeedSettings speedSettings;
        private readonly System.Collections.Generic.List<int> unlockedSpeeds =
            new System.Collections.Generic.List<int>();
        private int speedIndex;
        private double speedMultiplier = 1.0;
        private bool paused;
        private bool ownsTimeScale;
        private readonly FightBattlePhaseMachine phases = new FightBattlePhaseMachine();
        private FightOfficialCombatCatalog combatCatalog;
        private OfficialBattleHudCatalog hudCatalog;
        private OfficialBattleDamageColors damageColors;
        private OfficialBattleNativeAnimationLibrary nativeModelLibrary;
        private OfficialBattleEnvironmentView environmentView;
        private OfflineMainlineProgressSession statueProgress;
        private bool statueEffectsEnded;

        public event Action<FightBattleExecutionResult> StepCompleted;
        public event Action<string> HudControlClicked;
        public event Action<int> SpeedChanged;
        public event Action<FightBattlePhase, FightBattlePhase> PhaseChanged;

        public FightBattleCheckpoint Checkpoint => executor == null ? null : executor.State;
        /// <summary>Presentation length of the last step, from the official skill/hit timing.</summary>
        public float LastStepSeconds { get; private set; } = 0.35f;
        public FightBattlePhase Phase => phases.Phase;
        public bool IsBattleActive => phases.IsBattleActive;
        public int SpeedIndex => speedIndex;
        public double SpeedMultiplier => speedMultiplier;
        public System.Collections.Generic.IReadOnlyList<int> UnlockedSpeedIndices => unlockedSpeeds;
        public bool IsPaused => paused;

        public void SetHudVisible(bool visible)
        {
            if (hudRoot != null) hudRoot.SetActive(visible);
        }
        public bool HasBattleEnvironment => environmentView != null;

        public void Begin(FightBattleCheckpoint checkpoint, Transform worldRoot, Transform hudParent,
            OfficialBattleGridMapper gridMapper, Func<int, Sprite> skillIcon,
            Func<FightBattleUnitState, Sprite> timelinePortrait,
            Func<FightBattleUnitState, string> modelKey,
            Func<FightBattleUnitState, FightBattleStepEvent, string> animationForEvent,
            Action<Transform, FightBattleUnitState> orientUnit,
            OfficialCombatAssetCatalog combatAssets = null,
            OfficialBattleModelCatalog modelCatalog = null,
            OfficialBattleHudCatalog hudCatalog = null,
            bool restoreCheckpoint = false,
            Func<FightBattleUnitState, Sprite> timelineFactionIcon = null,
            int environmentGameId = 0,
            bool? environmentIsDay = null,
            Func<FightBattleUnitState, string> animationGroupResolver = null,
            OfflineMainlineProgressSession statueProgress = null)
        {
            if (executor != null) throw new InvalidOperationException("A battle session is already active.");
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (worldRoot == null) throw new ArgumentNullException(nameof(worldRoot));
            if (hudParent == null) throw new ArgumentNullException(nameof(hudParent));
            if (gridMapper == null) throw new ArgumentNullException(nameof(gridMapper));
            if (skillIcon == null) throw new ArgumentNullException(nameof(skillIcon));
            if (timelinePortrait == null) throw new ArgumentNullException(nameof(timelinePortrait));
            if (modelKey == null) throw new ArgumentNullException(nameof(modelKey));
            if (orientUnit == null) throw new ArgumentNullException(nameof(orientUnit));

            OfficialCombatAssetCatalog resolvedCombatAssets = combatAssets != null
                ? combatAssets : OfficialCombatAssetCatalog.LoadDefault();
            OfficialBattleModelCatalog resolvedModelCatalog = modelCatalog != null
                ? modelCatalog : OfficialBattleModelCatalog.LoadDefault();
            OfficialBattleHudCatalog resolvedHudCatalog = hudCatalog != null
                ? hudCatalog : OfficialBattleHudCatalog.LoadDefault();

            combatCatalog = resolvedCombatAssets.Load();
            this.statueProgress = statueProgress;
            statueEffectsEnded = false;
            if (!restoreCheckpoint && statueProgress != null)
            {
                statueProgress.Reload();
                ApplyPendingStatueEffects(checkpoint, combatCatalog, statueProgress.Save.Player);
            }
            hudCatalog = resolvedHudCatalog;
            damageColors = OfficialBattleDamageColors.Load();
            FightBattleExecutor nextExecutor = restoreCheckpoint
                ? resolvedCombatAssets.RestoreBattle(checkpoint)
                : resolvedCombatAssets.BeginBattle(checkpoint);
            var nextHud = new OfficialBattleHudView(resolvedHudCatalog);
            OfficialBattleNativeAnimationLibrary nextNativeModels = null;
            OfficialBattleEnvironmentView nextEnvironment = null;
            OfficialBattleSceneView nextScene = null;
            GameObject nextHudRoot = null;
            try
            {
                nextNativeModels = OfficialBattleNativeAnimationLibrary.LoadDefault();
                if (environmentGameId > 0)
                    nextEnvironment = OfficialBattleEnvironmentView.Load(worldRoot, environmentGameId,
                        isDay: environmentIsDay);
                nextScene = new OfficialBattleSceneView(resolvedModelCatalog, worldRoot,
                    modelKey, gridMapper, animationForEvent, orientUnit, resolvedHudCatalog, hudParent,
                    nextNativeModels, animationGroupResolver);
                nextHudRoot = nextHud.Mount(hudParent);
                nextHud.ControlClicked += OnHudControlClicked;
                nextHud.SpeedCycled += OnSpeedCycled;
                nextHud.Refresh(checkpoint);
                nextHud.RefreshSkillSlots(checkpoint, skillIcon);
                nextHud.RefreshTimeline(checkpoint, timelinePortrait, timelineFactionIcon, false);
                nextHud.RefreshBoss(checkpoint, UnitName);
                nextHud.RefreshStageStatus(checkpoint);
                nextScene.Refresh(checkpoint);
                executor = nextExecutor;
                hudView = nextHud;
                sceneView = nextScene;
                nativeModelLibrary = nextNativeModels;
                environmentView = nextEnvironment;
                hudRoot = nextHudRoot;
                skillIconResolver = skillIcon;
                timelinePortraitResolver = timelinePortrait;
                timelineFactionResolver = timelineFactionIcon;
                phases.PhaseChanged += OnPhaseChanged;
                phases.Begin(true);
                phases.StartPlay();
            }
            catch
            {
                nextScene?.Dispose();
                nextEnvironment?.Dispose();
                nextNativeModels?.Dispose();
                if (nextHudRoot != null) Destroy(nextHudRoot);
                throw;
            }
        }

        public FightBattleExecutionResult Advance()
        {
            if (executor == null) throw new InvalidOperationException("Begin a battle session before advancing it.");
            if (stepping) throw new InvalidOperationException("A battle step is already being applied.");
            if (executor.State.Finished) throw new InvalidOperationException("The battle is already finished.");

            stepping = true;
            try
            {
                FightBattleExecutionResult result = executor.Step(executor.State.Sequence,
                    executor.State.Counter + 1);
                sceneView.Refresh(result.Checkpoint, result.Event);
                hudView.Refresh(result.Checkpoint);
                hudView.RefreshSkillSlots(result.Checkpoint, skillIconResolver, result.Event);
                hudView.RefreshTimeline(result.Checkpoint, timelinePortraitResolver,
                    timelineFactionResolver, true);
                hudView.RefreshBoss(result.Checkpoint, UnitName);
                hudView.RefreshStageStatus(result.Checkpoint);
                if (!result.Replayed)
                    sceneView.ShowDamageFloats(result.Event, result.Checkpoint, hudCatalog.Font, damageColors);
                LastStepSeconds = result.Event != null && result.Event.Duration > 0f
                    ? result.Event.Duration : 0.35f;
                if (result.Checkpoint.Finished) phases.BeginBattleEnd();
                if (result.Checkpoint.Finished) EndBattle();
                StepCompleted?.Invoke(result);
                return result;
            }
            finally
            {
                stepping = false;
            }
        }

        public void EndBattle()
        {
            if (statueEffectsEnded || statueProgress == null) return;
            statueEffectsEnded = true;
            ProgressionResult result = statueProgress.ClearPendingStatueBattleEffects(
                "statue-battle-end:" + Guid.NewGuid().ToString("N"));
            if (result.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException("Statue battle effects could not be cleared: " + result.Message);
        }

        private static void ApplyPendingStatueEffects(FightBattleCheckpoint checkpoint,
            FightOfficialCombatCatalog combat, OfflinePlayerProgress playerProgress)
        {
            List<OfflineStatueBattleEffect> effects = playerProgress?.PendingStatueBattleEffects;
            if (effects == null || effects.Count == 0) return;
            FightBattleUnitState player = checkpoint.Units.FirstOrDefault(value => value.Id == 1);
            if (player == null || player.MaxHp <= 0 || player.Turn == null || player.Statuses == null)
                throw new InvalidDataException("A pending statue effect requires a valid player battle unit.");

            int statusSerial = Math.Max(checkpoint.StatusInstanceSerial,
                player.Statuses.Where(value => value != null).Select(value => value.InstanceId).DefaultIfEmpty(0).Max());
            foreach (OfflineStatueBattleEffect effect in effects)
            {
                if (effect.HpPercentChange != 0f)
                {
                    double hpRatio = 1.0 + effect.HpPercentChange;
                    player.Hp = Math.Max(1L, Math.Min(player.MaxHp,
                        (long)Math.Floor(player.Hp * hpRatio)));
                    player.Turn.Hp = player.Hp;
                }
                foreach (int classId in effect.StatusEffectClassIds)
                {
                    FightBattleStatusState status = combat.CreateStatus(classId, player.Id, player.Id,
                        ++statusSerial);
                    player.Statuses.Add(status);
                }
            }
            checkpoint.StatusInstanceSerial = statusSerial;
        }

        private Func<int, Sprite> skillIconResolver;

        /// <summary>
        /// Installs the official <c>Battle.Speeds</c>/<c>PlayModeSpeeds</c> table and resolves the
        /// speeds whose official conditions hold for this local character. Speed labels are shown by
        /// the HUD; the applied multiplier always comes from the official speed table.
        /// </summary>
        public void ConfigureSpeeds(FightBattleSpeedSettings settings, int modeType,
            Func<int, bool> conditionMet)
        {
            speedSettings = settings != null ? settings : FightBattleSpeedSettings.Default();
            unlockedSpeeds.Clear();
            unlockedSpeeds.AddRange(speedSettings.Resolve(modeType, conditionMet));
            speedIndex = unlockedSpeeds.Count > 0 ? unlockedSpeeds[0] : 0;
            speedMultiplier = speedSettings.Multiplier(speedIndex);
            ownsTimeScale = true;
            ApplyTimeScale();
            hudView?.RefreshSpeed(speedSettings, speedIndex, unlockedSpeeds);
            SpeedChanged?.Invoke(speedIndex);
        }

        public bool SetSpeedIndex(int index)
        {
            if (speedSettings == null || !unlockedSpeeds.Contains(index) || index == speedIndex) return false;
            speedIndex = index;
            speedMultiplier = speedSettings.Multiplier(index);
            ApplyTimeScale();
            hudView?.RefreshSpeed(speedSettings, speedIndex, unlockedSpeeds);
            SpeedChanged?.Invoke(index);
            return true;
        }

        /// <summary>Cycles to the next official speed that is currently selectable.</summary>
        public bool CycleSpeed()
        {
            if (speedSettings == null || unlockedSpeeds.Count <= 1) return false;
            int position = unlockedSpeeds.IndexOf(speedIndex);
            int next = unlockedSpeeds[(Math.Max(position, 0) + 1) % unlockedSpeeds.Count];
            return SetSpeedIndex(next);
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            if (value)
            {
                if (!phases.Pause()) return;
            }
            else if (!phases.Resume()) return;
            paused = value;
            ApplyTimeScale();
        }

        /// <summary>Confirms readiness for modes that show the official prepare phase.</summary>
        public bool Ready() => phases.Ready();

        public bool StartPlay() => phases.StartPlay();

        private void ApplyTimeScale()
        {
            if (!ownsTimeScale) return;
            Time.timeScale = paused ? 0f : (float)speedMultiplier;
        }

        private void Update()
        {
            hudView?.Tick(Time.unscaledDeltaTime);
            sceneView?.Tick(Time.deltaTime);
        }

        public void AttachUnitTransform(int unitId, Transform transform)
        {
            if (sceneView == null)
                throw new InvalidOperationException("Begin a battle session before attaching a unit transform.");
            sceneView.AttachUnitTransform(unitId, transform);
        }

        public void DisposeSession()
        {
            if (hudView != null)
            {
                hudView.ControlClicked -= OnHudControlClicked;
                hudView.SpeedCycled -= OnSpeedCycled;
            }
            phases.PhaseChanged -= OnPhaseChanged;
            phases.Finish();
            sceneView?.Dispose();
            environmentView?.Dispose();
            environmentView = null;
            nativeModelLibrary?.Dispose();
            if (hudRoot != null) Destroy(hudRoot);
            if (ownsTimeScale)
            {
                Time.timeScale = 1f;
                ownsTimeScale = false;
            }
            paused = false;
            speedMultiplier = 1.0;
            speedIndex = 0;
            speedSettings = null;
            unlockedSpeeds.Clear();
            executor = null;
            sceneView = null;
            nativeModelLibrary = null;
            hudView = null;
            hudRoot = null;
            combatCatalog = null;
            hudCatalog = null;
            damageColors = null;
            skillIconResolver = null;
            timelinePortraitResolver = null;
            timelineFactionResolver = null;
        }

        private void OnDestroy()
        {
            DisposeSession();
        }

        private void OnHudControlClicked(string path)
        {
            HudControlClicked?.Invoke(path);
        }

        private void OnSpeedCycled()
        {
            CycleSpeed();
        }

        private string UnitName(FightBattleUnitState unit)
        {
            return unit == null || unit.MonsterId <= 0 || combatCatalog == null
                ? null : combatCatalog.MonsterName(unit.MonsterId);
        }

        private void OnPhaseChanged(FightBattlePhase previous, FightBattlePhase next)
        {
            if (next == FightBattlePhase.End && ownsTimeScale)
            {
                Time.timeScale = 1f;
                ownsTimeScale = false;
            }
            PhaseChanged?.Invoke(previous, next);
        }
    }
}
