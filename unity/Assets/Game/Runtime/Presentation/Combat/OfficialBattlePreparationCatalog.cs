using System;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattlePreparationCatalog : ScriptableObject
    {
        public TextAsset Source;
        public GameObject AreaPrefab;
        public GameObject PlayerPointerPrefab;
        public float DownKeepTime;
        public float DragThreshold;
        [NonSerialized] private Data data;

        [Serializable] private sealed class Data { public Range[] ranges; public Offset[] offsets; }
        [Serializable] private sealed class Range { public int stageId, type, width, height; }
        [Serializable] private sealed class Offset { public int type, x, y; }

        public static OfficialBattlePreparationCatalog Load()
        {
            var result = Resources.Load<OfficialBattlePreparationCatalog>("Combat/Preparation/OfficialBattlePreparationCatalog");
            if (result == null || result.Source == null || result.AreaPrefab == null || result.PlayerPointerPrefab == null)
                throw new InvalidOperationException("Official preparation catalog has not been imported.");
            return result;
        }

        public FightBattlePreparationRange RangeFor(int stageId, FightGridPoint entry)
        {
            if (data == null) data = JsonUtility.FromJson<Data>(Source.text);
            Range range = Array.Find(data.ranges, value => value.stageId == stageId);
            if (range == null) throw new InvalidOperationException("Official preparation stage is absent: " + stageId);
            Offset offset = Array.Find(data.offsets, value => value.type == range.type);
            if (offset != null) entry += new FightGridPoint(offset.x, offset.y);
            return new FightBattlePreparationRange(entry, range.width, range.height,
                new FightGridPoint(range.width / 2, range.height / 2));
        }
    }
}
