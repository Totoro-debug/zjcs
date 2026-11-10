namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;

public class SwitchInStageGameBtn : SwitchInGameBtnBase
{
	public List<StageType> StageTypes;

	public override bool HasGameToSwitchIn()
	{
		foreach (IHomeHudGameData value in Services.MainPlayer.CurrentGames.Values)
		{
			if (value.IsBackground)
			{
				StageType type = Services.GetStageType(value.StageId);
				if (StageTypes.Contains(type))
				{
					mGameId = value.GameId;
					return true;
				}
			}
		}
		return false;
	}
}

}
