using System;
using System.Collections.Generic;

namespace Zjcs.Official.BattleResultUI
{
    // Presentation adapter for the fields consumed from PlayerFightResult. The
    // completed battle/checkpoint remains owned by the common battle module.
    public sealed class BattleResultData
    {
        public int StageId;
        public long GameId;
        public bool FastVictory;
        public bool Win;
        public long SelfAccDamage, TeamAccDamage;
        public IReadOnlyList<BattleResultPlayer> Players;
    }

    public sealed class BattleResultPlayer
    {
        public BattleResultTarget TargetData;
    }

    public sealed class BattleResultTarget
    {
        public long TargetId;
        public bool IsNpc;
    }

    public sealed class BattleResultStage
    {
        public int Id, Type;
    }

    // Original settings/parser/record/report operations consumed by
    // GameEndPanelBase. Offline adapters keep online actions disabled.
    public interface IBattleResultBaseHost
    {
        BattleResultStage GetStage(int stageId);
        ISet<int> ShareStageTypes { get; }
        ISet<int> SaveRecordStageTypes { get; }
        ISet<int> ThumbsUpStageTypes { get; }
        bool IsPlayback { get; }
        bool IsAutoSaveRecordStage(int stageId);
        bool HasLocalCapturedRecord(long gameId);
        IEnumerable<long> TeamMemberTargetIds { get; }
        void SetThumbsUpGameId(long gameId);
        void OpenReport(BattleResultData result);
        void OpenShare(long gameId);
        bool ActiveSaveRecord(long gameId, int stageType);
        void ShowSaveRecordSuccess();
    }
}
