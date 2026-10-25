using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.World
{
    [Serializable]
    public sealed class DailyChallengeBattleRuntimeData
    {
        public string manifest;
        public int[] stageIds;
        public List<DailyDungeonRuntimeStage> stages = new List<DailyDungeonRuntimeStage>();
        public List<DailyDungeonRuntimeBattleScene> battleScenes = new List<DailyDungeonRuntimeBattleScene>();
        public List<DailyDungeonRuntimeFormation> formations = new List<DailyDungeonRuntimeFormation>();
    }

    public sealed class OfflineDailyChallengeBattleCatalog
    {
        public const string ResourcePath = "DailyChallenges/battle-stages";
        public const string ExpectedManifest = "854_156383";
        private readonly DailyChallengeBattleRuntimeData data;

        private OfflineDailyChallengeBattleCatalog(DailyChallengeBattleRuntimeData value) => data = value;

        public IReadOnlyList<DailyDungeonRuntimeStage> Stages => data.stages;
        public IReadOnlyList<DailyDungeonRuntimeBattleScene> BattleScenes => data.battleScenes;
        public IReadOnlyList<DailyDungeonRuntimeFormation> Formations => data.formations;

        public static OfflineDailyChallengeBattleCatalog LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new FileNotFoundException("Official daily challenge battle catalog is missing.");
            DailyChallengeBattleRuntimeData value = JsonUtility.FromJson<DailyChallengeBattleRuntimeData>(asset.text);
            if (value == null || value.manifest != ExpectedManifest || value.stageIds == null
                || value.stages == null || value.battleScenes == null || value.formations == null)
                throw new InvalidDataException("Official daily challenge battle catalog is invalid.");
            if (value.stageIds.Distinct().Count() != value.stageIds.Length
                || value.stageIds.Any(id => value.stages.Count(stage => stage != null && stage.id == id) != 1))
                throw new InvalidDataException("Official daily challenge stage list is inconsistent.");
            foreach (DailyDungeonRuntimeStage stage in value.stages)
                if (stage.id <= 0 || stage.gameId <= 0
                    || !value.battleScenes.Any(scene => scene.gameId == stage.gameId))
                    throw new InvalidDataException("Official daily challenge stage scene mapping is incomplete.");
            return new OfflineDailyChallengeBattleCatalog(value);
        }

        public DailyDungeonRuntimeStage FindStage(int stageId) =>
            data.stages.FirstOrDefault(stage => stage != null && stage.id == stageId);

        public DailyDungeonRuntimeBattleScene FindScene(int gameId) =>
            data.battleScenes.FirstOrDefault(scene => scene != null && scene.gameId == gameId);
    }
}
