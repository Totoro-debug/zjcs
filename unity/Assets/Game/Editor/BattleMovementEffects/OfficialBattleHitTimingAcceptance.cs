using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor
{
    public static class OfficialBattleHitTimingAcceptance
    {
        private sealed class Ground : IFightSkillViewServices
        {
            public MyEntity GetEntity(EcEntityIdentifier id) => null;
            public Vector3 GridToPos(Pos2d p) => new Vector3(p.X, p.Y, 0f);
        }

        public static void RunIsolated()
        {
            var ground = new Ground();
            int count = 0;
            for (int seed = 0; seed < 64; seed++)
            for (int flip = 0; flip < 2; flip++)
            for (int rotation = 0; rotation < 2; rotation++)
            {
                var info = new FightHitViewFormula.SkillHitBaseDelayInfoProvider {
                    Offset = new Vector3(seed % 3 - 1f, seed % 5 * .2f, seed % 7 * -.1f),
                    TargetOffset = new Vector3(seed % 4 * -.3f, seed % 6 * .25f, seed % 3 * .1f),
                    RotateCenterOffset = new Vector3(seed % 5 * .2f, seed % 2 * -.4f, seed % 4 * .3f),
                    IsUseCenterRotation = rotation != 0, DelayDistFactor = seed % 5 * .07f };
                var cast = new FightBattleSkillCastPresentation {
                    SourceX = seed % 8, SourceY = seed % 9, FlipX = flip != 0 };
                var center = new FightGridPoint(seed % 11 - 3, seed % 13 - 4);
                var hit = new FightBattleHitDefinition { Delay = .37f, DelayDistFactor = info.DelayDistFactor,
                    Trajectory = new FightBattleHitTrajectory {
                        OffsetX = info.Offset.x, OffsetY = info.Offset.y, OffsetZ = info.Offset.z,
                        TargetOffsetX = info.TargetOffset.x, TargetOffsetY = info.TargetOffset.y, TargetOffsetZ = info.TargetOffset.z,
                        RotateCenterOffsetX = info.RotateCenterOffset.x, RotateCenterOffsetY = info.RotateCenterOffset.y,
                        RotateCenterOffsetZ = info.RotateCenterOffset.z, IsUseCenterRotation = info.IsUseCenterRotation } };
                float official = hit.Delay + FightHitViewFormula.CalcSkillHitDelay(info,
                    new Pos2d(cast.SourceX, cast.SourceY), new Pos2d(center.X, center.Y), ground, cast.FlipX);
                float actual = FightBattleHitTiming.EffectDelay(hit, cast, center);
                if (Math.Abs(official - actual) > .00001f)
                    throw new InvalidOperationException($"Hit timing differs from original: {seed}/{flip}/{rotation}: {actual}/{official}");
                count++;
            }
            Debug.Log($"Hit timing matches original CalcSkillHitDelay: {count} offset, Z, flip and rotation vectors.");
            OfficialBattlePublicMovementAcceptance.RunIsolated();
        }
    }
}
