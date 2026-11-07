using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.World;
using Entity = Zjcs.Presentation.World.OfficialMainlineMapRuntime.Map10RuntimeEntity;

namespace Zjcs.Presentation.World
{
    // Both a live world and a cold minimap read pass their restored entity state
    // through this projection. Rendering-only removal animations stay in the world.
    internal sealed class MainlineMapEntityPresentation
    {
        private readonly OfficialMapDefinition definition;
        private readonly OfficialMainlineMapProfile map;
        private readonly Map10LocalSave local;
        private readonly OfflineMainlineSave profile;
        private readonly IReadOnlyDictionary<int, Map10RuntimeLogicTag> tags;
        private readonly Func<int, bool> accessible;
        private readonly long now;

        internal MainlineMapEntityPresentation(OfficialMapDefinition definition, OfficialMainlineMapProfile map,
            Map10LocalSave local, OfflineMainlineSave profile,
            IReadOnlyDictionary<int, Map10RuntimeLogicTag> tags, Func<int, bool> accessible, long now)
        {
            this.definition = definition;
            this.map = map;
            this.local = local;
            this.profile = profile;
            this.tags = tags;
            this.accessible = accessible;
            this.now = now;
        }

        internal bool IsPublic(int region) => profile.World.ActivatedGoddessRegions
            .Any(value => value.MapClassId == map.MapClassId && value.Region == region);

        internal static string HeroReceipt(int mapClassId, int placement) => "hero-activation:" + mapClassId + ":" + placement;

        internal static bool HeroActivated(OfflineMainlineSave profile, int mapClassId, int placement) =>
            profile.Player.AppliedInventoryEventIds.Contains(HeroReceipt(mapClassId, placement));

        internal static int HeroState(OfflineMainlineSave profile, int mapClassId, int placement, bool isPublic) =>
            isPublic ? 2 : HeroActivated(profile, mapClassId, placement) ? 1 : 0;

        internal static bool IsDestroyed(int placement, IReadOnlyDictionary<int, Map10RuntimeLogicTag> tags,
            Map10LocalSave local, OfflineMainlineSave profile) => tags.TryGetValue(placement, out var tag)
                && (profile.World.CompletedEntityTags.Contains(tag.Tag)
                    || local.mapLogicStates.Any(value => value != null && value.Tag == tag.Tag && value.State < 0));

        internal Result Read(Entity entity, int state, bool spawned)
        {
            bool goddess = map.HasNativeUi && entity.TypeId == 202;
            if (tags.TryGetValue(entity.PlacementId, out var tag))
                state = local.mapLogicStates.FirstOrDefault(value => value != null && value.Tag == tag.Tag)?.State ?? state;
            if (map.MapClassId == 14 && local.completedRoadblockPlacements.Contains(entity.PlacementId)
                && OfficialMainlineRoadblockRules.Find(entity.PlacementId) != null)
                state = 1;
            if (goddess && IsPublic(entity.Region)) state = 1;
            if (entity.TypeId == 223)
                state = HeroState(profile, map.MapClassId, entity.PlacementId, IsPublic(entity.Region));
            if (entity.EntityClassId == 5011 || entity.EntityClassId == 8011
                || entity.EntityClassId == 8021 || entity.EntityClassId == 8026)
            {
                var placement = definition.Entities.Single(value => value.Id == entity.PlacementId);
                bool[] flags = IsPublic(entity.Region) ? placement.Public : placement.Private;
                spawned = flags != null && flags.Length > 0 && flags[0]
                    && OfflineMapStaminaService.Available(profile, map.MapClassId, entity.PlacementId, now);
            }
            bool stele = map.MapClassId == 12 && entity.TypeId == 218
                && (entity.EntityClassId == 6650 || entity.EntityClassId == 6655);
            return new Result(state, (spawned || goddess) && accessible(entity.Region)
                && (entity.TypeId == 223 || stele || !local.completedEntityPlacements.Contains(entity.PlacementId))
                && !IsDestroyed(entity.PlacementId, tags, local, profile));
        }

        internal readonly struct Result
        {
            internal readonly int State;
            internal readonly bool Active;
            internal Result(int state, bool active) { State = state; Active = active; }
        }
    }
}
