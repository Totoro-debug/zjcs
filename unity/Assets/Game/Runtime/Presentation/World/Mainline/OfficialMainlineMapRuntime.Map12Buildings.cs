using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap12BuildingData map12Buildings;
        private readonly Dictionary<int, Map12BuildingContext> map12BuildingContexts = new Dictionary<int, Map12BuildingContext>();
        private GameObject map12BuildingDialog;
        private bool map12BuildingStagePending;
        private const string MountLiftCompletionTag = "map12:building:3501";

        private void LoadMap12BuildingOperations()
        {
            if (mapClassId != 12 || map12Buildings != null) return;
            var source = Resources.Load<TextAsset>(MapResource("building-operations"));
            map12Buildings = source == null ? null : JsonUtility.FromJson<OfficialMap12BuildingData>(source.text);
            if (map12Buildings?.manifest != "871_156598" || map12Buildings.Buildings?.Length != 2
                || map12Buildings.Conditions?.Length != 5 || map12Buildings.MountLiftAwards?.Length != 2)
                throw new InvalidDataException("Original Map12 building operations are missing.");
        }

        private bool TryMap12BuildingInteraction(Map10RuntimeEntity entity)
        {
            if (mapClassId != 12 || (entity.EntityClassId != 6302 && entity.EntityClassId != 6103)) return false;
            LoadMap12BuildingOperations();
            var source = map12Buildings.Buildings.Single(v => v.PlacementId == entity.PlacementId);
            if (regions.IsPublicRegion(entity.Region)) return false; // Public dungeon/tower feature binding follows separately.
            if (!map12BuildingContexts.TryGetValue(entity.PlacementId, out var context))
                map12BuildingContexts.Add(entity.PlacementId, context = new Map12BuildingContext(this, entity, source));
            if (entity.State < 0 || entity.State >= source.States.Length) return false;
            var operation = context.ResolveOperation(source.States[entity.State].OperationId);
            if (entity.EntityClassId == 6103)
            {
                // The unactivated original branch only plays its prerequisite
                // story. The private daily-dungeon provider is a separate flow.
                if (entity.State != 0 || operation.Type != "ECWorldStageInteractionOperationCondition"
                    || operation.NeedAllMeet || operation.ConditionIds.Any(id => context.Condition(id) == true)) return false;
            }
            operation.DoOper(context);
            return true;
        }

        private sealed class Map12BuildingContext : IWorldStageOperationContext
        {
            private readonly OfficialMainlineMapRuntime host;
            private readonly Map10RuntimeEntity entity;
            private readonly OfficialMap12Building source;
            public Map12BuildingContext(OfficialMainlineMapRuntime host, Map10RuntimeEntity entity, OfficialMap12Building source)
            { this.host = host; this.entity = entity; this.source = source; }
            public bool IsStageActive => !host.closed && host.battleController == null
                && !host.IsEntityLocked(entity) && entity.State >= 0 && entity.State < source.States.Length;
            public void ReportError(string message) => Debug.LogError(message, host);
            public WorldStageInteractionOperation ResolveOperation(long id) => id < 0 ? null : source.Operations.Single(v => v.Id == id);
            public bool? Condition(int id)
            {
                var condition = host.map12Buildings.Conditions.SingleOrDefault(v => v.ClassId == id);
                return condition == null ? (bool?)null : host.IsMapTalkConditionMet(condition.Condition);
            }
            public void StartStage(int subState)
            {
                if (!IsStageActive) return;
                if (entity.EntityClassId != 6302 || entity.State != 0 || subState != 0)
                    throw new InvalidDataException("Unbound original building stage behaviour.");
                if (host.map12BuildingStagePending) return;
                host.map12BuildingStagePending = true;
                host.StartCoroutine(host.BeginMap12BuildingStage(entity));
            }
            public void PlayStory(int scenario, Action completed)
            {
                var talk = host.ResolveMapTalk(entity, scenario);
                if (talk == null) { completed?.Invoke(); return; }
                if (!string.IsNullOrEmpty(talk.TalkId)) host.StartStoryGraph(talk.TalkId, entity, completed);
                else host.PlayMapTalkDialog(talk, entity, completed, false);
            }
            public void OpenWindow(string name, bool report, int panelId, Action<int> click)
            {
                if (report || panelId != 0) throw new InvalidDataException("Original building window reporting changed.");
                if (name == "WorldMap/WorldFantasyLadderDialog") host.OpenMap12MountLiftDialog(entity, source, this, click);
                else if (name == "WorldMap/StageEndTipUI")
                {
                    var text = host.map12Buildings.Texts.FirstOrDefault(v => v.key == source.EndTipKey)
                        ?? throw new InvalidDataException("Original completed building notice is missing.");
                    host.hud.ShowOfficialNotice(text.value);
                }
                else throw new InvalidDataException("Building feature window is not bound: " + name);
            }
        }

        private IEnumerator BeginMap12BuildingStage(Map10RuntimeEntity entity)
        {
            // The original button sends an asynchronous stage request before
            // its remaining listeners resolve TowerActive's registered object.
            // Complete local transport on the next frame, after those listeners.
            yield return null;
            map12BuildingStagePending = false;
            if (!closed && battleController == null) StartEntityStage(entity, null);
        }

        private (int level, int subRank) Map12BuildingLevel(OfficialMap12Building source)
        {
            int type = regions.IsPublicRegion(mapEntities[source.PlacementId].Region) ? source.PublicLevelType : source.PrivateLevelType;
            if (type == 2) return (progressionSave.Save.Player.Level, progressionSave.Save.Player.SubRank);
            if (type != 1) throw new InvalidDataException("Unbound original Map12 building level type: " + type);
            return (source.Absolute ? source.Level : source.RegionLevel + source.Level,
                source.SubRank == 0 ? source.RegionSubRank : source.SubRank);
        }

        private void OpenMap12MountLiftDialog(Map10RuntimeEntity entity, OfficialMap12Building source,
            Map12BuildingContext context, Action<int> click)
        {
            if (map12BuildingDialog != null) return;
            var root = Instantiate(Resources.Load<GameObject>(NativeUiResource("WorldFantasyLadderDialog")), hud.ui.OverlayRoot, false);
            map12BuildingDialog = root;
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = NativeUi.Text;
            var assets = Resources.Load<OfficialMap11UiAssets>(NativeUiResource("DynamicItems"));
            var level = Map12BuildingLevel(source);
            root.GetComponent<StageDialog>().Init(new WorldStageUiContext
            {
                NameKey = "entity_" + entity.EntityClassId + "_name", DescriptionKey = "entity_" + entity.EntityClassId + "_desc",
                DialogIconPrefab = Resources.Load<GameObject>(NativeUiResource(entity.EntityClassId + "_DialogIcon")),
                Background = assets.mapBackground, Level = level.level,
                IsActive = () => context.IsStageActive, StartStage = index => context.StartStage(index),
                ShowRewardPreview = slot => OfficialMap12BuildingRewardView.Populate(slot, map12Buildings,
                    level.level, level.subRank, assets, MapProfile.NativeUiRoot, key => NativeUi.Text(key)),
                Close = CloseMap12BuildingDialog
            });
            root.GetComponents<WorldStageOperationWindowClickUi>()[0].InitWindowClick(click);
            root.GetComponent<WorldPopupWindow>().Show();
        }

        private void CloseMap12BuildingDialog()
        {
            if (map12BuildingDialog == null) return;
            var root = map12BuildingDialog;
            map12BuildingDialog = null;
            root.GetComponent<WorldPopupWindow>().Hide(() => Destroy(root));
        }

        private void SynchronizeMap12BuildingCompletions()
        {
            if (mapClassId != 12 || puzzles == null || !progressionSave.Save.World.CompletedEntityTags.Contains(MountLiftCompletionTag)) return;
            puzzles.SetState(3501, 1);
            mapEntities[3501].State = 1;
        }

        // Called before any battle counters or checkpoint removal. A failed
        // profile write leaves the finished checkpoint available for retry.
        private bool SettleMap12MountLiftBattle(Map10RuntimeEntity entity, int stageId, bool won)
        {
            if (mapClassId != 12 || entity?.PlacementId != 3501 || stageId != 102 || !won) return true;
            LoadMap12BuildingOperations();
            try
            {
                var source = map12Buildings.Buildings.Single(v => v.PlacementId == entity.PlacementId);
                var level = Map12BuildingLevel(source);
                var awards = map12Buildings.MountLiftRewardPreview(level.level, level.subRank);
                var result = progressionSave.ApplyInventoryTransaction(MountLiftCompletionTag, save =>
                {
                    foreach (var award in awards)
                        if (!OfflineInventoryOperations.Grant(save.Player, award.Key, award.Value, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                            throw new InvalidDataException("Original MountLift award could not be applied: " + award.Key);
                    save.World.CompletedEntityTags.Add(MountLiftCompletionTag);
                }, entity.EntityClassId, 1);
                if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidDataException(result.Message);
                SynchronizeMap12BuildingCompletions();
                if (result.IsApplied) stamina.ShowStaminaAward(awards);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                battleStepTimer = 2f;
                hud.ShowOfficialNotice("保存失败，请稍后重试");
                return false;
            }
        }
    }
}
