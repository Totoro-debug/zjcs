using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Official.World;
using static Zjcs.Offline.Progression.OfficialWorldExplorationCatalog;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfflineWorldExplorationService explorationService;
        private long explorationRefreshAt;
        private OfflineWorldExplorationService ExploreService => explorationService ??= new OfflineWorldExplorationService(progressionSave);
        private static long ExploreNow => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private OfflineExploreContext ExploreContext()
        {
            OfficialMapNavigationView.TryGetCell(session.Definition.Navigation, currentGrid.X, currentGrid.Y, out int area, out _);
            int region = regionConfigs.Values.SingleOrDefault(v => v.Areas.Contains(area))?.Region ?? -1;
            var nativeRegion = Interactions.regions.SingleOrDefault(v => v.MapClassId == mapClassId && v.Region == region);
            var context = new OfflineExploreContext { MapClassId = mapClassId, MapGroupId = session.Definition.MapGroupId,
                Area = area, Region = region, X = currentGrid.X, Y = currentGrid.Y, Level = progression.Player.Level,
                SubRank = progression.Player.SubRank, RegionLevel = nativeRegion?.Level ?? 0, RegionSubRank = nativeRegion?.SubRank ?? 0,
                DropItems = nativeRegion?.DropItems ?? Array.Empty<int>(), MapCanExplore = GetWorldInfo().CanExplore,
                RegionForbidsExplore = nativeRegion?.ForbidExplore ?? true, IsAlive = battleController == null,
                HasOrUsedItem = (save, id) => id == 4007 && save.Player.FruitTree.ProductionRepaired };
            context.IsActivityOpen = (type, id, ahead) => type == 49 && id == 220
                && ExploreService.IsMet(ExploreService.Catalog.Data.conditions.Single(v => v.ClassId == ExploreService.Catalog.Data.cooperativeActivity.OpenConditionId).Condition,
                    context, progressionSave.Save);
            context.FindSpawn = (entity, distance, active) => FindExploreSpawn(entity, distance, area, active);
            context.IsGroupCandidate = (group, save) => ExploreService.HasGroupRewardCandidates(group, context, save);
            context.ResolveRandomItem = (item, count, save) => ExploreService.ResolvePackage(item, count, context, save);
            return context;
        }
        private MapGridPoint[] ExploreBody(int entity)
        {
            var grid = Object(ExploreService.Catalog.Components(entity)["GridTransformComponent"]);
            return Values(grid, "BodyRangeInfos").Select(Object).Select(v => Object(v["Pos"]))
                .Select(v => new MapGridPoint(Int(v, "X"), Int(v, "Y"))).ToArray();
        }
        private OfflineExplorePoint? FindExploreSpawn(int entity, int distance, int area, IReadOnlyList<OfflineExploreEntity> active)
        {
            var spawner = ExploreService.Catalog.Data.spawners.Single(v => v.mapClassId == mapClassId);
            var occupied = new HashSet<MapGridPoint> { currentGrid };
            foreach (var value in mapEntities.Values.Where(v => v.PlacementId < 1000000000 && !spawner.EnableSpawnEntityClassIds.Contains(v.EntityClassId)))
                if (entityView.FindEntityObject(value.PlacementId)?.activeSelf == true) occupied.Add(value.Grid);
            foreach (var value in active.Where(v => v.MapClassId == mapClassId))
                foreach (var offset in ExploreBody(value.EntityClassId)) occupied.Add(new MapGridPoint(value.X + offset.X, value.Y + offset.Y));
            var body = ExploreBody(entity);
            var points = new List<MapGridPoint>();
            for (int y = currentGrid.Y - distance; y <= currentGrid.Y + distance; y++)
                for (int x = currentGrid.X - distance; x <= currentGrid.X + distance; x++)
                    if ((x - currentGrid.X) * (x - currentGrid.X) + (y - currentGrid.Y) * (y - currentGrid.Y) <= distance * distance)
                        points.Add(new MapGridPoint(x, y));
            foreach (var point in points.OrderBy(v => (v.X-currentGrid.X)*(v.X-currentGrid.X)+(v.Y-currentGrid.Y)*(v.Y-currentGrid.Y)).ThenBy(v => v.Y).ThenBy(v => v.X))
            {
                bool fits = body.All(offset =>
                {
                    var cell = new MapGridPoint(point.X + offset.X, point.Y + offset.Y);
                    return !occupied.Contains(cell) && OfficialMapNavigationView.TryGetCell(session.Definition.Navigation, cell.X, cell.Y, out int candidateArea, out int ground)
                        && candidateArea == area && spawner.EnableSpawnGroundTypes.Contains(ground) && session.Movement.IsFit(cell);
                });
                if (fits) return new OfflineExplorePoint(point.X, point.Y);
            }
            return null;
        }
        private void SynchronizeExploreEntities()
        {
            if (mapClassId != 11 || entityView == null) return;
            var active = progressionSave.Save.World.Exploration.Entities.Where(v => v.MapClassId == mapClassId && v.Active(ExploreNow)).ToArray();
            foreach (int id in mapEntities.Keys.Where(v => v >= 1000000000 && active.All(e => e.PlacementId != v)).ToArray())
            {
                entityView.RemoveRuntimeEntity(id); mapEntities.Remove(id);
            }
            foreach (var entity in active)
            {
                if (mapEntities.TryGetValue(entity.PlacementId, out var existing)) { existing.State = entity.State; continue; }
                var template = ExploreService.Catalog.Data.entities.Single(v => v.entityClassId == entity.EntityClassId);
                if (!entityTemplates.ContainsKey(entity.EntityClassId)) entityTemplates.Add(entity.EntityClassId, new Map10RuntimeEntityTemplate
                {
                    EntityClassId = entity.EntityClassId,
                    Components = template.templateComponents.Select(v => new Map10RuntimeTemplateComponent { Type = v.Type, Json = v.Json }).ToArray()
                });
                var record = new OfficialMap10EntityView.EntityRecord { placementId = entity.PlacementId, entityClassId = entity.EntityClassId,
                    typeId = template.parameters.WorldStageType, grid = new[] { entity.X, entity.Y }, region = entity.Region, area = entity.Area,
                    state = entity.State, active = true, modelPath = template.displayPaths.Single(v => v.mapClassId == mapClassId).path };
                mapEntities.Add(entity.PlacementId, new Map10RuntimeEntity(entityView.SpawnRuntimeEntity(record)));
            }
        }
        private void ExecuteExplore(int times, int token)
        {
            var result = ExploreService.Explore(ExploreContext(), times, token, Guid.NewGuid().ToString("N"), ExploreNow, out var generated);
            if (!result.IsApplied)
            {
                if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidOperationException(result.Message);
                if (!string.IsNullOrEmpty(result.Message)) ShowOfficialNotice(goddessUiData.texts.FirstOrDefault(v => v.key == result.Message)?.value ?? result.Message);
                return;
            }
            SynchronizeExploreEntities(); RefreshEntityActivity(); RefreshWorldTopBar(); RefreshExploreHud();
            if (generated.GuideRewardGranted) ShowStaminaAward(new Dictionary<int, long> { [56] = 1 });
        }
        private bool InteractExploreEntity(Map10RuntimeEntity entity)
        {
            if (entity.PlacementId < 1000000000) return false;
            var result = ExploreService.CollectEntity(entity.PlacementId - 1000000000, ExploreContext(), ExploreNow, out var awards);
            if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidOperationException(result.Message);
            if (result.IsApplied) { SynchronizeExploreEntities(); RefreshEntityActivity(); RefreshWorldTopBar(); ShowStaminaAward(awards); }
            return true;
        }
        private void RefreshExploration()
        {
            if (mapClassId != 11 || ui == null || ExploreNow <= explorationRefreshAt) return;
            explorationRefreshAt = ExploreNow;
            if (ExploreService.Cleanup(ExploreNow).IsApplied) { SynchronizeExploreEntities(); RefreshEntityActivity(); }
            RefreshExploreHud();
        }
        private void RefreshExploreHud()
        {
            if (mapClassId != 11 || goddessUiData == null || ui == null) return;
            bool visible = ExploreService.CanExplore(ExploreContext());
            foreach (var binding in goddessUiData.worldBindings.exploreButtons)
            {
                var source = goddessUiData.components.Single(v => v.panel == "WorldCanvas" && v.type == "WorldExploreBtn" && v.path == binding.path);
                bool show = visible && (binding.isOneTime || ExploreService.MaxTimes(ExploreNow) > 1);
                ui.Find("WorldCanvas", source.fields.mCostMoneyContent).SetActive(show);
                ui.Find("WorldCanvas", source.fields.mLayoutElement).GetComponent<LayoutElement>().ignoreLayout = !show;
                var buttonFields = ExploreFields("WorldCanvas", "PlayerCostMoneyContent", source.fields.mCostMoneyContent);
                string text = GoddessText(binding.isOneTime ? "ui_WorldCanvas_explore" : "ui_WorldCanvas_explore_moretimes");
                ui.Find("WorldCanvas", Text(buttonFields, "BtnText")).GetComponent<TMP_Text>().text = binding.isOneTime ? text
                    : string.Format(System.Globalization.CultureInfo.CurrentCulture, text, ExploreService.MaxTimes(ExploreNow));
                var button = ui.Find("WorldCanvas", Text(buttonFields, "Btn")).GetComponent<Button>();
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => ShowExploreConfirm(binding.isOneTime ? 1 : ExploreService.MaxTimes(ExploreNow)));
            }
        }
    }
}
