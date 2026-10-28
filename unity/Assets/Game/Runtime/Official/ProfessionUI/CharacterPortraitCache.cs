namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class CharacterPortraitCache
{
	public const long kDefaultHashCode = -1L;

	private const float kExpiryTime = 60f;

	private CharacterPortraitTexture2D mTexture;

	private float mClearTimeStamp;

	public List<ICharacterPortraitRequestor> Requestors = new List<ICharacterPortraitRequestor>();

	public long AppearanceHashCode { get; private set; }

	public bool NeedRecycle { get; private set; }

	public void UpdateTexture(CharacterPortraitTexture2D characterPortraitTexture, long hashCode, bool needRecycle, out CharacterPortraitTexture2D oldTexture)
	{
		oldTexture = (needRecycle ? mTexture : null);
		mTexture = characterPortraitTexture;
		AppearanceHashCode = hashCode;
		NeedRecycle = needRecycle;
		UpdateTimeStamp();
		int count = Requestors.Count;
		for (int i = 0; i < count; i++)
		{
			Requestors[i].OnTextureUpdate(characterPortraitTexture.Tex);
		}
	}

	public void Clear()
	{
		mTexture = null;
		AppearanceHashCode = 0L;
		NeedRecycle = false;
		if (Requestors.Count != 0)
		{
			Debug.LogError("CharacterPortraitCache 在有引用的时候被释放， 引用数量：");
		}
	}

	public Texture2D SubcribeTexture(ICharacterPortraitRequestor requestor)
	{
		Requestors.Add(requestor);
		return mTexture.Tex;
	}

	public void Unsubscribe(ICharacterPortraitRequestor requestor)
	{
		Requestors.Remove(requestor);
	}

	public CharacterPortraitTexture2D ForceGetTexture()
	{
		return mTexture;
	}

	public bool IsExpire(float nowTime)
	{
		if (Requestors.Count != 0)
		{
			return false;
		}
		if (mClearTimeStamp > nowTime)
		{
			return false;
		}
		return true;
	}

	private void UpdateTimeStamp()
	{
		mClearTimeStamp = Time.realtimeSinceStartup + 60f;
	}
}

}
