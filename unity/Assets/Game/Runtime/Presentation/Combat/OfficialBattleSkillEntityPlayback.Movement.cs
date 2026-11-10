using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSkillEntityPlayback
    {
        private readonly OfficialBattleMoveActionPlayback moveViews;
        private int movementSerial;

        public bool OwnsMovement(FightBattleMovementEvent movement) => movement != null
            && movement.HasDirection && movement.HasFacing && movement.Path != null
            && movement.Path.Count > 1 && moveViews.ContainsClass(movement.MoveBehaviourId);

        public void StartMovement(FightBattleMovementEvent movement, Transform source)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillEntityPlayback));
            if (!OwnsMovement(movement))
                throw new ArgumentException("A bound native action and settled direction/facing snapshot are required.", nameof(movement));
            var lease = BindUnit(movement.UnitId, source);
            var end = movement.Path[movement.Path.Count - 1];
            lease.Projection.Position = new Pos2d(end.X, end.Y);
            lease.Projection.Mirrored = movement.EndFlipX;
            moveViews.Create(MovementData(movement, new EcEntityIdentifier(5, ++movementSerial)));
        }

        internal float MovementDuration(FightBattleMovementEvent movement) =>
            MovementData(movement, default).Movement.TotalMoveDuration;

        private OfficialBattleMoveActionData MovementData(FightBattleMovementEvent movement,
            EcEntityIdentifier identity)
        {
            var path = new List<Pos2d>();
            for (int index = 1; index < movement.Path.Count; index++)
                path.Add(new Pos2d(movement.Path[index].X, movement.Path[index].Y));
            var start = movement.Path[0];
            return new OfficialBattleMoveActionData(combatCatalog, movement.MoveBehaviourId,
                identity, UnitId(movement.UnitId),
                new Pos2d(start.X, start.Y), path, Facing(movement.Direction));
        }
    }
}
