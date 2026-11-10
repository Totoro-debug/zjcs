// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public class GameRoundPortraitETC2SceneHandler : MonoBehaviour, IGameRoundPortraitHandler
{
	public GameObject PlayerIconGo;

	public HeadCharacterPortraitETC2Scene HeadPlayerPortrait;

	public WorldCircleSpriteRenderer HeadSr;

	public SpriteRenderer FactionBgSr;

	private IFightDynamicTextureETC2 mFightDynamicTextureETC2 { get; set; }

	public void BindAtlas(IFightDynamicTextureETC2 atlas) => mFightDynamicTextureETC2 = atlas ?? throw new System.ArgumentNullException(nameof(atlas));

	private void OnDisable()
	{
		HeadSr.SetColor(Color.white);
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
		GridSpriteInfo gridSpriteInfo = mFightDynamicTextureETC2.Get(imgPath.GetHashCode());
		if (gridSpriteInfo != null)
		{
			FactionBgSr.sprite = gridSpriteInfo.Sprite;
		}
	}

	public void SetPortraitColor(Color color)
	{
		HeadSr.SetColor(color);
	}

	public void SetPortraitActive(bool active)
	{
		PlayerIconGo.SetActive(active);
	}
}

}
