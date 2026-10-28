// Migrated source application/query code; see map13-entity-state-views receipt.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.World.Map13EntityStateViews
{
    // Explicit source-consumption data boundary. Only the original shared
    // state queries below are migrated; this is not the WorldStage executor.
    [Serializable] public sealed class Map13StageStateQueryData
    { public List<StageStateInfo> StateInfos = new List<StageStateInfo>(); }
    public class Map13WorldStageQuery
    {
        public int State;
        public Map13StageStateQueryData Info;
public virtual int GetStateComponentIndex(StageComponentType compType, int subStateIndex = 0)
	{
		if (compType == StageComponentType.None)
		{
			return -1;
		}
		StageStateInfo curStateInfo = GetCurStateInfo();
		if (curStateInfo == null || curStateInfo.SubStateList == null || subStateIndex >= curStateInfo.SubStateList.Count)
		{
			return -1;
		}
		SubStageStateInfo subStageStateInfo = curStateInfo.SubStateList[subStateIndex];
		if (subStageStateInfo.Conditions != null && subStageStateInfo.Conditions.Count > 0)
		{
			foreach (StageCompCfg condition in subStageStateInfo.Conditions)
			{
				if (condition.CompType == compType)
				{
					return condition.CompIndex;
				}
			}
		}
		if (subStageStateInfo.Behaviour != null && subStageStateInfo.Behaviour.CompType == compType)
		{
			return subStageStateInfo.Behaviour.CompIndex;
		}
		if (subStageStateInfo.Results != null && subStageStateInfo.Results.Count > 0)
		{
			foreach (StageCompCfg result in subStageStateInfo.Results)
			{
				if (result.CompType == compType)
				{
					return result.CompIndex;
				}
			}
		}
		return -1;
	}

public StageStateInfo GetCurStateInfo()
	{
		if (State < 0 || State >= Info.StateInfos.Count)
		{
			return null;
		}
		return Info.StateInfos[State];
	}
    }
}
