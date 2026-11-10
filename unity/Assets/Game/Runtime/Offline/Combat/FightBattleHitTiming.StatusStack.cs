using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Zjcs.Combat.Official
{
    public static partial class FightBattleHitTiming
    {
        // Per-hit resolved inputs; never write stack overrides into a shared hit definition.
        public sealed class Flight
        {
            internal readonly FightBattleHitTrajectory Trajectory;
            public int OverrideIndex { get; }
            public float DelayDistFactor { get; }

            internal Flight(int index, float factor, FightBattleHitTrajectory trajectory)
            { OverrideIndex = index; DelayDistFactor = factor; Trajectory = trajectory; }
        }

        /// <summary>Original MyFightHitBase.UpdateDelayInfo over this cast's frozen source statuses.</summary>
        public static Flight ResolveOverride(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition skill, FightBattleUnitState source,
            FightBattleSkillCastPresentation cast, FightBattleHitDefinition hit)
        {
            if (skill == null || source == null || cast == null || hit == null)
                throw new ArgumentNullException();
            if (catalog == null) return null;
            int sourceClassId = source.RoleEntityId > 0 ? source.RoleEntityId
                : source.CharacterType == 1 ? 3100
                : source.MonsterTemplateId > 0 ? source.MonsterTemplateId : source.MonsterId;
            // Original GetSourceStatusAgent requires both components on the actual source.
            if (!catalog.HasComponent(sourceClassId, "FightStatusAgentComponent")
                || !catalog.HasComponent(sourceClassId, "FightSkillAgentComponent")
                || !catalog.TryGetComponent(skill.EntityId,
                    "FightHitExtDelayInfoOverriderByStatusStackComponent", out var data)) return null;
            int index = OverrideIndex(cast, Convert.ToInt32(data["StatusId"], CultureInfo.InvariantCulture));
            if (index < 0) return null;
            var value = hit.Trajectory ?? new FightBattleHitTrajectory();
            var trajectory = new FightBattleHitTrajectory {
                OffsetX = value.OffsetX, OffsetY = value.OffsetY, OffsetZ = value.OffsetZ,
                TargetOffsetX = value.TargetOffsetX, TargetOffsetY = value.TargetOffsetY, TargetOffsetZ = value.TargetOffsetZ,
                RotateCenterOffsetX = value.RotateCenterOffsetX, RotateCenterOffsetY = value.RotateCenterOffsetY,
                RotateCenterOffsetZ = value.RotateCenterOffsetZ, IsUseCenterRotation = value.IsUseCenterRotation };
            float factor = hit.DelayDistFactor;
            if (Entry(data, "Offsets", index, out object offset))
                Vector(offset, out trajectory.OffsetX, out trajectory.OffsetY, out trajectory.OffsetZ);
            if (Entry(data, "TargetOffsets", index, out object target))
                Vector(target, out trajectory.TargetOffsetX, out trajectory.TargetOffsetY, out trajectory.TargetOffsetZ);
            if (Entry(data, "RotationCenterOffsets", index, out object center))
                Vector(center, out trajectory.RotateCenterOffsetX, out trajectory.RotateCenterOffsetY, out trajectory.RotateCenterOffsetZ);
            if (Entry(data, "DelayDistFactors", index, out object distance))
                factor = Convert.ToSingle(distance, CultureInfo.InvariantCulture);
            if (Entry(data, "IsUseCenterRotations", index, out object rotation))
                trajectory.IsUseCenterRotation = Convert.ToBoolean(rotation, CultureInfo.InvariantCulture);
            return new Flight(index, factor, trajectory);
        }

        private static int OverrideIndex(FightBattleSkillCastPresentation cast, int statusId)
        {
            // MyFightHitExtDelayInfoOverriderByStatusStack.GetStatusCount: shared entry
            // wins even when zero; otherwise the first matching applied status wins.
            foreach (var shared in cast.SourceSharedStacks)
                if (shared.ShareId == statusId) return shared.StackCount - 1;
            foreach (var status in cast.SourceStatuses)
                if (status.ClassId == statusId) return status.StackCount - 1;
            return -1;
        }

        private static bool Entry(IReadOnlyDictionary<string, object> data, string key, int index,
            out object value)
        {
            if (!(data[key] is IList values))
                throw new InvalidOperationException("Original hit delay override is not a list: " + key);
            value = index < values.Count ? values[index] : null;
            return index < values.Count;
        }

        private static void Vector(object value, out float x, out float y, out float z)
        {
            if (!(value is IList xyz) || xyz.Count != 3)
                throw new InvalidOperationException("Original hit delay override offset must have three coordinates.");
            x = Convert.ToSingle(xyz[0], CultureInfo.InvariantCulture);
            y = Convert.ToSingle(xyz[1], CultureInfo.InvariantCulture);
            z = Convert.ToSingle(xyz[2], CultureInfo.InvariantCulture);
        }
    }
}
