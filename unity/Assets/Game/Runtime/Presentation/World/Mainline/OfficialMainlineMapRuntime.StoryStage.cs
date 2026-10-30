using System;
using System.IO;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        [Serializable]
        private sealed class StoryStartStageFields
        {
            public int StageId;
            public int IsUsePlayerLevelAsStageLevel;
        }

        private bool map14StoryStageBattle;
        private bool map14StoryStageWon;

        private void StartMap14StoryStage(Map10RuntimeStoryNode node)
        {
            if (mapClassId != 14)
                throw new InvalidDataException("Official story stage entry is not migrated for Map" + mapClassId + ".");
            StoryStartStageFields fields = JsonUtility.FromJson<StoryStartStageFields>(node.sourceJson);
            if (fields == null || (fields.StageId != 52 && fields.StageId != 53)
                || fields.IsUsePlayerLevelAsStageLevel != 0
                || !stages.TryGetValue(fields.StageId, out Map10RuntimeStage stage)
                || !rosters.TryGetValue(fields.StageId, out Map10RuntimeRoster roster)
                || !battleScenes.TryGetValue(stage.GameId, out Map10RuntimeBattleScene scene))
                throw new InvalidDataException("Official Map14 story battle configuration is incomplete: " + node.sourceJson);
            if (map14StoryStageBattle || battleController != null)
                throw new InvalidOperationException("An official Map14 story battle is already active.");

            map14StoryStageBattle = true;
            map14StoryStageWon = false;
            try
            {
                BeginBattle(null, stage, roster, scene, () =>
                {
                    bool won = map14StoryStageWon;
                    map14StoryStageBattle = false;
                    map14StoryStageWon = false;
                    RunStoryGraph(won ? StoryNext(node, "SuccessExit") : node.id);
                });
            }
            catch
            {
                map14StoryStageBattle = false;
                map14StoryStageWon = false;
                throw;
            }
        }
    }
}
