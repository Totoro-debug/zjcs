using System;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;

namespace Zjcs.Presentation.Combat
{
    // Presentation-created entities share the battle entity-slot coordinates.
    // This port supplies the original MyPlayableTrack container conversion.
    public sealed class OfficialBattleEntityPositionContainer : MyContainer, IEntityContainerLocalPosition
    {
        public Transform EntitySlot { get; set; }
        public Vector3 ToLocalPos(Vector3 position) => EntitySlot.InverseTransformPoint(position);
        public override ILogger Logger => Debug.unityLogger;
        public override IECViewMapRootLifetime Container => throw new NotSupportedException("Battle presentation does not own shared ECS lifetime.");
        public override MyEntity MyRootEntity => throw new NotSupportedException("Battle presentation does not create a shared ECS root.");
    }
}
