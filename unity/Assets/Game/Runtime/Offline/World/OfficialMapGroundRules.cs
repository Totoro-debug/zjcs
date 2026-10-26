using System;
using System.Collections.Generic;
using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    public sealed class OfficialMapGroundRules : IOfficialMapMovementRules
    {
        private sealed class Overlay
        {
            public MapGridPoint Position;
            public int Mask;
            public bool Overrides;
        }

        private readonly OfficialMapNavigation navigation;
        private readonly int moveMask;
        private readonly Dictionary<long, Overlay> overlays = new Dictionary<long, Overlay>();
        private readonly Dictionary<MapGridPoint, int> extraBlocked = new Dictionary<MapGridPoint, int>();
        private readonly HashSet<DirectedEdge> crossBlocked = new HashSet<DirectedEdge>();

        private readonly struct DirectedEdge : IEquatable<DirectedEdge>
        {
            private readonly MapGridPoint from;
            private readonly MapGridPoint to;

            public DirectedEdge(MapGridPoint from, MapGridPoint to)
            {
                this.from = from;
                this.to = to;
            }

            public bool Equals(DirectedEdge other) => from.Equals(other.from) && to.Equals(other.to);
            public override bool Equals(object value) => value is DirectedEdge other && Equals(other);
            public override int GetHashCode() => 31 * from.GetHashCode() + to.GetHashCode();
        }

        public OfficialMapGroundRules(OfficialMapNavigation navigation, int moveMask)
        {
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.moveMask = moveMask;
        }

        public OfficialMapGroundRules(OfficialMapDefinition definition)
            : this(GetNavigation(definition), GetMoveMask(definition))
        {
        }

        public void AddOverlay(long id, MapGridPoint position, int groundType, bool overridesGroundType)
        {
            if (overlays.ContainsKey(id))
                throw new ArgumentException("Ground overlay identity is already present.", nameof(id));
            overlays.Add(id, new Overlay { Position = position, Mask = 1 << groundType, Overrides = overridesGroundType });
        }

        public bool RemoveOverlay(long id)
        {
            return overlays.Remove(id);
        }

        public void AddExtraBlocked(MapGridPoint position)
        {
            extraBlocked.TryGetValue(position, out int references);
            extraBlocked[position] = references + 1;
        }

        public void RemoveExtraBlocked(MapGridPoint position)
        {
            if (!extraBlocked.TryGetValue(position, out int references)) return;
            if (references <= 1) extraBlocked.Remove(position);
            else extraBlocked[position] = references - 1;
        }

        public void AddCrossBlocked(MapGridPoint from, MapGridPoint to)
        {
            crossBlocked.Add(new DirectedEdge(from, to));
        }

        public void RemoveCrossBlocked(MapGridPoint from, MapGridPoint to)
        {
            crossBlocked.Remove(new DirectedEdge(from, to));
        }

        public bool IsFit(MapGridPoint position)
        {
            if (!OfficialMapNavigationView.TryGetCell(navigation, position.X, position.Y, out int _, out int groundType))
                return false;

            int groundMask = 1 << groundType;
            int overrideMask = 0;
            foreach (Overlay overlay in overlays.Values)
            {
                if (!overlay.Position.Equals(position)) continue;
                if (overlay.Overrides) overrideMask |= overlay.Mask;
                else groundMask |= overlay.Mask;
            }

            if (extraBlocked.ContainsKey(position)) return false;
            int effectiveMask = overrideMask > 0 ? overrideMask : groundMask;
            return (~moveMask & effectiveMask) == 0;
        }

        public bool CanMoveToNext(MapGridPoint from, MapGridPoint to)
        {
            return !crossBlocked.Contains(new DirectedEdge(from, to));
        }

        private static OfficialMapNavigation GetNavigation(OfficialMapDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return definition.Navigation ?? throw new ArgumentException("Official map navigation is missing.", nameof(definition));
        }

        private static int GetMoveMask(OfficialMapDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            OfficialMapCharacterMover mover = definition.CharacterMover;
            if (mover == null || mover.WalkableGroundTypes == null)
                throw new ArgumentException("Official character movement data is missing.", nameof(definition));

            int mask = 0;
            foreach (int groundType in mover.WalkableGroundTypes)
            {
                if (groundType < 0 || groundType >= 32)
                    throw new ArgumentException("Official character movement data has an invalid ground type.", nameof(definition));
                mask |= 1 << groundType;
            }

            if (mask != mover.MoveMask)
                throw new ArgumentException("Official character movement mask differs from its ground types.", nameof(definition));
            return mask;
        }
    }
}
