using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    /// <summary>Weekly activity adapter over the common official battle runtime.</summary>
    public sealed class WeeklyChallengeBattleRunner : MonoBehaviour, IOfficialBattleModeAdapter
    {
        private HomeFeatureRuntime host;
        private OfficialBattleRuntimeController controller;
        private Transform battleRoot, playerTransform, previousPlayerParent;
        private Vector3 previousPlayerPosition, previousPlayerScale;
        private Quaternion previousPlayerRotation;
        private bool complete;
        private FightBattleReport finalReport;
        private OfflineWeeklyChallengeSession session;
        private int stageId;
        private long periodStartLocalTicks;

        public event Action<FightBattleCheckpoint> Completed;
        public event Action Left;
        public OfficialBattleRuntimeController Controller => controller;
        public FightBattleReport FinalReport => finalReport;

        public void Begin(HomeFeatureRuntime runtime, int stageId)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (controller != null) throw new InvalidOperationException("Weekly battle is already active.");
            this.stageId = stageId;
            session = new OfflineWeeklyChallengeSession(System.IO.Path.Combine(host.SavesRoot, "saves",
                host.ActiveProfileSlotId, "weekly-challenge-save.json"));
            OfflineWeeklyChallengeBattleCatalog catalog = OfflineWeeklyChallengeBattleCatalog.LoadDefault();
            if (!catalog.TryGetBattle(stageId, out Map10RuntimeStage stage,
                    out Map10RuntimeRoster roster, out Map10RuntimeBattleScene scene))
                throw new InvalidOperationException("Official weekly battle stage is unavailable: " + stageId);
            FightBattleCheckpoint checkpoint;
            if (session.TryResumeBattle(stageId, out string savedCheckpoint, out periodStartLocalTicks))
            {
                checkpoint = FightBattleCheckpointSerializer.Load(savedCheckpoint);
                if (checkpoint.StageId != stageId)
                    throw new InvalidOperationException("Saved weekly challenge stage does not match its activity entry.");
            }
            else
            {
                periodStartLocalTicks = session.CurrentPeriodStartLocalTicks(DateTime.Now);
                checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest
                {
                    Stage = stage, Roster = roster, Scene = scene,
                    Player = host.Profile.Save.Player, World = host.Profile.Save.World,
                    Progression = host.DailyDungeonProgression,
                    RandomSeed = (int)((uint)Guid.NewGuid().GetHashCode() % (FightRandomStream.Modulus - 1)) + 1
                });
                session.StartBattle(stageId, periodStartLocalTicks, FightBattleCheckpointSerializer.Save(checkpoint));
            }
            MountBattle(checkpoint, scene, stage);
        }

        private void MountBattle(FightBattleCheckpoint checkpoint, Map10RuntimeBattleScene scene, Map10RuntimeStage stage)
        {
            playerTransform = host.HomePlayerTransform;
            if (playerTransform == null) throw new InvalidOperationException("Official home player model is absent.");
            previousPlayerParent = playerTransform.parent;
            previousPlayerPosition = playerTransform.localPosition;
            previousPlayerRotation = playerTransform.localRotation;
            previousPlayerScale = playerTransform.localScale;
            battleRoot = new GameObject("WeeklyChallengeBattleWorld").transform;
            FightBattleUnitState player = checkpoint.Units.Find(value => value.Id == 1);
            Map10RuntimeVector3 origin = scene.FightGridGroundComponent.OriginPos;
            battleRoot.position = playerTransform.position - new Vector3(origin.X + player.X,
                origin.Y + player.Y, origin.Z);
            playerTransform.SetParent(battleRoot, true);
            OfficialCombatAssetCatalog assets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = assets.Load();
            controller = gameObject.AddComponent<OfficialBattleRuntimeController>();
            controller.Begin(new OfficialBattleRequest
            {
                Checkpoint = checkpoint,
                WorldRoot = battleRoot,
                HudParent = host.Hud.CanvasTransform,
                ModeAdapter = this,
                TimelinePortrait = OfficialBattleCharacterPortraits.ForProfile(battleRoot.gameObject,
                    host.DailyDungeonProgression, host.ActiveProfileSlotId, host.SavesRoot),
                HostPresentation = new OfficialBattleHostPresentation(host.Scene.Camera,
                    host.Scene.Root.transform, host.Hud.CanvasTransform, host.SetBattlePresentationActive),
                AnimationGroupResolver = unit => OfficialCharacterAnimationProfile.GroupForProfession(unit.Profession),
                CombatAssets = assets,
                EnvironmentGameId = stage.GameId,
                StatueProgress = host.Profile,
                Options = OfficialMapBattleSessionOptionsBuilder.Create(stage, true, 3, 3, player.X, player.Y),
                InitialStepDelay = 0f
            });
            controller.AttachUnitTransform(1, playerTransform);
            controller.ConfigureSpeeds(combat.SpeedSettings, 0, conditionId =>
            {
                var context = new OfficialConditionContext { Level = host.Level };
                foreach (int map in host.Profile.Save.Player.UnlockedMapClassIds ?? new List<int>())
                    context.UnlockedMapClassIds.Add(map);
                return host.DailyDungeonProgression.Catalog.EvaluateCondition(conditionId, context);
            });
        }

        private void Update()
        {
            if (controller != null && !complete) controller.Tick(Time.deltaTime);
        }

        public void OnBattleCompleted(FightBattleCheckpoint checkpoint)
        {
            if (complete || checkpoint == null) return;
            complete = true;
            session.CompleteBattle(stageId, periodStartLocalTicks,
                checkpoint.Outcome != null && checkpoint.Outcome.Won,
                stageId == 4800 ? PlayerDamageDealt(checkpoint) : 0);
            DisposeBattle();
            Completed?.Invoke(checkpoint);
            Destroy(this);
        }

        public void OnBattleHudControlClicked(string path)
        {
            if (path == null || !path.EndsWith("/CloseBtn/CloseBtn_normal", StringComparison.Ordinal)) return;
            complete = true;
            DisposeBattle();
            Left?.Invoke();
            Destroy(this);
        }

        public void OnBattleStepCompleted(FightBattleExecutionResult result)
        {
            if (result?.Checkpoint == null) return;
            session.SaveBattleCheckpoint(stageId, periodStartLocalTicks,
                FightBattleCheckpointSerializer.Save(result.Checkpoint));
        }
        public void OnBattleReportReady(FightBattleReport report) { finalReport = report; }

        private static long PlayerDamageDealt(FightBattleCheckpoint checkpoint)
        {
            FightBattleReport report = FightBattleReportBuilder.Build(checkpoint);
            FightBattleReportPlayerGroups groups = FightBattleReportGrouping.Build(report, 1);
            FightBattleReportPlayerGroup player = groups.Players.Find(value => value.TargetId == 1);
            if (player == null) return 0;
            long damage = 0;
            foreach (FightBattleReportUnit unit in player.Normal) damage = checked(damage + Math.Max(0, unit.DamageDealt));
            foreach (FightBattleReportUnit unit in player.Summon) damage = checked(damage + Math.Max(0, unit.DamageDealt));
            foreach (FightBattleReportUnit unit in player.Pet) damage = checked(damage + Math.Max(0, unit.DamageDealt));
            return damage;
        }

        private void OnDestroy() => DisposeBattle();

        private void DisposeBattle()
        {
            if (controller != null)
            {
                controller.DisposeSession();
                Destroy(controller);
                controller = null;
            }
            if (playerTransform != null)
            {
                playerTransform.SetParent(previousPlayerParent, false);
                playerTransform.localPosition = previousPlayerPosition;
                playerTransform.localRotation = previousPlayerRotation;
                playerTransform.localScale = previousPlayerScale;
                playerTransform = null;
            }
            if (battleRoot != null)
            {
                Destroy(battleRoot.gameObject);
                battleRoot = null;
            }
        }
    }
}
