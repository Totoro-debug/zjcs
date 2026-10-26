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
        private sealed partial class OfficialMainlineStaminaModule
        {
            private OfflineMapStaminaService staminaService;
            private long staminaRefreshAt;
            private OfficialMapAwardList mapAwardList;
            public OfflineMapStaminaService Stamina => staminaService ??= new OfflineMapStaminaService(host.progressionSave);
            public static bool IsStaminaPoint(Map10RuntimeEntity entity) => entity.EntityClassId == 8011
                || entity.EntityClassId == 8021 || entity.EntityClassId == 8026 || entity.EntityClassId == 5011;
            public OfficialMapEntityPlacement StaminaPlacement(Map10RuntimeEntity entity) =>
                host.session.Definition.Entities.Single(value => value.Id == entity.PlacementId);
            public Dictionary<int, long> StaminaCosts(Map10RuntimeEntity entity)
            {
                var costs = host.Interactions.parameters.Single(value => value.EntityClassId == entity.EntityClassId).CostItems;
                // StageUtils.GetStageCost uses the current behaviour's CompIndex (0
                // for all four verified templates) and removes empty cost entries.
                return costs[0].CostItems.Where(value => value.ItemId > 0 && value.Amount > 0)
                    .ToDictionary(value => value.ItemId, value => value.Amount);
            }
            private long StaminaNextRefresh(Map10RuntimeEntity entity, long now)
            {
                var placement = StaminaPlacement(entity);
                var area = host.Interactions.areas.Single(value => value.ClassId == host.mapClassId && value.Area == placement.Area);
                int index = Array.IndexOf(area.RefreshGroups, placement.Refresh);
                if (index < 0) throw new InvalidDataException("Official map refresh group has no area interval.");
                return checked(now + area.RefreshIntervals[index]);
            }
            private long StaminaAddition()
            {
                var props = host.progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).NonBattleProps;
                return props.TryGetValue(29, out long value) ? value : 0;
            }
            public bool StaminaCanStart(Map10RuntimeEntity entity)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                host.progressionSave.Reload();
                if (!Stamina.Available(host.mapClassId, entity.PlacementId, now)) return false;
                if (!string.IsNullOrEmpty(Stamina.State(host.mapClassId, entity.PlacementId)?.BattleEventId)) return true;
                if (StaminaCosts(entity).Any(value => host.progressionSave.InventoryCount(value.Key) < value.Value))
                {
                    host.hud.ShowOfficialNotice(string.Format(CultureInfo.CurrentCulture,
                        host.NativeUi.Text("ui_common_item_not_enough"), host.NativeUi.Text("item_57_name")));
                    return false;
                }
                return true;
            }
            public void CollectStaminaPoint(Map10RuntimeEntity entity)
            {
                if (!StaminaCanStart(entity)) return;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var result = Stamina.Collect(host.mapClassId, host.session.Definition.MapGroupId, entity.PlacementId, entity.EntityClassId,
                    StaminaCosts(entity), host.progression.Player.Level, host.progression.Player.SubRank, StaminaAddition(), now,
                    StaminaNextRefresh(entity, now), out var awards);
                if (!result.IsApplied)
                {
                    if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidOperationException(result.Message);
                    return;
                }
                host.RefreshEntityActivity();
                host.hud.RefreshWorldTopBar();
                ShowStaminaAward(awards);
            }
            public void ShowStaminaAward(IReadOnlyDictionary<int, long> awards)
            {
                if (awards.Count == 0) return;
                host.NativeUi.Text("ui_award_list_tip");
                if (mapAwardList == null) mapAwardList = OfficialMapAwardList.Create(host.uiParent, host.NativeUi.Data, host.MapProfile.NativeUiRoot);
                mapAwardList.Enqueue(awards);
            }
            public void FinishStaminaBattle(Map10RuntimeEntity entity, bool won)
            {
                if (entity == null || !IsStaminaPoint(entity)) return;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var result = Stamina.FinishBattle(host.mapClassId, host.session.Definition.MapGroupId, entity.PlacementId,
                    entity.EntityClassId, won, host.progression.Player.Level, host.progression.Player.SubRank, StaminaAddition(),
                    now, StaminaNextRefresh(entity, now), out var awards);
                if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidOperationException(result.Message);
                host.hud.RefreshWorldTopBar();
                if (result.IsApplied && won) ShowStaminaAward(awards);
                host.RefreshEntityActivity();
            }
            public void RefreshStaminaPoints()
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now <= staminaRefreshAt || host.entityView == null) return;
                staminaRefreshAt = now;
                if (host.mapEntities.Values.Any(IsStaminaPoint)) host.RefreshEntityActivity();
            }
        }
    }
}
