namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class CharacterPortraitRequestor : ICharacterPortraitRequestor
{
	public Action<RawImage, Texture2D> OnImageUpdate;

	[SerializeField]
	private RawImage mRawImage;

	[SerializeField]
	private string mAnimationName;

	private object mAppearanceData;

	private long mRequestId;

	private ProfessionPortraitKind mPortraitType;

	private int mSex;

	private int mProfession;

	private bool mNeedPersist;

	private ProfessionPortraitCancellation mCancellationToken;

	private bool mIsReady;

	public RawImage Image => mRawImage;

	public void SetRequestInfo(long requestId, ProfessionPortraitKind portraitType, object appearanceData, int sex, int profession, bool needPersist)
	{
		mRequestId = requestId;
		mAppearanceData = appearanceData;
		mSex = sex;
		mProfession = profession;
		mPortraitType = portraitType;
		mNeedPersist = needPersist;
		mIsReady = true;
	}

	public void SetAnimationName(string animationName)
	{
		mAnimationName = animationName;
	}

	public object GetAppearanceData()
	{
		return mAppearanceData;
	}

	public ProfessionPortraitKind GetPortraitType()
	{
		return mPortraitType;
	}

	public int GetSex()
	{
		return mSex;
	}

	public int GetProfession()
	{
		return mProfession;
	}

	public string GetAnimationName()
	{
		return mAnimationName;
	}

	public long GetRequestId()
	{
		return mRequestId;
	}

	public bool NeedPersist()
	{
		return mNeedPersist;
	}

	public void OnTextureUpdate(Texture2D texture)
	{
		if ((Object)(object)mRawImage != (Object)null)
		{
			mRawImage.texture = (Texture)(object)texture;
			((Graphic)mRawImage).SetNativeSize();
		}
		OnImageUpdate?.Invoke(mRawImage, texture);
	}

	public void StopRequest()
	{
		ProfessionPortraitCancellation val = mCancellationToken;
		if (val != null)
		{
			val.Dispose();
		}
		mCancellationToken = null;
		if ((Object)(object)mRawImage != (Object)null)
		{
			mRawImage.texture = null;
		}
	}

	public bool TryGetCancellationToken(out CancellationToken cancellationToken)
	{

		ProfessionPortraitCancellation val = mCancellationToken;
		if (val != null)
		{
			val.Dispose();
		}
		if (mIsReady)
		{
			mCancellationToken = new ProfessionPortraitCancellation();
			cancellationToken = mCancellationToken.Token;
			return true;
		}
		cancellationToken = default;
		return mIsReady;
	}
}

}
