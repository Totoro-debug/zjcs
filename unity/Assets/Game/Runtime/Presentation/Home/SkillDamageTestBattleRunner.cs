using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    // StageDamageTestPanel request adapter. Grid, formations, skill assembly and
    // property calculations reuse the migrated official common battle pipeline.
    public sealed class SkillDamageTestBattleRunner : MonoBehaviour, IOfficialBattleModeAdapter
    {
        private HomeFeatureRuntime host;
        private OfficialSkillDamageTestRequest request;
        private OfficialBattleRuntimeController controller;
        private Transform battleRoot;
        private bool complete;
        public event Action<FightBattleCheckpoint> Completed;
        public event Action Left;
        public OfficialBattleRuntimeController Controller => controller;

        public void Begin(HomeFeatureRuntime runtime, OfficialSkillDamageTestRequest settings)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            request = settings ?? throw new ArgumentNullException(nameof(settings));
            var data = JsonUtility.FromJson<Map10RuntimeBattleData>(request.BattleRuntimeJson);
            if (data == null || (data.manifest != "854_156383" && data.manifest != "884_156692"))
                throw new InvalidOperationException("Original battle data is absent.");
            Map10RuntimeStage stage = data.stages.Single(value => value.Id == request.StageId);
            Map10RuntimeRoster roster = data.monsterRosters.Single(value => value.StageId == request.StageId);
            Map10RuntimeBattleScene scene = data.battleScenes.Single(value => value.GameId == stage.GameId);
            FightBattleCheckpoint checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest
            {
                Stage = stage, Roster = roster, Scene = scene,
                Player = host.Profile.Save.Player, World = host.Profile.Save.World,
                Progression = host.DailyDungeonProgression,
                RandomSeed = (int)((uint)Guid.NewGuid().GetHashCode() % (FightRandomStream.Modulus - 1)) + 1,
                MonsterLevel = request.MonsterLevel, MaxRound = request.MaxRound,
                DamageTest = new FightDamageTestRules
                {
                    Difficulty = request.Difficulty,
                    EnableMonsterOneDamage = request.EnableMonsterOneDamage,
                    EnableMonsterUnlimitHp = request.EnableMonsterUnlimitHp
                }
            });
            MountBattle(checkpoint, scene, stage);
        }

        private void MountBattle(FightBattleCheckpoint checkpoint, Map10RuntimeBattleScene scene, Map10RuntimeStage stage)
        {
            Transform playerTransform = host.HomePlayerTransform;
            if (playerTransform == null) throw new InvalidOperationException("Official home player model is absent.");
            battleRoot = new GameObject("SkillDamageTestBattleWorld").transform;
            FightBattleUnitState player = checkpoint.Units.First(value => value.Id == 1);
            battleRoot.position = playerTransform.position - new Vector3(
                scene.FightGridGroundComponent.OriginPos.X + player.X,
                scene.FightGridGroundComponent.OriginPos.Y + player.Y,
                scene.FightGridGroundComponent.OriginPos.Z);
            OfficialCombatAssetCatalog assets = OfficialCombatAssetCatalog.LoadDefault();
            controller = gameObject.AddComponent<OfficialBattleRuntimeController>();
            controller.Begin(new OfficialBattleRequest
            {
                Checkpoint = checkpoint,
                WorldRoot = battleRoot,
                PlayerAppearance = new OfficialBattlePlayerAppearanceSource(1, 1,
                    () => host.Profile.Save.Player, id => host.DailyDungeonProgression.Catalog.ItemType(id)),
                HudParent = host.Hud.CanvasTransform,
                ModeAdapter = this,
                SpeedConditionMet = conditionId => {
                    var context = new OfficialConditionContext { Level = host.Level };
                    foreach (int map in host.Profile.Save.Player.UnlockedMapClassIds ?? new List<int>()) context.UnlockedMapClassIds.Add(map);
                    return host.DailyDungeonProgression.Catalog.EvaluateCondition(conditionId, context);
                },
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
        }

        private void Update()
        {
            if (controller == null || complete) return;
            controller.Tick(Time.deltaTime);
        }

        public void OnBattleCompleted(FightBattleCheckpoint checkpoint)
        {
            if (complete || checkpoint == null) return;
            complete = true;
            DisposeBattle();
            Completed?.Invoke(checkpoint);
            Destroy(this);
        }

        public void OnBattleHudControlClicked(string path)
        {
            if (!path.EndsWith("/CloseBtn/CloseBtn_normal", StringComparison.Ordinal)) return;
            complete = true;
            DisposeBattle();
            Left?.Invoke();
            Destroy(this);
        }

        public void OnBattleStepCompleted(FightBattleExecutionResult result) { }

        public FightBattleReport FinalReport { get; private set; }
        public void OnBattleReportReady(FightBattleReport report) { FinalReport = report; }

        private void OnDestroy() => DisposeBattle();
        private void DisposeBattle()
        {
            if (controller != null) {
                controller.DisposeSession(); Destroy(controller); controller = null;
            }
            if (battleRoot != null) { Destroy(battleRoot.gameObject); battleRoot = null; }
        }

    }
}
