using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineStatuesModule
        {
            public Map11RuntimeWitchResult[] witchResults = Array.Empty<Map11RuntimeWitchResult>();
            public Map11RuntimePeriodicityAward[] witchPeriodicAwards = Array.Empty<Map11RuntimePeriodicityAward>();
            public OfficialMap11WitchWindow witchDialog;
            private GameObject witchCanvas;
            private Action witchPreviewOpened, witchPreviewClosed, witchCloseRequested;
            public Map10RuntimeEntity witchSpawnPending;
            private bool witchSpawnAttempted;

            public void RecoverWitchStates()
            {
                if (host.mapClassId != 11) return;
                foreach (Map10RuntimeEntity entity in host.mapEntities.Values.Where(value => value.EntityClassId == 6620))
                {
                    int index = WitchActivationIndex(entity);
                    if (index < 0) continue;
                    Map11RuntimeWitchInfo info = witchInfos.Single(value => value.ActiveCount == index);
                    if (info.IsNextState || WitchSpawnDefeated(entity))
                    {
                        entity.State = 1;
                        host.entityView.SetEntityState(entity.PlacementId, 1);
                    }
                    else if (host.localSave.activeBattlePlacementId == entity.PlacementId)
                    {
                        witchSpawnPending = entity;
                        witchSpawnAttempted = false;
                    }
                }
            }

            private int WitchActivationIndex(Map10RuntimeEntity entity) => host.progressionSave.Save.World.ActivatedWitches
                .FindIndex(value => value.MapClassId == host.mapClassId && value.PlacementId == entity.PlacementId);

            public void InteractWitch(Map10RuntimeEntity entity)
            {
                host.progressionSave.Reload();
                int index = WitchActivationIndex(entity);
                if (index < 0)
                {
                    StartWitchStoryBook(entity);
                    Map10RuntimeMapTalkEntry talk = host.ResolveMapTalk(entity, 1);
                    if (talk != null && !string.IsNullOrEmpty(talk.TalkId) && host.stories.TryGetValue(talk.TalkId, out Map10RuntimeStory graph))
                    {
                        Map10RuntimeStoryNode start = FindStoryNode(graph, graph.entryNodeId);
                        int condition = StoryNext(start, "CanStart");
                        if (condition == 0 || host.EvaluateStoryBoolean(graph, FindStoryNode(graph, condition)))
                            host.StartStoryGraph(talk.TalkId, entity, null);
                    }
                    return;
                }
                Map11RuntimeWitchInfo info = witchInfos.Single(value => value.ActiveCount == index);
                if (!info.IsNextState && !WitchSpawnDefeated(entity))
                {
                    witchSpawnPending = entity;
                    witchSpawnAttempted = false;
                    return;
                }
                ShowWitchVisit(entity);
            }

            private OfficialMap11WitchWindow OpenWitchWindow(string key, int entityClassId = 6620)
            {
                host.NativeUi.Text("entity_6620_name"); // Load and validate the same pinned UI catalog.
                witchDialog?.Destroy();
                if (witchCanvas == null)
                {
                    witchCanvas = new GameObject("Map11WitchUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    witchCanvas.transform.SetParent(host.uiParent, false);
                    Canvas canvas = witchCanvas.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 120;
                    CanvasScaler scaler = witchCanvas.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1080, 1920);
                    scaler.matchWidthOrHeight = 0.5f;
                }
                witchDialog = new OfficialMap11WitchWindow(key, witchCanvas.transform, host.NativeUi.Data, host.MapProfile.NativeUiRoot, entityClassId);
                return witchDialog;
            }

            private Map11RuntimeWitchResult[] WitchEffects(Map11RuntimeStageResultRef[] refs) => refs
                .Select(reference => witchResults.Single(value => value.ComponentType == reference.ComponentType
                    && value.ComponentIndex == reference.ComponentIndex)).ToArray();

            private void StartWitchStoryBook(Map10RuntimeEntity entity)
            {
                Map11RuntimeWitchInfo info = witchInfos.Single(value => value.ActiveCount == host.progressionSave.ActiveWitchCount);
                OfficialMap11WitchWindow window = OpenWitchWindow("WorldWitchTouchDialog");
                // Reading ends by opening the result preview first, exactly as WorldWitchTouchUI.
                window.Book(info.StoryBookId, () => ShowWitchActivationPreview(entity, info));
                witchCloseRequested = () => ShowWitchActivationPreview(entity, info);
                window.Show();
            }

            public void WaitForWitchPreviewOpen(Action continuation) => witchPreviewOpened = continuation;
            public void WaitForWitchPreviewClose(Action continuation) => witchPreviewClosed = continuation;

            private void ShowWitchActivationPreview(Map10RuntimeEntity entity, Map11RuntimeWitchInfo info)
            {
                Map11RuntimeWitchResult[] effects = WitchEffects(info.StageResults);
                OfficialMap11WitchWindow window = OpenWitchWindow("WorldStageInteractionPreviewDialogWitch");
                Action close = () =>
                {
                    string receipt = "witch-activation:" + host.mapClassId + ":" + entity.PlacementId;
                    ProgressionResult result = host.progressionSave.ApplyWitchActivation(host.mapClassId, entity.PlacementId, receipt,
                        effects.Sum(value => value.HpPercentChange), WitchStatuses(effects), WitchAwards(effects),
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    RequireWitchResult(result);
                    CloseWitchWindow();
                    if (info.IsNextState)
                    {
                        entity.State = 1;
                        host.entityView.SetEntityState(entity.PlacementId, 1);
                    }
                    else
                    {
                        witchSpawnPending = entity;
                        witchSpawnAttempted = false;
                    }
                    host.regions.RefreshRegionProgress();
                    host.RefreshEntityActivity();
                    host.SaveLocalProgress();
                    Action continuation = witchPreviewClosed;
                    witchPreviewClosed = null;
                    continuation?.Invoke();
                };
                window.Preview("ui_witch_result_" + info.ActiveCount, "ui_witch_content_" + info.ActiveCount, effects, close);
                witchCloseRequested = close;
                window.Show();
                Action opened = witchPreviewOpened;
                witchPreviewOpened = null;
                opened?.Invoke();
            }

            private static int[] WitchStatuses(Map11RuntimeWitchResult[] effects) => effects
                .SelectMany(value => value.StatusEffects ?? Array.Empty<Map11RuntimeStatueStatus>())
                .SelectMany(value => value.FightStatusClassIds).Distinct().ToArray();

            private static OfflineItemStack[] WitchAwards(Map11RuntimeWitchResult[] effects) => effects
                .SelectMany(value => value.AwardItems ?? Array.Empty<Map10RuntimeAwardItem>())
                .GroupBy(value => value.ItemId).Select(group => new OfflineItemStack
                    { ItemId = group.Key, Count = group.Sum(value => value.Count) }).ToArray();

            private static void RequireWitchResult(ProgressionResult result)
            {
                if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged)
                    throw new InvalidOperationException(result.Message);
            }

            private void ShowWitchVisit(Map10RuntimeEntity entity)
            {
                OfficialMap11WitchWindow window = OpenWitchWindow("WorldTouchDialogWitchPeriodicityInteraction");
                OfficialMap11UiComponent source = window.Source("PeriodicityInteractionStageUI");
                bool available = !host.progressionSave.HasVisitedStatueOn(DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                window.State(source, available ? 1 : 0);
                window.SetText(source.fields.mPeriodicityInteractionTips, window.Text("map_periodicity_interaction_tips_1"));
                window.SetText(window.Source("StageDialog").fields.myStageName, window.Text("entity_6620_name"));
                window.SetText(window.Source("StageDialog").fields.myStageDesc, window.Text("entity_6620_desc"));
                window.EntityIcon();
                window.BindButton(source.fields.mInteractionBtn, () => VisitWitch(entity));
                witchCloseRequested = CloseWitchWindow;
                window.Show();
            }

            private void VisitWitch(Map10RuntimeEntity entity)
            {
                string date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                host.progressionSave.Reload();
                if (host.progressionSave.HasVisitedStatueOn(date)) { ShowWitchVisit(entity); return; }
                int weight = witchPeriodicAwards.Sum(value => value.Weight);
                int random = UnityEngine.Random.Range(0, weight);
                Map11RuntimePeriodicityAward chosen = null;
                foreach (Map11RuntimePeriodicityAward candidate in witchPeriodicAwards)
                {
                    random -= candidate.Weight;
                    if (random < 0) { chosen = candidate; break; }
                }
                if (chosen == null) throw new InvalidDataException("Official witch periodic weights are invalid.");
                Map11RuntimeWitchResult[] effects = WitchEffects(chosen.StageResults);
                ProgressionResult result = host.progressionSave.ApplyStatueVisit(date, "witch", host.mapClassId, entity.PlacementId,
                    "statue-visit:" + date, effects.Sum(value => value.HpPercentChange), WitchStatuses(effects), WitchAwards(effects),
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                RequireWitchResult(result);
                OpenWitchWindow("WorldStageInteractionPreparePlayableDialog").Show();
                witchCloseRequested = null;
                host.StartCoroutine(ShowWitchVisitResult(chosen, effects));
            }

            private IEnumerator ShowWitchVisitResult(Map11RuntimePeriodicityAward chosen, Map11RuntimeWitchResult[] effects)
            {
                // PeriodicityInteractionStageUI passes a null playable and the original ActionTime.
                if (chosen.ActionTime > 0) yield return new WaitForSeconds(chosen.ActionTime);
                if (host.closed || witchDialog == null) yield break;
                OfficialMap11WitchWindow preview = OpenWitchWindow("WorldStageInteractionPreviewDialogWitch");
                preview.Preview("map_periodicity_random_award_interaction_title_" + chosen.PeriodicityId,
                    "map_periodicity_random_award_interaction_desc_" + chosen.PeriodicityId, effects, CloseWitchWindow);
                witchCloseRequested = CloseWitchWindow;
                preview.Show();
            }

            private bool WitchSpawnDefeated(Map10RuntimeEntity entity) => host.localSave.battleResults.Any(value =>
                value.placementId == entity.PlacementId && value.stageId == 20001 && value.won);

            public bool TryStartWitchSpawnBattle()
            {
                if (witchSpawnPending == null || witchSpawnAttempted || host.storyRunning || witchDialog != null) return false;
                witchSpawnAttempted = true;
                Map10RuntimeEntity entity = witchSpawnPending;
                Map11RuntimeWitchInfo info = witchInfos.Single(value => value.ActiveCount == WitchActivationIndex(entity));
                Map11RuntimeWitchResult spawn = WitchEffects(info.StageResults).Single(value => value.ComponentType == 245);
                if (spawn.SpawnEntityClassId != 5100 || spawn.AutoInteractionSubStateIndex != 0)
                    throw new InvalidDataException("Unsupported official witch summon configuration.");
                Map10RuntimeStage stage = host.stages[spawn.SpawnStageId];
                host.BeginBattle(entity, stage, host.rosters[stage.Id], host.battleScenes[stage.GameId], null,
                    host.TryLoadActiveBattle(stage.Id, entity.PlacementId));
                return true;
            }

            public void RequestWitchDialogClose() => witchCloseRequested?.Invoke();
            private void CloseWitchWindow()
            {
                witchCloseRequested = null;
                witchDialog?.Destroy();
                witchDialog = null;
                if (witchCanvas != null) Destroy(witchCanvas);
                witchCanvas = null;
            }

            public void CloseWitchDialog()
            {
                witchPreviewOpened = witchPreviewClosed = null;
                CloseWitchWindow();
                witchSpawnPending = null;
            }
        }
    }
}
