using System;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    [Serializable] public sealed class OfficialMap12BuildingData
    {
        public string manifest;
        public int mapClassId;
        public OfficialMap12Building[] Buildings;
        public Map10RuntimeTalkConditionInfo[] Conditions;
    }
    [Serializable] public sealed class OfficialMap12Building
    {
        public int EntityClassId, PlacementId, CompleteState, PublicDefaultState;
        public bool CanInteractAfterMove;
        public OfficialMap12BuildingState[] States;
        public WorldStageInteractionOperation[] Operations;
    }
    [Serializable] public sealed class OfficialMap12BuildingState { public long OperationId; }
}
