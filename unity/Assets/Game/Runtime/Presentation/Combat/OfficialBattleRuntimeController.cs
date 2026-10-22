using System;
using UnityEngine;
using Zjcs.Combat.Official;

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

        public event Action<FightBattleExecutionResult> StepCompleted;
        public event Action<string> HudControlClicked;

        public FightBattleCheckpoint Checkpoint => executor == null ? null : executor.State;

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
            Func<FightBattleUnitState, Sprite> timelineFactionIcon = null)
        {
            if (executor != null) throw new InvalidOperationException("A battle session is already active.");
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (worldRoot == null) throw new ArgumentNullException(nameof(worldRoot));
            if (hudParent == null) throw new ArgumentNullException(nameof(hudParent));
            if (gridMapper == null) throw new ArgumentNullException(nameof(gridMapper));
            if (skillIcon == null) throw new ArgumentNullException(nameof(skillIcon));
            if (timelinePortrait == null) throw new ArgumentNullException(nameof(timelinePortrait));
            if (modelKey == null) throw new ArgumentNullException(nameof(modelKey));
            if (animationForEvent == null) throw new ArgumentNullException(nameof(animationForEvent));
            if (orientUnit == null) throw new ArgumentNullException(nameof(orientUnit));

            OfficialCombatAssetCatalog resolvedCombatAssets = combatAssets != null
                ? combatAssets : OfficialCombatAssetCatalog.LoadDefault();
            OfficialBattleModelCatalog resolvedModelCatalog = modelCatalog != null
                ? modelCatalog : OfficialBattleModelCatalog.LoadDefault();
            OfficialBattleHudCatalog resolvedHudCatalog = hudCatalog != null
                ? hudCatalog : OfficialBattleHudCatalog.LoadDefault();

            FightBattleExecutor nextExecutor = restoreCheckpoint
                ? resolvedCombatAssets.RestoreBattle(checkpoint)
                : resolvedCombatAssets.BeginBattle(checkpoint);
            var nextHud = new OfficialBattleHudView(resolvedHudCatalog);
            var nextScene = new OfficialBattleSceneView(resolvedModelCatalog, worldRoot,
                modelKey, gridMapper, animationForEvent, orientUnit);
            GameObject nextHudRoot = null;
            try
            {
                nextHudRoot = nextHud.Mount(hudParent);
                nextHud.ControlClicked += OnHudControlClicked;
                nextHud.Refresh(checkpoint);
                nextHud.RefreshSkillSlots(checkpoint, skillIcon);
                nextHud.RefreshTimeline(checkpoint, timelinePortrait, timelineFactionIcon, false);
                nextScene.Refresh(checkpoint);
                executor = nextExecutor;
                hudView = nextHud;
                sceneView = nextScene;
                hudRoot = nextHudRoot;
                skillIconResolver = skillIcon;
                timelinePortraitResolver = timelinePortrait;
                timelineFactionResolver = timelineFactionIcon;
            }
            catch
            {
                nextScene.Dispose();
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
                StepCompleted?.Invoke(result);
                return result;
            }
            finally
            {
                stepping = false;
            }
        }

        private Func<int, Sprite> skillIconResolver;

        private void Update()
        {
            hudView?.Tick(Time.unscaledDeltaTime);
        }

        public void DisposeSession()
        {
            if (hudView != null) hudView.ControlClicked -= OnHudControlClicked;
            sceneView?.Dispose();
            if (hudRoot != null) Destroy(hudRoot);
            executor = null;
            sceneView = null;
            hudView = null;
            hudRoot = null;
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
    }
}
