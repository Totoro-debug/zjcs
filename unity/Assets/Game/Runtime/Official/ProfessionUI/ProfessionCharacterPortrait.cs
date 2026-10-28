namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;
using UnityEngine.UI;

public class ProfessionCharacterPortrait : CharacterPortraitBase
{
	[SerializeField]
	private CharacterPortraitRequestor mRequestor;

	private Action<RawImage> mOnImageUpdate;

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

	protected override void OnDisable()
	{
		mRequestor.StopRequest();
		base.OnDisable();
	}

	public void GeneratePortrait(long playerId, int sex, int profession, object appearanceData, Action<RawImage> onImageUpdate)
	{
		int professionCode = ProfessionPortraitRequestKey.GetProfessionCode(profession);
		long longHashCode = ProfessionPortraitRequestKey.GetLongHashCode(playerId, professionCode);
		mOnImageUpdate = onImageUpdate;
		mRequestor.SetRequestInfo(longHashCode, ProfessionPortraitKind.Profession, appearanceData, sex, profession, needPersist: true);
		SetPortraitData(mRequestor);
	}

	public void OnTextureUpdate(RawImage rawImage, Texture2D texture)
	{
		mOnImageUpdate?.Invoke(rawImage);
	}
}

}
