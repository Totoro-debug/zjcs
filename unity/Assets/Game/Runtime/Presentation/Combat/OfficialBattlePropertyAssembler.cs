using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Converts official battle properties while preserving the shared ordering and overflow rules.</summary>
    public static class OfficialBattlePropertyAssembler
    {
        public static List<FightPropertyValue> ToProperties(IEnumerable<KeyValuePair<int, long>> values)
        {
            return (values ?? Enumerable.Empty<KeyValuePair<int, long>>())
                .Where(value => value.Key > 0)
                .OrderBy(value => value.Key)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value })
                .ToList();
        }

        public static List<FightPropertyValue> ToNonZeroProperties(IEnumerable<KeyValuePair<int, long>> values)
        {
            return (values ?? Enumerable.Empty<KeyValuePair<int, long>>())
                .Where(value => value.Key > 0 && value.Value != 0)
                .OrderBy(value => value.Key)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value })
                .ToList();
        }

        public static Dictionary<int, long> ToMap(IEnumerable<FightPropertyValue> values)
        {
            var result = new Dictionary<int, long>();
            foreach (FightPropertyValue value in values ?? Enumerable.Empty<FightPropertyValue>())
                if (value != null && value.Id > 0)
                    result[value.Id] = result.TryGetValue(value.Id, out long current)
                        ? checked(current + value.Value) : value.Value;
            return result;
        }

        public static long Value(IReadOnlyDictionary<int, long> values, int id) =>
            values != null && values.TryGetValue(id, out long result) ? result : 0L;

        public static long Value(IEnumerable<FightPropertyValue> values, int id) =>
            Value(ToMap(values), id);

        public static int CheckedValue(IReadOnlyDictionary<int, long> values, int id)
        {
            long value = Value(values, id);
            return value <= 0 ? 1 : checked((int)Math.Min(value, int.MaxValue));
        }
    }
}
