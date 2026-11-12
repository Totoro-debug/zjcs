using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleRuntimeController : MonoBehaviour
    {
        private FightBattleExecutor executor;
        private IDisposable renderPipelineScope;
        private OfficialBattleHostPresentation.Scope hostPresentation;
        private OfficialBattleCameraView cameraView;
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
        private FightBattlePhaseMachine phases = new FightBattlePhaseMachine();
        private OfficialBattleSessionOptions sessionOptions;
        private Transform pendingHudParent;
        private float remainingHudDelay;
        private bool hudVisible = true;
        private FightOfficialCombatCatalog combatCatalog;
        private OfficialBattleHudCatalog hudCatalog;
        private OfficialBattleDamageColors damageColors;
        private OfficialBattleNativeAnimationLibrary nativeModelLibrary;
        private OfficialBattleSkillGraphPlayback skillGraphPlayback;
        private OfficialBattleEnvironmentView environmentView;
        private OfflineMainlineProgressSession statueProgress;
        private bool statueEffectsEnded;
        private FightProcessPlayback<float> stepPlayback;
        private OfficialBattlePreparationView preparationView;
        private bool battleCompletedRaised;
        private FightBattleReport finalReport;
        private IOfficialBattleModeAdapter modeAdapter;
        private readonly OfficialBattleLeaveDialog leaveDialog = new OfficialBattleLeaveDialog();

        // The game uses mouse input; acceptance can feed the same pointer path without OS input.
        public Func<OfficialBattlePointerFrame> PreparationPointerProvider { get; set; }

        public event Action<FightBattleExecutionResult> StepCompleted;
        public event Action<string> HudControlClicked;
        public event Action<int> SpeedChanged;
        public event Action<FightBattlePhase, FightBattlePhase> PhaseChanged;
        public event Action<FightBattleCheckpoint> BattleCompleted;
        public event Action<FightBattleReport> BattleReportReady;
        public event Action StepPresentationCompleted;
        public event Action PreparationPositionChanged;

        public FightBattleCheckpoint Checkpoint => executor == null ? null : executor.State;
        public FightBattleReport FinalReport => finalReport;
        public Transform TryGetUnitTransform(int unitId) => sceneView?.TryGetUnitTransform(unitId)
            ?? skillGraphPlayback?.TryGetGridTransform(unitId);
        /// <summary>Presentation length of the last step, from the official skill/hit timing.</summary>
        public float LastStepSeconds { get; private set; } = 0.35f;
        public FightBattlePhase Phase => phases.Phase;
        public bool IsBattleActive => phases.IsBattleActive;
        public int SpeedIndex => speedIndex;
        public double SpeedMultiplier => speedMultiplier;
        public System.Collections.Generic.IReadOnlyList<int> UnlockedSpeedIndices => unlockedSpeeds;
        public bool IsPaused => paused;
        public bool IsPresentingStep => stepPlayback != null && !stepPlayback.Complete;
        public bool CanCompleteBattle => executor != null && executor.State.Finished && !IsPresentingStep
            && (executor.State.StatusSeconds?.PendingHits.Count ?? 0) == 0;
        public bool CanAdvance => executor != null && !executor.State.Finished
            && !paused && !IsPresentingStep
            && (executor.State.StatusSeconds?.PendingHits.Count ?? 0) == 0
            && (Phase == FightBattlePhase.Play || Phase == FightBattlePhase.PlayStart);

        public void SetHudVisible(bool visible)
        {
            bool returning = visible && !hudVisible;
            hudVisible = visible;
            if (!visible) leaveDialog.Dispose();
            if (!visible) cameraView?.SetVisible(false);
            hostPresentation?.SetVisible(visible);
            if (visible) cameraView?.SetVisible(true);
            if (hudRoot != null) hudRoot.SetActive(visible);
            skillGraphPlayback?.SetDamageUIVisible(visible);
            if (returning && Checkpoint != null)
            {
                sceneView?.Refresh(Checkpoint, null);
                skillGraphPlayback?.RefreshGridSnapshot(Checkpoint);
                hudView?.Refresh(Checkpoint);
                hudView?.RefreshSkillSlots(Checkpoint, skillIconResolver);
                hudView?.RefreshPet(Checkpoint, statueProgress?.Save?.Player);
                hudView?.RefreshTimeline(Checkpoint, timelinePortraitResolver, timelineFactionResolver, false, null);
                hudView?.RefreshBoss(Checkpoint, UnitName);
                hudView?.RefreshStageStatus(Checkpoint);
            }
        }
        public bool HasBattleEnvironment => environmentView != null;
        public bool OwnsPresentationCamera => cameraView != null;

        public bool TryGetSceneCamera(out Vector3 position, out float orthographicSize)
        {
            position = default;
            orthographicSize = 0f;
            if (environmentView == null || environmentView.CameraOrthographicSize <= 0f) return false;
            position = environmentView.Root.TransformPoint(environmentView.CameraPosition);
            orthographicSize = environmentView.CameraOrthographicSize;
            return true;
        }


        public void Begin(OfficialBattleRequest request)
        {
            if (executor != null) throw new InvalidOperationException("A battle session is already active.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Checkpoint == null) throw new ArgumentNullException(nameof(request.Checkpoint));
            if (request.WorldRoot == null) throw new ArgumentNullException(nameof(request.WorldRoot));
            battleCompletedRaised = false;
            finalReport = null;
            if (float.IsNaN(request.InitialStepDelay) || float.IsInfinity(request.InitialStepDelay) || request.InitialStepDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(request.InitialStepDelay));
            var checkpoint = request.Checkpoint;
            var worldRoot = request.WorldRoot;
            var hudParent = request.HudParent;
            var gridMapper = new OfficialBattleGridMapper(request.WorldRoot, request.Checkpoint.Grid);
            var skillIcon = request.SkillIcon;
            var timelinePortrait = request.TimelinePortrait;
            var modelKey = request.ModelKey;
            var animationForEvent = request.AnimationForEvent;
            Action<Transform, FightBattleUnitState> orientUnit = OrientUnit;
            var combatAssets = request.CombatAssets;
            var modelCatalog = request.ModelCatalog;
            var hudCatalog = request.HudCatalog;
            var restoreCheckpoint = request.RestoreCheckpoint;
            var timelineFactionIcon = request.TimelineFactionIcon;
            var environmentGameId = request.EnvironmentGameId;
            var environmentIsDay = request.EnvironmentIsDay;
            var animationGroupResolver = request.AnimationGroupResolver;
            var statueProgress = request.StatueProgress;
            var options = request.Options;
            var nativeNpcFactory = request.NativeNpcFactory;
            var requestedModeAdapter = request.ModeAdapter;
            remainingStepDelay = request.InitialStepDelay;
            if (hudParent == null) throw new ArgumentNullException(nameof(hudParent));
            sessionOptions = options ?? new OfficialBattleSessionOptions();
            if (double.IsNaN(sessionOptions.PrepareSeconds) || double.IsInfinity(sessionOptions.PrepareSeconds)
                || sessionOptions.PrepareSeconds < 0 || float.IsNaN(sessionOptions.ShowMainPanelDelay)
                || float.IsInfinity(sessionOptions.ShowMainPanelDelay) || sessionOptions.ShowMainPanelDelay < 0)
                throw new ArgumentException("Battle preparation and HUD delays must be finite and nonnegative.", nameof(options));
            modeAdapter = requestedModeAdapter;

            OfficialCombatAssetCatalog resolvedCombatAssets = combatAssets != null
                ? combatAssets : OfficialCombatAssetCatalog.LoadDefault();
            OfficialBattleModelCatalog resolvedModelCatalog = modelCatalog != null
                ? modelCatalog : OfficialBattleModelCatalog.LoadDefault();
            OfficialBattleHudCatalog resolvedHudCatalog = hudCatalog != null
                ? hudCatalog : OfficialBattleHudCatalog.LoadDefault();

            combatCatalog = resolvedCombatAssets.Load();
            modelKey = modelKey ?? (unit => unit.Id == 1 ? null : resolvedModelCatalog.ResolveModelKey(unit));
            if (skillIcon == null)
            {
                Sprite fallback = resolvedHudCatalog.FindSprite("C_none-fa3e084e40.png");
                if (fallback == null) throw new InvalidOperationException("Official battle fallback portrait is missing.");
                var icons = OfficialCombatSkillIconCatalog.TryLoadDefault();
                skillIcon = skillIcon ?? (itemId => icons?.Find(itemId) ?? fallback);
            }
            var portraitOverride = timelinePortrait;
            var portraits = OfficialBattleMonsterPortraitCatalog.TryLoadDefault();
            var petPortraits = Resources.Load<OfficialBattleMonsterPortraitCatalog>("Combat/OfficialBattlePetPortraitCatalog");
            Sprite portraitFallback = resolvedHudCatalog.FindSprite("C_none-fa3e084e40.png");
            if (portraitFallback == null) throw new InvalidOperationException("Official battle fallback portrait is missing.");
            // GameRoundCharacterItem.RefreshIcon checks FightRolePetEntityComponent
            // before the monster branch and uses GetPetIcon(PetEntityClassId).
            timelinePortrait = unit => portraitOverride?.Invoke(unit) ?? (unit.CharacterType == 7
                ? petPortraits?.Find(unit.MonsterId) : unit.MonsterId > 0
                && unit.CharacterType != 1 ? portraits?.Find(combatCatalog.MonsterIconId(unit.MonsterId)) : null)
                ?? portraitFallback;
            this.statueProgress = statueProgress;
            statueEffectsEnded = false;
            if (!restoreCheckpoint && statueProgress != null)
            {
                statueProgress.Reload();
                ApplyPendingStatueEffects(checkpoint, combatCatalog, statueProgress.Save.Player);
            }
            this.hudCatalog = resolvedHudCatalog;
            damageColors = OfficialBattleDamageColors.Load();
            FightBattleExecutor nextExecutor = restoreCheckpoint
                ? resolvedCombatAssets.RestoreBattle(checkpoint)
                : resolvedCombatAssets.BeginBattle(checkpoint);
            var nextHud = new OfficialBattleHudView(resolvedHudCatalog);
            OfficialBattleNativeAnimationLibrary nextNativeModels = null;
            OfficialBattleSkillGraphPlayback nextSkillGraphPlayback = null;
            OfficialBattleEnvironmentView nextEnvironment = null;
            OfficialBattleSceneView nextScene = null;
            GameObject nextHudRoot = null;
            try
            {
                hostPresentation = request.HostPresentation?.Capture(worldRoot);
                var pipelineAssets=Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
                if(pipelineAssets!=null)renderPipelineScope=pipelineAssets.Enter();
                nextNativeModels = OfficialBattleNativeAnimationLibrary.LoadDefault();
                OfficialBattleSkillGraphCatalog graphCatalog =
                    OfficialBattleSkillGraphCatalog.TryLoadDefault();
                if (graphCatalog != null && graphCatalog.Count > 0)
                {
                    var nativeScope = new OfflineBattleNativePlayableScope(worldRoot,
                        request.SkillPlayableSoundHost);
                    try
                    {
                        var entityViews = new OfficialBattleSkillEntityPlayback(worldRoot, gridMapper, nativeScope,
                            id => nextExecutor.State.Units.Find(unit => unit.Id == id)
                                ?? nextExecutor.State.GridItems.Find(item => item != null && item.HasRuntime && item.Id == id)?.Runtime);
                        entityViews.SkillStarted += nextHud.OnSkillStarted;
                        entityViews.SkillEnded += nextHud.OnSkillEnded;
                        entityViews.CastStarted += nextHud.OnPetSkillStarted;
                        nextSkillGraphPlayback = new OfficialBattleSkillGraphPlayback(graphCatalog, nativeScope, entityViews);
                        nextSkillGraphPlayback.BindDamageUI(worldRoot, checkpoint.StageType);
                    }
                    catch { nativeScope.Dispose(); throw; }
                }
                if (environmentGameId > 0)
                    nextEnvironment = OfficialBattleEnvironmentView.Load(worldRoot, environmentGameId,
                        isDay: environmentIsDay);
                nextScene = new OfficialBattleSceneView(resolvedModelCatalog, worldRoot,
                    modelKey, gridMapper, animationForEvent, orientUnit, resolvedHudCatalog, hudParent,
                    nextNativeModels, animationGroupResolver, nativeNpcFactory,
                    nextSkillGraphPlayback?.Scope, restoreCheckpoint, request.PlayerAppearance);
                nextHud.ControlClicked += OnHudControlClicked;
                nextHud.SpeedCycled += OnSpeedCycled;
                if (sessionOptions.ShowMainPanelDelay <= 0)
                {
                    nextHudRoot = nextHud.Mount(hudParent, hostPresentation?.Camera);
                    RefreshPetFunction(nextHud, nextExecutor.State);
                    nextHud.Refresh(checkpoint);
                    nextHud.RefreshSkillSlots(checkpoint, skillIcon);
                    nextHud.RefreshTimeline(checkpoint, timelinePortrait, timelineFactionIcon, false);
                    nextHud.RefreshBoss(checkpoint, UnitName);
                    nextHud.RefreshStageStatus(checkpoint);
                }
                nextScene.BindMovementActions(nextSkillGraphPlayback);
                nextScene.Refresh(checkpoint);
                nextSkillGraphPlayback?.BindPlayerClonePrefabs(nextScene.BindPlayerClonePrefab);
                executor = nextExecutor;
                hudView = nextHud;
                sceneView = nextScene;
                nextSkillGraphPlayback?.BindSourceDeathView(nextScene);
                nextSkillGraphPlayback?.BindGridEntities(worldRoot, gridMapper, checkpoint,
                    nextScene.Body, id => resolvedHudCatalog.TryGetStatusSprites(id, out var icon, out _, out _) && icon != null);
                nextSkillGraphPlayback?.BindAirCombo(nextExecutor, worldRoot, nextScene.Body);
                nativeModelLibrary = nextNativeModels;
                skillGraphPlayback = nextSkillGraphPlayback;
                environmentView = nextEnvironment;
                if (hostPresentation != null)
                {
                    if (TryGetSceneCamera(out Vector3 cameraPosition, out float cameraSize))
                        hostPresentation.SetCamera(cameraPosition, cameraSize);
                    hostPresentation.SetVisible(hudVisible);
                }
                hudRoot = nextHudRoot;
                skillIconResolver = skillIcon;
                timelinePortraitResolver = timelinePortrait;
                timelineFactionResolver = timelineFactionIcon;
                pendingHudParent = hudParent;
                remainingHudDelay = sessionOptions.ShowMainPanelDelay;
                phases = new FightBattlePhaseMachine(restoreCheckpoint && checkpoint.PreparationPending
                    ? checkpoint.PreparationSecondsRemaining : sessionOptions.PrepareSeconds);
                phases.PhaseChanged += OnPhaseChanged;
                phases.AutoStartRequested += OnAutoStartRequested;
                phases.CountdownChanged += OnPrepareCountdownChanged;
                if (sessionOptions.CanChangePosition)
                {
                    preparationView = new OfficialBattlePreparationView(checkpoint, worldRoot,
                        sceneView, gridMapper, sessionOptions);
                    preparationView.PositionChanged += OnPreparationPositionChanged;
                }
                bool restorePreparation = restoreCheckpoint && checkpoint.PreparationPending;
                phases.Begin((restoreCheckpoint && !restorePreparation) || sessionOptions.SkipPrepareStatus);
                checkpoint.PreparationPending = phases.IsPreparing;
                preparationView?.SetPreparing(phases.IsPreparing);
                if (phases.Phase == FightBattlePhase.PlayStart) phases.StartPlay();
                hudView.RefreshPhase(phases.Phase, sessionOptions);
                hudView.RefreshPrepareCountdown(phases.CountdownRemaining);
                if (hostPresentation?.Camera != null && TryGetSceneCamera(out Vector3 initialCamera, out float _))
                {
                    cameraView = new OfficialBattleCameraView(hostPresentation.Camera, worldRoot,
                        environmentGameId, initialCamera, sceneView.Body);
                    cameraView.Refresh(checkpoint, Phase);
                    cameraView.SetVisible(hudVisible);
                }
                skillGraphPlayback?.BindSceneHosts(cameraView, visibilityHost: sceneView, masterResolver: sceneView);
                if (hudRoot != null) hudRoot.SetActive(hudVisible);
                skillGraphPlayback?.SetDamageUIVisible(hudVisible);
                ConfigureSpeeds(combatCatalog.SpeedSettings, request.SpeedModeType, request.SpeedConditionMet);
            }
            catch
            {
                if (executor == nextExecutor)
                {
                    DisposeSession();
                }
                else
                {
                    nextSkillGraphPlayback?.Dispose();
                    nextScene?.Dispose();
                    nextEnvironment?.Dispose();
                    nextNativeModels?.Dispose();
                    if (nextHudRoot != null) Destroy(nextHudRoot);
                    renderPipelineScope?.Dispose();
                    renderPipelineScope=null;
                    hostPresentation?.Dispose();
                    hostPresentation = null;
                    modeAdapter = null;
                }
                throw;
            }
        }

        private static void OrientUnit(Transform root, FightBattleUnitState unit)
        {
            Vector3 scale = root.localScale;
            bool flip = unit.HasFlipX ? unit.FlipX : unit.Direction == 1;
            scale.x = Mathf.Abs(scale.x) * (flip ? -1f : 1f);
            root.localScale = scale;
        }

        private float remainingStepDelay;

        /// <summary>Call once per frame from the mode adapter. Preparation, pause and presentation
        /// gate advancement internally; the mode only decides whether its world is suspended.</summary>
        public bool Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!CanAdvance) return false;
            remainingStepDelay = Mathf.Max(0f, remainingStepDelay - deltaTime);
            if (remainingStepDelay > 0f) return false;
            Advance();
            return true;
        }

        private FightBattleExecutionResult Advance()
        {
            if (executor == null) throw new InvalidOperationException("Begin a battle session before advancing it.");
            if (stepping) throw new InvalidOperationException("A battle step is already being applied.");
            if (executor.State.Finished) throw new InvalidOperationException("The battle is already finished.");
            if (!CanAdvance) throw new InvalidOperationException("The battle cannot advance in its current phase.");

            stepping = true;
            try
            {
                FightBattleExecutionResult result = executor.Step(executor.State.Sequence,
                    executor.State.Counter + 1);
                // Background tower battles keep the same executor and step timing, but
                // their inactive native models cannot receive Animator/skill-graph leaves.
                // Rebuild the visible snapshot when the host returns to the foreground.
                if (hudVisible)
                {
                    sceneView.Refresh(result.Checkpoint, result.Event);
                    if (hudRoot != null)
                    {
                        hudView.Refresh(result.Checkpoint);
                        hudView.RefreshSkillSlots(result.Checkpoint, skillIconResolver);
                        hudView.RefreshPet(result.Checkpoint, statueProgress?.Save?.Player);
                        hudView.RefreshTimeline(result.Checkpoint, timelinePortraitResolver,
                            timelineFactionResolver, true, result.Event);
                        hudView.RefreshBoss(result.Checkpoint, UnitName);
                        hudView.RefreshStageStatus(result.Checkpoint);
                    }
                    // Install the settled CD snapshot before native zero-delay starts.
                    // Delayed starts and each skill's own Remove drive subsequent states.
                    skillGraphPlayback?.Play(result.Event, result.Checkpoint,
                        sceneView.Body, sceneView.TryGetPetTransform);
                    if (!result.Replayed)
                        sceneView.ShowDamageFloats(result.Event, result.Checkpoint, hudCatalog.Font, damageColors,
                            skillGraphPlayback == null ? null : damage => skillGraphPlayback.OwnsDamageUI(result.Event, damage),
                            skillGraphPlayback == null ? null : cast => skillGraphPlayback.OwnsReplayUI(cast),
                            skillGraphPlayback == null ? null : action => skillGraphPlayback.OwnsStatusCustomActionUI(action));
                }
                // Play dispatches authored zero-delay hits immediately. Match that
                // boundary before publishing the step/checkpoint to its observers.
                if (executor.AdvanceStatusSecondsWithPresentation(0d,
                    elapsed => skillGraphPlayback?.Tick((float)elapsed)) && hudVisible)
                {
                    sceneView.RefreshStatusSnapshot(result.Checkpoint);
                    hudView.Refresh(result.Checkpoint);
                }
                LastStepSeconds = result.Event != null && result.Event.Duration > 0f
                    ? result.Event.Duration : 0.35f;
                if (result.Event != null)
                {
                    LastStepSeconds = Mathf.Max(LastStepSeconds, result.Event.ActionDuration);
                    foreach (FightBattleDamageEvent damage in result.Event.DamageEvents)
                        if (damage != null) LastStepSeconds = Mathf.Max(LastStepSeconds, damage.Delay);
                    foreach (FightBattleSkillCastEvent cast in result.Event.SkillCasts)
                        if (cast != null) LastStepSeconds = Mathf.Max(LastStepSeconds, cast.Delay
                            + Mathf.Max(0f, cast.Presentation?.Duration ?? 0f));
                }
                // A settled result is not a completed presentation. In particular the last lethal
                // hit must reach its configured time before the mode destroys the battle scene.
                stepPlayback = new FightProcessPlayback<float>(new[] { LastStepSeconds }, value => value,
                    _ => CompleteStepPresentation());
                IOfficialBattleModeAdapter adapter = modeAdapter;
                StepCompleted?.Invoke(result);
                adapter?.OnBattleStepCompleted(result);
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

        /// <summary>Stop future actions and complete the current presentation through the normal
        /// report-before-result handoff. The mode persists Checkpoint before returning.</summary>
        public bool RequestPartEnd()
        {
            if (!CanLeaveBattle() || !Zjcs.Offline.Combat.OfflineBattlePartEnd.Finish(Checkpoint)) return false;
            if (phases.IsPreparing) phases.BeginBattleEnd();
            return true;
        }

        public bool RequestForfeit()
        {
            if (!CanLeaveBattle() || !Zjcs.Offline.Combat.OfflineBattleForfeit.Finish(Checkpoint)) return false;
            if (phases.IsPreparing) phases.BeginBattleEnd();
            return true;
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
                    player.Hp = Math.Max(1L, Math.Min(player.MaxHp,
                        player.Hp + (long)(player.MaxHp * (double)effect.HpPercentChange)));
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
        private void ConfigureSpeeds(FightBattleSpeedSettings settings, int modeType,
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
        public bool Ready()
        {
            if (!phases.Ready()) return false;
            return phases.StartPlay();
        }

        public bool StartPlay() => phases.StartPlay();

        private void ApplyTimeScale()
        {
            if (!ownsTimeScale) return;
            Time.timeScale = paused ? 0f : phases.Phase == FightBattlePhase.Preparing
                ? 1f : (float)speedMultiplier;
        }

        private void Update()
        {
            phases.Tick(Time.unscaledDeltaTime);
            if (phases.IsPreparing && preparationView != null)
            {
                OfficialBattlePointerFrame pointer = PreparationPointerProvider != null ? PreparationPointerProvider()
                    : new OfficialBattlePointerFrame { Position = Input.mousePosition,
                        Down = Input.GetMouseButtonDown(0), Held = Input.GetMouseButton(0),
                        Up = Input.GetMouseButtonUp(0) };
                if (cameraView == null || cameraView.AllowsGameObjectInput)
                    preparationView.Tick(pointer, Time.unscaledDeltaTime);
            }
            if (executor != null && hudRoot == null && pendingHudParent != null)
            {
                // FightMainPanelLoader.DealyLoad uses WaitForSeconds, hence scaled time.
                remainingHudDelay -= Time.deltaTime;
                if (remainingHudDelay <= 0) MountDelayedHud();
            }
            hudView?.Tick(Time.unscaledDeltaTime);
            sceneView?.Tick(Time.deltaTime);
            if (executor != null) cameraView?.Refresh(Checkpoint, Phase);
            if (!paused)
            {
                if (executor != null && (Phase == FightBattlePhase.Play || Phase == FightBattlePhase.PlayStart)
                    && executor.AdvanceStatusSecondsWithPresentation(Time.deltaTime,
                        elapsed => skillGraphPlayback?.Tick((float)elapsed)))
                {
                    // Timer state changes at the hit's playback time rather than
                    // at the eager whole-cast settlement boundary.
                    sceneView?.RefreshStatusSnapshot(Checkpoint);
                    hudView?.Refresh(Checkpoint);
                }
                if (executor == null || Phase != FightBattlePhase.Play && Phase != FightBattlePhase.PlayStart)
                    skillGraphPlayback?.Tick(Time.deltaTime);
                stepPlayback?.Tick(Time.deltaTime);
                // A disk checkpoint can contain the final settled hit before its
                // presentation callback was delivered. No Step can run in that state.
                // Defer handoff until Update so the caller can finish attaching hosts
                // after Begin, and use the normal report/completion ordering once.
                if (executor != null && executor.State.Finished
                    && !IsPresentingStep && !battleCompletedRaised
                    && (executor.State.StatusSeconds?.PendingHits.Count ?? 0) == 0)
                    CompleteStepPresentation();
            }
        }

        private void LateUpdate()
        {
            if (!paused) skillGraphPlayback?.LateTick();
        }

        private void CompleteStepPresentation()
        {
            FightBattleCheckpoint completedCheckpoint = null;
            FightBattleReport completedReport = null;
            if (executor != null && executor.State.Finished)
            {
                phases.BeginBattleEnd();
                EndBattle();
                if (!battleCompletedRaised)
                {
                    battleCompletedRaised = true;
                    completedCheckpoint = executor.State;
                    completedReport = FightBattleReportBuilder.Build(completedCheckpoint);
                    finalReport = completedReport;
                }
            }
            IOfficialBattleModeAdapter adapter = modeAdapter;
            if (completedReport != null)
            {
                BattleReportReady?.Invoke(completedReport);
                adapter?.OnBattleReportReady(completedReport);
            }
            // Hosts may dispose the session from OnBattleCompleted. Presentation
            // observers must see the final checkpoint and BattleEnd before that handoff.
            StepPresentationCompleted?.Invoke();
            if (completedCheckpoint != null)
            {
                BattleCompleted?.Invoke(completedCheckpoint);
                adapter?.OnBattleCompleted(completedCheckpoint);
            }
        }

        public void AttachUnitTransform(int unitId, Transform transform)
        {
            if (sceneView == null)
                throw new InvalidOperationException("Begin a battle session before attaching a unit transform.");
            sceneView.AttachUnitTransform(unitId, transform);
        }

        public void DisposeSession()
        {
            leaveDialog.Dispose();
            // Original graph Stop callbacks still need the live camera and unit hosts.
            skillGraphPlayback?.Dispose();
            skillGraphPlayback = null;
            cameraView?.Dispose();
            cameraView = null;
            hostPresentation?.Dispose();
            hostPresentation = null;
            stepPlayback?.Cancel();
            stepPlayback = null;
            remainingStepDelay = 0f;
            if (hudView != null)
            {
                hudView.ControlClicked -= OnHudControlClicked;
                hudView.SpeedCycled -= OnSpeedCycled;
            }
            phases.PhaseChanged -= OnPhaseChanged;
            phases.AutoStartRequested -= OnAutoStartRequested;
            phases.CountdownChanged -= OnPrepareCountdownChanged;
            phases.Finish();
            if (preparationView != null)
            {
                preparationView.PositionChanged -= OnPreparationPositionChanged;
                preparationView.Dispose();
                preparationView = null;
            }
            sceneView?.Dispose();
            environmentView?.Dispose();
            renderPipelineScope?.Dispose();
            renderPipelineScope=null;
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
            skillGraphPlayback = null;
            hudView = null;
            hudRoot = null;
            combatCatalog = null;
            hudCatalog = null;
            damageColors = null;
            skillIconResolver = null;
            timelinePortraitResolver = null;
            timelineFactionResolver = null;
            pendingHudParent = null;
            remainingHudDelay = 0;
            sessionOptions = null;
            PreparationPointerProvider = null;
            hudVisible = true;
            battleCompletedRaised = false;
            finalReport = null;
            modeAdapter = null;
        }

        private void OnDestroy()
        {
            DisposeSession();
        }

        private void OnHudControlClicked(string path)
        {
            if (path == OfficialBattleHudView.StartButtonPath)
            {
                Ready();
                return;
            }
            if (path == OfficialBattleHudView.CloseButtonPath)
            {
                if (!CanLeaveBattle()) return;
                string key = FightBattleLeaveRules.ConfirmationKey(Checkpoint.StageType, Phase,
                    sessionOptions?.VoidFissureLayer);
                if (key == null) DispatchLeave();
                else leaveDialog.Show(hudRoot.transform.parent, hudCatalog.Font, key, DispatchLeave);
                return;
            }
            IOfficialBattleModeAdapter adapter = modeAdapter;
            HudControlClicked?.Invoke(path);
            adapter?.OnBattleHudControlClicked(path);
        }

        private bool CanLeaveBattle() => Checkpoint != null && !Checkpoint.Finished
            && FightBattleLeaveRules.CanLeave(Phase, sessionOptions == null || sessionOptions.LeaveInPreparing,
                sessionOptions == null || sessionOptions.LeaveInPlaying);

        private void DispatchLeave()
        {
            // A live battle can finish while the original confirmation window is open.
            if (!CanLeaveBattle()) return;
            var adapter = modeAdapter;
            HudControlClicked?.Invoke(OfficialBattleHudView.CloseButtonPath);
            adapter?.OnBattleHudControlClicked(OfficialBattleHudView.CloseButtonPath);
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
            if (executor != null) executor.State.PreparationPending = next == FightBattlePhase.Preparing;
            preparationView?.SetPreparing(next == FightBattlePhase.Preparing);
            hudView?.RefreshPhase(next, sessionOptions);
            hudView?.RefreshPrepareCountdown(next == FightBattlePhase.Preparing
                ? phases.CountdownRemaining : 0);
            if (next == FightBattlePhase.End && ownsTimeScale)
            {
                Time.timeScale = 1f;
                ownsTimeScale = false;
            }
            else ApplyTimeScale();
            PhaseChanged?.Invoke(previous, next);
        }

        private void OnAutoStartRequested() => Ready();

        private void OnPreparationPositionChanged() => PreparationPositionChanged?.Invoke();

        private void OnPrepareCountdownChanged(double remaining)
        {
            if (executor != null) executor.State.PreparationSecondsRemaining = remaining;
            hudView?.RefreshPrepareCountdown(remaining);
        }

        private void RefreshPetFunction(OfficialBattleHudView view, FightBattleCheckpoint checkpoint)
        {
            if (statueProgress?.Save?.Player == null) return;
            view.RefreshPet(checkpoint, statueProgress.Save.Player);
        }

        private void MountDelayedHud()
        {
            if (executor == null || hudView == null || pendingHudParent == null || hudRoot != null) return;
            hudRoot = hudView.Mount(pendingHudParent, hostPresentation?.Camera);
            RefreshPetFunction(hudView, Checkpoint);
            hudView.Refresh(Checkpoint);
            hudView.RefreshSkillSlots(Checkpoint, skillIconResolver);
            hudView.RefreshTimeline(Checkpoint, timelinePortraitResolver, timelineFactionResolver, false);
            hudView.RefreshBoss(Checkpoint, UnitName);
            hudView.RefreshStageStatus(Checkpoint);
            hudView.RefreshPhase(Phase, sessionOptions);
            hudView.RefreshPrepareCountdown(phases.CountdownRemaining);
            if (speedSettings != null) hudView.RefreshSpeed(speedSettings, speedIndex, unlockedSpeeds);
            hudRoot.SetActive(hudVisible);
        }
    }
}
