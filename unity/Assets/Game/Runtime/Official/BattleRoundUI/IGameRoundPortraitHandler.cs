// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public interface IGameRoundPortraitHandler
{
	void SetPortraitData(long playerId, Sex sex, int profession, object appearanceData);

	void SetPortraitData(Sex sex, int profession, object appearanceData);

	void SetPortraitFactionImg(string imgPath);

	void SetPortraitColor(Color color);

	void SetPortraitActive(bool active);
}

}
