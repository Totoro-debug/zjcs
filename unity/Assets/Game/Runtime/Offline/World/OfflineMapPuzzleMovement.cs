using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    public sealed partial class OfflineMapPuzzleSession
    {
        public MapGridPoint Position(int id) => new MapGridPoint(states[id].GridX, states[id].GridY);

        // StageResultEntityPushedComponent.GetTargetPosNextPosAndDir and
        // ECStageResultEntityPushedView.CanMoveToNextPos: cardinal adjacency,
        // GridMover.CanStand, then the inclusive Lo MovableRect. The player's
        // follow-through is the explicitly confirmed offline server adaptation.
        public bool TryPush(int id, MapGridPoint player, OfficialMapNavigation navigation,
            out MapGridPoint playerAfter)
        {
            playerAfter = player;
            MapPuzzleRule rule = rules[id];
            MapGridPoint box = Position(id);
            if (!rule.HasPushResult || !IsSpawned(id) || !InteractionEnabled(id)
                || Math.Abs(box.X - player.X) + Math.Abs(box.Y - player.Y) != 1) return false;
            var next = new MapGridPoint(box.X + box.X - player.X, box.Y + box.Y - player.Y);
            MapPuzzleRect rect = rule.MovableRect;
            if (rect != null && (next.X < rect.MinGrid.X || next.X > rect.MaxGrid.X
                || next.Y < rect.MinGrid.Y || next.Y > rect.MaxGrid.Y)) return false;
            OfficialMapGroundRules ground = CreateGround(navigation, rule.MoveMask);
            var oldBody = new HashSet<MapGridPoint>(rule.Body.Select(body => BodyPosition(rule, box, body)));
            // Common.GridGroundComponent.CanStand checks Info.GetBodyRange at the
            // prospective anchor, whitelisting the entity's existing RangeList.
            foreach (MapPuzzleBody body in rule.Body)
            {
                var target = new MapGridPoint(next.X + body.Pos.X, next.Y + body.Pos.Y);
                if (!oldBody.Contains(target) && !ground.IsFit(target)) return false;
            }
            var sub = rule.Stage.StateInfos[State(id)].SubStateList[0];
            if (sub.Behaviour.CompType != 2 || sub.Conditions.Count != 0
                || sub.Results.Count != 1 || sub.Results[0].CompType != 219 || rule.Stage.AutoNextState)
                throw new InvalidDataException("Unexpected original pushed-box stage: " + id);
            states[id].GridX = next.X;
            states[id].GridY = next.Y;
            playerAfter = box;
            RefreshLocks();
            return true;
        }

        public OfficialMapGroundRules CreateGround(OfficialMapNavigation navigation, int moveMask)
        {
            var ground = new OfficialMapGroundRules(navigation, moveMask);
            long overlayId = 0;
            foreach (MapPuzzleRule rule in rules.Values)
            {
                if (!IsSpawned(rule.PlacementId)) continue;
                foreach (MapPuzzleBody body in rule.Body)
                    ground.AddOverlay(++overlayId, BodyPosition(rule, Position(rule.PlacementId), body),
                        body.GroundType, body.OverrideGroundType);
            }
            return ground;
        }

        private bool PushBoxComplete(MapPuzzleRule rule)
        {
            // Confirmed offline completion predicate. This only opens the lock;
            // RefreshLocks executes the original spawn/state-update results.
            return rule.PushBoxDetectorIds.All(detector => IsSpawned(detector)
                && rule.PushBoxIds.Any(box => IsSpawned(box) && Position(box).Equals(Position(detector))));
        }

        // Common.GridTransformComponent.CalcLocalToWorldDir / MyMath.Rotate.
        private static MapGridPoint BodyPosition(MapPuzzleRule rule, MapGridPoint anchor, MapPuzzleBody body)
        {
            float x = body.Pos.X - rule.RealCenter[0], y = body.Pos.Y - rule.RealCenter[1];
            if (rule.InitialFlipX) x = -x;
            float rotatedX, rotatedY;
            switch (rule.InitialDirection)
            {
                case 0: rotatedX = x; rotatedY = y; break;
                case 1: rotatedX = -x; rotatedY = -y; break;
                case 2: rotatedX = -y; rotatedY = x; break;
                case 3: rotatedX = y; rotatedY = -x; break;
                default: throw new InvalidDataException("Unsupported official grid direction.");
            }
            return new MapGridPoint(anchor.X + (int)Math.Round(rotatedX + rule.RealCenter[0]),
                anchor.Y + (int)Math.Round(rotatedY + rule.RealCenter[1]));
        }
    }
}
