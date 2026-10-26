using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfflineMapStaminaService staminaService;
        private long staminaRefreshAt;
        private OfficialMapAwardList mapAwardList;
        private OfflineMapStaminaService Stamina => staminaService ??= new OfflineMapStaminaService(progressionSave);
        private static bool IsStaminaPoint(Map10RuntimeEntity entity) => entity.EntityClassId == 8011
            || entity.EntityClassId == 8021 || entity.EntityClassId == 8026 || entity.EntityClassId == 5011;
        private OfficialMapEntityPlacement StaminaPlacement(Map10RuntimeEntity entity) =>
            session.Definition.Entities.Single(value => value.Id == entity.PlacementId);
        private Dictionary<int, long> StaminaCosts(Map10RuntimeEntity entity)
        {
            var costs = Interactions.parameters.Single(value => value.EntityClassId == entity.EntityClassId).CostItems;
            // StageUtils.GetStageCost uses the current behaviour's CompIndex (0
            // for all four verified templates) and removes empty cost entries.
            return costs[0].CostItems.Where(value => value.ItemId > 0 && value.Amount > 0)
                .ToDictionary(value => value.ItemId, value => value.Amount);
        }
        private long StaminaNextRefresh(Map10RuntimeEntity entity, long now)
        {
            var placement = StaminaPlacement(entity);
            var area = Interactions.areas.Single(value => value.ClassId == mapClassId && value.Area == placement.Area);
            int index = Array.IndexOf(area.RefreshGroups, placement.Refresh);
            if (index < 0) throw new InvalidDataException("Official map refresh group has no area interval.");
            return checked(now + area.RefreshIntervals[index]);
        }
        private long StaminaAddition()
        {
            var props = progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).NonBattleProps;
            return props.TryGetValue(29, out long value) ? value : 0;
        }
        private bool StaminaCanStart(Map10RuntimeEntity entity)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            progressionSave.Reload();
            if (!Stamina.Available(mapClassId, entity.PlacementId, now)) return false;
            if (!string.IsNullOrEmpty(Stamina.State(mapClassId, entity.PlacementId)?.BattleEventId)) return true;
            if (StaminaCosts(entity).Any(value => progressionSave.InventoryCount(value.Key) < value.Value))
            {
                ShowOfficialNotice(string.Format(CultureInfo.CurrentCulture,
                    GoddessText("ui_common_item_not_enough"), GoddessText("item_57_name")));
                return false;
            }
            return true;
        }
        private void CollectStaminaPoint(Map10RuntimeEntity entity)
        {
            if (!StaminaCanStart(entity)) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var result = Stamina.Collect(mapClassId, session.Definition.MapGroupId, entity.PlacementId, entity.EntityClassId,
                StaminaCosts(entity), progression.Player.Level, progression.Player.SubRank, StaminaAddition(), now,
                StaminaNextRefresh(entity, now), out var awards);
            if (!result.IsApplied)
            {
                if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidOperationException(result.Message);
                return;
            }
            RefreshEntityActivity();
            RefreshWorldTopBar();
            ShowStaminaAward(awards);
        }
        private void ShowStaminaAward(IReadOnlyDictionary<int, long> awards)
        {
            if (awards.Count == 0) return;
            GoddessText("ui_award_list_tip");
            if (mapAwardList == null) mapAwardList = OfficialMapAwardList.Create(uiParent, goddessUiData, MapProfile.NativeUiRoot);
            mapAwardList.Enqueue(awards);
        }
        private void FinishStaminaBattle(Map10RuntimeEntity entity, bool won)
        {
            if (entity == null || !IsStaminaPoint(entity)) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var result = Stamina.FinishBattle(mapClassId, session.Definition.MapGroupId, entity.PlacementId,
                entity.EntityClassId, won, progression.Player.Level, progression.Player.SubRank, StaminaAddition(),
                now, StaminaNextRefresh(entity, now), out var awards);
            if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidOperationException(result.Message);
            RefreshWorldTopBar();
            if (result.IsApplied && won) ShowStaminaAward(awards);
            RefreshEntityActivity();
        }
        private void RefreshStaminaPoints()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now <= staminaRefreshAt || entityView == null) return;
            staminaRefreshAt = now;
            if (mapEntities.Values.Any(IsStaminaPoint)) RefreshEntityActivity();
        }
    }
}
