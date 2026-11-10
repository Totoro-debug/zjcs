// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

public class GameRoundPortraitETC2Handler : MonoBehaviour, IGameRoundPortraitHandler
{
	public GameObject PlayerIconGo;

	public HeadCharacterPortraitETC2 HeadPlayerPortrait;

	public GridSpriteRawImage HeadImg;

	public GridSpriteImage FactionBgImg;

	private IFightDynamicTextureETC2 mFightDynamicTextureETC2 { get; set; }

	public void BindAtlas(IFightDynamicTextureETC2 atlas) => mFightDynamicTextureETC2 = atlas ?? throw new System.ArgumentNullException(nameof(atlas));

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
		GridSpriteInfo gridSpriteInfo = mFightDynamicTextureETC2.Get(imgPath.GetHashCode());
		if (gridSpriteInfo != null)
		{
			FactionBgImg.SetGridSpriteInfo(gridSpriteInfo);
		}
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
