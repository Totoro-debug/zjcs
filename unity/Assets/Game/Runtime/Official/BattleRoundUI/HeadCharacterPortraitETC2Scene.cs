// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;
using UnityEngine.UI;

public class HeadCharacterPortraitETC2Scene : HeadCharacterPortrait
{
	[SerializeField]
	private WorldCircleSpriteRenderer mCircleSpriteSr;

	private IFightDynamicTextureETC2 mFightDynamicTextureETC2 { get; set; }

	public void BindAtlas(IFightDynamicTextureETC2 atlas) => mFightDynamicTextureETC2 = atlas ?? throw new System.ArgumentNullException(nameof(atlas));

	private void Awake()
	{
		CharacterPortraitRequestor characterPortraitRequestor = mRequestor;
		characterPortraitRequestor.OnImageUpdate = (Action<RawImage, Texture2D>)Delegate.Combine(characterPortraitRequestor.OnImageUpdate, new Action<RawImage, Texture2D>(OnTextureUpdate));
	}

	protected override void OnDestroy()
	{
		CharacterPortraitRequestor characterPortraitRequestor = mRequestor;
		characterPortraitRequestor.OnImageUpdate = (Action<RawImage, Texture2D>)Delegate.Remove(characterPortraitRequestor.OnImageUpdate, new Action<RawImage, Texture2D>(OnTextureUpdate));
		base.OnDestroy();
	}

	private void OnTextureUpdate(RawImage rawImage, Texture2D texture)
	{
		TryReplaceToETC2Atlas(texture);
	}

	private void TryReplaceToETC2Atlas(Texture2D tex)
	{
		if ((Object)(object)tex == (Object)null)
		{
			return;
		}
		GridSpriteInfo gridSpriteInfo = mFightDynamicTextureETC2.TryGetOrAdd(tex, mRequestor.GetRequestId());
		if (gridSpriteInfo == null)
		{
			RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(72, 1, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("HeadCharacterPortraitETC2Scene.TryReplaceToETC2Atlas 替换失败, texture name:");
				message.AppendFormatted(((Object)tex).name, 0, null, "tex.name");
			}
			Log.Warn(ref message, null, "TryReplaceToETC2Atlas", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/Fight/MainPanel/PlayerPortrait/HeadCharacterPortraitETC2Scene.cs", 37);
		}
		else
		{
			mCircleSpriteSr.SetSprite(gridSpriteInfo.Sprite);
		}
	}
}

}
