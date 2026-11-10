// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

public class GameRoundPortraitNormalHandler : MonoBehaviour, IGameRoundPortraitHandler
{
	public GameObject PlayerIconGo;

	public HeadCharacterPortrait HeadPlayerPortrait;

	public RawImage HeadImg;

	private void OnDisable()
	{
		((Graphic)HeadImg).color = Color.white;
	}

	public void SetPortraitData(long playerId, Sex sex, int profession, object appearanceData)
	{
		HeadPlayerPortrait.GeneratePortrait(playerId, sex, profession, appearanceData);
	}

	public void SetPortraitData(Sex sex, int profession, object appearanceData)
	{
		HeadPlayerPortrait.GenerateTemporarilyPortrait(sex, profession, appearanceData);
	}

	public void SetPortraitFactionImg(string imgPath)
	{
	}

	public void SetPortraitColor(Color color)
	{
		((Graphic)HeadImg).color = color;
	}

	public void SetPortraitActive(bool active)
	{
		PlayerIconGo.SetActive(active);
	}
}

}
