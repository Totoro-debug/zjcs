using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;

namespace Zjcs.Presentation.Combat
{
    // Projects only additional shared fields actually read by original skill
    // prefab views. Empty original client proxies consume no extra state.
    public sealed class OfficialBattleSkillPrefabData
    {
        private sealed class DelayOverride : FightHitExtDelayInfoOverriderByStatusStackComponent
        {
            public FightHitExtDelayInfoOverriderByStatusStackComponentInfo Configuration;
            public override FightHitExtDelayInfoOverriderByStatusStackComponentInfo Info => Configuration;
        }

        private readonly DelayOverride delay;

        public OfficialBattleSkillPrefabData(FightOfficialCombatCatalog catalog, int classId)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!catalog.ContainsEntity(classId))
                throw new InvalidOperationException("Original shared skill configuration is unavailable: " + classId);
            if (catalog.TryGetComponent(classId, "FightHitExtDelayInfoOverriderByStatusStackComponent", out var data))
                delay = new DelayOverride { Configuration = new FightHitExtDelayInfoOverriderByStatusStackComponentInfo {
                    StatusId = Convert.ToInt32(Required(data, "StatusId"), CultureInfo.InvariantCulture),
                    DelayDistFactors = Read(data, "DelayDistFactors", Float),
                    Offsets = Read(data, "Offsets", Vector),
                    TargetOffsets = Read(data, "TargetOffsets", Vector),
                    IsUseCenterRotations = Read(data, "IsUseCenterRotations", value => Convert.ToBoolean(value, CultureInfo.InvariantCulture)),
                    RotationCenterOffsets = Read(data, "RotationCenterOffsets", Vector) } };
        }

        public T GetComponent<T>() => delay != null && typeof(T).IsInstanceOfType(delay) ? (T)(object)delay : default;

        private static object Required(IReadOnlyDictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                throw new InvalidOperationException("Original skill view configuration is missing " + key);
            return value;
        }

        private static List<T> Read<T>(IReadOnlyDictionary<string, object> data, string key, Func<object, T> convert)
        {
            if (!(Required(data, key) is IList values))
                throw new InvalidOperationException("Original skill view configuration is not a list: " + key);
            var result = new List<T>(values.Count);
            foreach (var value in values) result.Add(convert(value));
            return result;
        }

        private static float Float(object value) => Convert.ToSingle(value, CultureInfo.InvariantCulture);

        private static Vector3 Vector(object value)
        {
            if (!(value is IList xyz) || xyz.Count != 3)
                throw new InvalidOperationException("Original skill view offset must contain three coordinates.");
            return new Vector3(Float(xyz[0]), Float(xyz[1]), Float(xyz[2]));
        }
    }
}
