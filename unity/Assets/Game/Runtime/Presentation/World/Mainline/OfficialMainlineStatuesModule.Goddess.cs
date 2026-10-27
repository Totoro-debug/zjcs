using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineStatuesModule
        {
            public GoddessTouchDialog goddessDialog;
            private GameObject goddessCanvas;

            public bool IsGoddessActivated(Map10RuntimeEntity entity) =>
                host.progressionSave.Save.World.ActivatedGoddessRegions.Any(value =>
                    value.MapClassId == host.mapClassId && value.Region == entity.Region);

            private bool IsGoddessRegionFull(Map10RuntimeEntity entity) =>
                host.progressionSave.Save.World.Maps.First(value => value.MapClassId == host.mapClassId)
                    .Regions.First(value => value.Region == entity.Region).Progress >= 10000;

            public void ShowGoddessDialog(Map10RuntimeEntity entity)
            {
                if (goddessDialog != null) return;
                bool lost = entity.EntityClassId == 6011;
                string key = lost ? "LostGoddessTouchDialog" : "GoddessTouchDialog";
                GameObject prefab = Resources.Load<GameObject>(host.NativeUiResource("") + key);
                if (prefab == null) throw new InvalidDataException("Official goddess prefab is missing: " + key);
                goddessCanvas = new GameObject("Map11GoddessUI", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                goddessCanvas.transform.SetParent(host.uiParent, false);
                Canvas canvas = goddessCanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 120;
                CanvasScaler scaler = goddessCanvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                goddessDialog = Instantiate(prefab, goddessCanvas.transform, false).GetComponent<GoddessTouchDialog>();
                OfflineRegionProgress region = host.progressionSave.Save.World.Maps.First(value => value.MapClassId == host.mapClassId)
                    .Regions.First(value => value.Region == entity.Region);
                bool full = IsGoddessRegionFull(entity);
                // RoadblockUtils.GetTextColor reads these exact GlobalColors fields.
                host.NativeUi.Text("entity_" + entity.EntityClassId + "_name");
                float[] rgba = full ? host.NativeUi.Data.enoughColor : host.NativeUi.Data.lackColor;
                string progressColor = ColorUtility.ToHtmlStringRGB(new Color(rgba[0], rgba[1], rgba[2], rgba[3]));
                string condition = string.Format(CultureInfo.InvariantCulture, host.NativeUi.Text("ui_goddess_region_progress_100_text"),
                    host.NativeUi.Text("region_" + host.mapClassId + "_" + entity.Region), IsGoddessActivated(entity) ? string.Empty
                        : string.Format(CultureInfo.InvariantCulture, host.NativeUi.Text("suit_tip_with_parenthesis"),
                            "<color=#" + progressColor + ">" + region.Progress / 100 + "%</color>"));
                Dictionary<string, object> settings = host.progression.Catalog.Rows("game_settings").First();
                string blessing = string.Format(CultureInfo.InvariantCulture, host.NativeUi.Text("ui_GoddessTouchDialog_level_text"),
                    Convert.ToInt32(settings["GoddessToBlessingLevel"], CultureInfo.InvariantCulture));
                string[] props = lost ? new[]
                {
                    host.NativeUi.Text("PropType.HangUpExpAddPercent") + "<color=#E5A23D>+1%</color>",
                    host.NativeUi.Text("PropType.HangUpMoneyAddPercent") + "<color=#E5A23D>+1%</color>"
                } : Array.Empty<string>();
                goddessDialog.DoInit(IsGoddessActivated(entity), full, host.NativeUi.Text("entity_" + entity.EntityClassId + "_name"),
                    condition, blessing, props, () => ActivateGoddess(entity), () => TransferToGoddess(entity),
                    () => host.hud.ShowOfficialNotice(host.NativeUi.Text("STAGE_CONDITION_REGION_PROGRESS_NOT_FULL")), CloseGoddessDialog);
            }

            private void ActivateGoddess(Map10RuntimeEntity entity)
            {
                if (IsGoddessActivated(entity)) return;
                Map10RuntimeEntityTemplate template = host.entityTemplates[entity.EntityClassId];
                OfficialWorldStageRule stage = JsonUtility.FromJson<OfficialWorldStageRule>(
                    template.Components.Single(value => value.Type == "WorldStageComponent").Json);
                OfficialSubStageStateInfo subState = stage.StateInfos[entity.State].SubStateList[0];
                if (subState.Behaviour.CompType != 2 || subState.Behaviour.CompIndex != 0
                    || subState.Conditions.Any(value => value.CompType != 101 || value.CompIndex != 0))
                    throw new InvalidDataException("Official goddess touch-stage binding differs from its handler.");
                if (!IsGoddessRegionFull(entity))
                {
                    host.hud.ShowOfficialNotice(host.NativeUi.Text("STAGE_CONDITION_REGION_PROGRESS_NOT_FULL"));
                    return;
                }
                foreach (OfficialStageCompCfg result in subState.Results)
                {
                    if (result.CompType == 204 && result.CompIndex == 0)
                    {
                        ProgressionResult recorded = host.progressionSave.RecordGoddessActivated(host.mapClassId, entity.Region,
                            host.MapReceiptPrefix + host.slotId + ":goddess:" + entity.Region);
                        if (recorded.Status == ProgressionResultStatus.InvalidState)
                            throw new InvalidOperationException(recorded.Message);
                    }
                    else if (result.CompType == 209)
                    {
                        GoddessStateUpdate update = JsonUtility.FromJson<GoddessStateUpdate>(template.Components.Single(value =>
                            value.Type == "StageResultSelfStateUpdateComponent").Json);
                        entity.State = update.InfoList[result.CompIndex].State;
                        host.entityView.SetEntityState(entity.PlacementId, entity.State);
                    }
                    else throw new InvalidDataException("Unimplemented official goddess stage result: " + result.CompType);
                }
                host.RefreshEntityActivity();
                host.regions.regionFog?.Refresh(host.navigation.GridToWorld(entity.Grid));
                host.nativeMap12World?.UnlockRegion(host.navigation.GridToWorld(entity.Grid));
                host.SaveLocalProgress();
            }

            private void TransferToGoddess(Map10RuntimeEntity entity)
            {
                if (entity.EntityClassId == 6011 && Manhattan(host.navigation.currentGrid, entity.Grid) <= 1) return;
                // Confirmed offline rule: the original RPC contains only an entity id. Prefer its
                // own tile, otherwise select the nearest standable tile; ties use row then column.
                MapGridPoint target = entity.Grid;
                if (!host.session.Movement.IsFit(target))
                {
                    long nearest = long.MaxValue;
                    for (int y = 0; y < host.session.Definition.Navigation.Height; y++)
                    for (int x = 0; x < host.session.Definition.Navigation.Width; x++)
                    {
                        var point = new MapGridPoint(x, y);
                        long dx = x - entity.Grid.X, dy = y - entity.Grid.Y;
                        long distance = dx * dx + dy * dy;
                        if (distance >= nearest || !host.session.Movement.IsFit(point)) continue;
                        target = point;
                        nearest = distance;
                    }
                    if (nearest == long.MaxValue)
                        throw new InvalidDataException("Map11 has no standable goddess transfer destination.");
                }
                host.navigation.movementPath = null;
                host.navigation.pendingInteraction = null;
                host.navigation.movementCompletion = null;
                host.navigation.currentGrid = target;
                host.navigation.SetAvatarPosition(host.navigation.currentGrid);
                host.SaveLocalProgress();
            }

            public void CloseGoddessDialog()
            {
                goddessDialog = null;
                if (goddessCanvas != null) Destroy(goddessCanvas);
                goddessCanvas = null;
            }

            [Serializable] private sealed class GoddessStateUpdate { public GoddessState[] InfoList; }
            [Serializable] private sealed class GoddessState { public int State; }
        }
    }
}
