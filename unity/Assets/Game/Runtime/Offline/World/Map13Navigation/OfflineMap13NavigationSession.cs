using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13Navigation;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.Map13Navigation
{
    public sealed class OfflineMap13NavigationSession
    {
        private sealed class Handler : IAStarMovableHandler
        {
            private readonly OfficialMapGroundRules ground;
            public Handler(OfficialMapGroundRules ground) { this.ground = ground; }
            public bool IsFit(Pos2d position, IAStarMoveCustomContext context) =>
                context is DefaultAStarMoveCustomContext && ground.IsFit(Grid(position));
            public bool CanMoveToNext(Pos2d from, Pos2d to) => ground.CanMoveToNext(Grid(from), Grid(to));
        }

        private readonly Dictionary<int, Map13WaterRule> waterRules;
        private readonly Dictionary<int, OfficialMapCharacterMover> movers;
        public OfficialMapDefinition Definition { get; }
        public OfflineMap13WaterSession Water { get; }

        public OfflineMap13NavigationSession(OfficialMapDefinition definition, Map13WaterDefinition water,
            Map13NavigationAssets.MoverData moverData, Map13WaterState[] saved = null)
        {
            if (definition == null || definition.ClassId != 13 || definition.Navigation == null
                || definition.Navigation.Width != 240 || definition.Navigation.Height != 240
                || definition.Navigation.CharacterId != 3001 || definition.Navigation.MemberId != 3002
                || definition.CharacterMover == null || definition.CharacterMover.CharacterClassId != 3001
                || definition.CharacterMover.MoveMask != 65 || definition.Entities == null || definition.Entities.Length != 2690
                || moverData == null || moverData.MapClassId != 13 || moverData.Manifest != "871_156598")
                throw new InvalidDataException("Navigation data does not belong to selected Map13.");
            // Also checks the official ground-type bits against the stored mask.
            _ = new OfficialMapGroundRules(definition);
            Definition = definition;
            Water = new OfflineMap13WaterSession(water, saved);
            waterRules = water.Rules.ToDictionary(rule => rule.PlacementId);
            movers = moverData.Movers.ToDictionary(mover => mover.CharacterClassId);
            foreach (OfficialMapCharacterMover mover in movers.Values)
            {
                int mask = 0;
                foreach (int type in mover.WalkableGroundTypes)
                {
                    if (type < 0 || type >= 32) throw new InvalidDataException("Invalid original mover ground type.");
                    mask |= 1 << type;
                }
                if (mask != mover.MoveMask) throw new InvalidDataException("Original mover mask differs from its types.");
            }
            foreach (var rule in waterRules.Values.Where(rule => rule.Routes.Length > 0))
                if (!movers.ContainsKey(rule.EntityClassId)) throw new InvalidDataException("Missing floating platform mover.");
        }

        // The world host supplies ordinary spawned entities, their current body
        // overrides, cross-blocks and temporary blocks. It must omit the moving
        // entity identified by excludedId. No quest spawn policy is inferred here.
        public OfficialMapGroundRules CreateGround(int moveMask,
            Action<OfficialMapGroundRules, int> bindWorldOccupancy = null, int excludedId = 0)
        {
            var ground = new OfficialMapGroundRules(Definition.Navigation, moveMask);
            long overlayId = long.MinValue;
            foreach (Map13WaterRule rule in waterRules.Values)
            {
                var state = Water.State(rule.PlacementId);
                if (!state.Spawned || rule.PlacementId == excludedId) continue;
                foreach (var body in Water.Body(rule.PlacementId))
                    ground.AddOverlay(overlayId++, state.Grid + body.Pos, body.GroundType, body.OverrideGroundType);
            }
            bindWorldOccupancy?.Invoke(ground, excludedId);
            return ground;
        }

        public List<MapGridPoint> FindPlayerPath(OfficialMapPathRequest request,
            Action<OfficialMapGroundRules, int> bindWorldOccupancy = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.BodyOffsets == null) throw new ArgumentException("Original player body offsets are required.", nameof(request));
            var builder = new FindPathArgContext.Builder(Position(request.Start), Position(request.End))
                .WithPreferDist(request.PreferDistance).WithPreciseTarget(request.PreciseTarget)
                .WithMaxStep(Definition.Navigation.MaxSteps)
                .WithBodyRanges(request.BodyOffsets.Select(Position).ToList())
                .WithCustomContext(new DefaultAStarMoveCustomContext(Definition.CharacterMover.MoveMask));
            if (request.FirstSteps != null) builder.WithFirstStepPosList(request.FirstSteps.Select(Position).ToList());
            if (request.CanEnd != null) builder.WithCheckEndDel(position => request.CanEnd(Grid(position)));
            return AStarUtils.FindPath(new Handler(CreateGround(Definition.CharacterMover.MoveMask, bindWorldOccupancy)),
                builder.Build())?.Select(Grid).ToList();
        }

        public bool ContainsClickableWaterBody(int id, MapGridPoint point)
        {
            var state = Water.State(id);
            return state.Spawned && Water.Body(id).Any(body => !body.IgnoreClick && (state.Grid + body.Pos).Equals(point));
        }

        public bool TouchWater(int id, MapGridPoint actor, Map13WaterOccupant[] occupants,
            Action<OfficialMapGroundRules, int> bindWorldOccupancy = null)
        {
            var grounds = new Dictionary<int, OfficialMapGroundRules>();
            var startingBodies = new Dictionary<int, HashSet<MapGridPoint>>();
            return Water.Touch(id, actor, occupants, (platformId, target) =>
            {
                if (!grounds.TryGetValue(platformId, out var ground))
                {
                    var rule = waterRules[platformId];
                    ground = CreateGround(movers[rule.EntityClassId].MoveMask, bindWorldOccupancy, platformId);
                    grounds.Add(platformId, ground);
                    MapGridPoint anchor = Water.State(platformId).Grid;
                    startingBodies.Add(platformId, new HashSet<MapGridPoint>(Water.Body(platformId).Select(body => anchor + body.Pos)));
                }
                // Original GridMover.CanStand merges its existing RangeList into
                // the whitelist before testing the next body against the ground.
                return startingBodies[platformId].Contains(target) || ground.IsFit(target);
            });
        }

        private static Pos2d Position(MapGridPoint point) => new Pos2d(point.X, point.Y);
        private static MapGridPoint Grid(Pos2d point) => new MapGridPoint(point.X, point.Y);
    }
}
