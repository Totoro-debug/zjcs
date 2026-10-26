using System;
using System.Collections.Generic;

namespace Zjcs.Official.World
{
    // Serialized fields migrated from Common.StageCompCfg, StageStateInfo,
    // SubStageStateInfo and WorldStageComponentInfo. MessagePack is replaced by local JSON.
    [Serializable]
    public sealed class OfficialStageCompCfg
    {
        public int CompType;
        public int CompIndex;
    }

    [Serializable]
    public sealed class OfficialSubStageStateInfo
    {
        public List<OfficialStageCompCfg> Conditions = new List<OfficialStageCompCfg>();
        public OfficialStageCompCfg Behaviour;
        public List<OfficialStageCompCfg> Results = new List<OfficialStageCompCfg>();
        public int Select;
    }

    [Serializable]
    public sealed class OfficialStageStateInfo
    {
        public List<OfficialStageCompCfg> EnterResults = new List<OfficialStageCompCfg>();
        public List<OfficialSubStageStateInfo> SubStateList = new List<OfficialSubStageStateInfo>();
    }

    [Serializable]
    public sealed class OfficialWorldStageRule
    {
        public bool DestroyOnEnd;
        public bool AutoNextState;
        public int StageWorldType;
        public List<OfficialStageStateInfo> StateInfos = new List<OfficialStageStateInfo>();
    }
}
