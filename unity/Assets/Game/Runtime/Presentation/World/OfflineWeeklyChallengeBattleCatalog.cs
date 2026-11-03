using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Presentation.World;

namespace Zjcs.Offline.World
{
    // Weekly modes consume the same stage, roster, grid and scene DTOs as the
    // other battle hosts. The activity tables only select official stage ids.
    public sealed class OfflineWeeklyChallengeBattleCatalog
    {
        public const string ResourcePath = "WeeklyChallenges/battle-stages";
        public const string ExpectedManifest = "884_156692";
        private readonly Map10RuntimeBattleData data;
        private readonly HashSet<int> voidFissureIds;
        private readonly HashSet<int> demonBattlefieldIds;

        private OfflineWeeklyChallengeBattleCatalog(Map10RuntimeBattleData value)
        {
            data = value;
            voidFissureIds = new HashSet<int>(value.voidFissureStageIds ?? Array.Empty<int>());
            demonBattlefieldIds = new HashSet<int>(value.demonBattlefieldStageIds ?? Array.Empty<int>());
        }

        public IReadOnlyList<Map10RuntimeStage> Stages => data.stages;
        public IReadOnlyList<Map10RuntimeRoster> MonsterRosters => data.monsterRosters;
        public IReadOnlyList<Map10RuntimeBattleScene> BattleScenes => data.battleScenes;
        public IReadOnlyCollection<int> VoidFissureStageIds => voidFissureIds;
        public IReadOnlyCollection<int> DemonBattlefieldStageIds => demonBattlefieldIds;

        public static OfflineWeeklyChallengeBattleCatalog LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new FileNotFoundException("Official weekly battle catalog is missing.");
            WeeklyBattleRuntimeData value = JsonUtility.FromJson<WeeklyBattleRuntimeData>(asset.text);
            if (value == null || value.manifest != ExpectedManifest || value.stages == null
                || value.monsterRosters == null || value.battleScenes == null)
                throw new InvalidDataException("Official weekly battle catalog is invalid.");
            if (value.stages.Length != 201 || value.battleScenes.Length != 3
                || value.stages.Select(stage => stage.Id).Distinct().Count() != value.stages.Length)
                throw new InvalidDataException("Official weekly battle catalog counts are inconsistent.");
            foreach (Map10RuntimeStage stage in value.stages)
            {
                if (stage == null || stage.Id <= 0 || stage.GameId <= 0
                    || !value.battleScenes.Any(scene => scene != null && scene.GameId == stage.GameId)
                    || !value.monsterRosters.Any(roster => roster != null && roster.StageId == stage.Id))
                    throw new InvalidDataException("Official weekly stage scene or roster mapping is incomplete.");
            }
            if (!value.voidFissureStageIds.Contains(7301) || !value.voidFissureStageIds.Contains(7525)
                || !value.demonBattlefieldStageIds.Contains(4800))
                throw new InvalidDataException("Official weekly activity stage bindings are incomplete.");
            return new OfflineWeeklyChallengeBattleCatalog(value);
        }

        public bool TryGetBattle(int stageId, out Map10RuntimeStage stage,
            out Map10RuntimeRoster roster, out Map10RuntimeBattleScene scene)
        {
            stage = data.stages.FirstOrDefault(value => value != null && value.Id == stageId);
            roster = stage == null ? null : data.monsterRosters.FirstOrDefault(value =>
                value != null && value.StageId == stageId);
            scene = stage == null ? null : data.battleScenes.FirstOrDefault(value =>
                value != null && value.GameId == stage.GameId);
            return stage != null && roster != null && scene != null;
        }

        [Serializable]
        private sealed class WeeklyBattleRuntimeData
        {
            public string manifest;
            public int[] voidFissureStageIds;
            public int[] demonBattlefieldStageIds;
            public Map10RuntimeStage[] stages;
            public Map10RuntimeRoster[] monsterRosters;
            public Map10RuntimeBattleScene[] battleScenes;
        }
    }
}
