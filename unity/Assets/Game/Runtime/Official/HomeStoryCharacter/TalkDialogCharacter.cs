// Adapted from the selected original client; see home-story-character source manifest.
namespace Zjcs.Official.HomeStoryCharacter
{
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryPortrait;
using Object = UnityEngine.Object;
using Cysharp.Threading.Tasks;

public sealed class TalkDialogCharacter : MonoBehaviour
{
	[SerializeField]
	private ETalkPortraitDirectionType mTalkPortraitDirectionType;

	[SerializeField]
	private RectTransform mCharacterOffsetAdaptRect;

	[SerializeField]
	private Color mCharacterLightColor;

	[SerializeField]
	private Color mCharacterDarkColor;

	[SerializeField]
	private float mCharacterChangeColorDuration;

	[SerializeField]
	private Animation mAnimation;

	[SerializeField]
	private string mAniMoveIn;

	[SerializeField]
	private string mAniMoveOut;

	[SerializeField]
	private string mAniLight;

	[SerializeField]
	private string mAniDark;

	[SerializeField]
	private string mAniSwitch;

	private string mCurrentPortraitPath;

	private bool mIsLeavedTalk;

	private bool mIsInTalk;

	private PortraitFace mPortraitFace;

	private string mSwitchToPortarit = "";

	private string mSwitchToFace = "";

	private ETalkPortraitStatusType mStatus;

	private static readonly Vector3 sRightPortraitFlipDisable = new Vector3(-1f, 1f, 1f);

	private readonly List<IHomeHudAssetHandle<GameObject>> mPortraitHandles = new List<IHomeHudAssetHandle<GameObject>>();
	public IStoryPortraitServices Services { get; set; }

	public string CurrentPortraitPath => mCurrentPortraitPath;

	private void OnEnable()
	{
		mPortraitHandles.Clear();
	}

	private void OnDisable()
	{
		foreach (var handle in mPortraitHandles) handle.Dispose();
		mPortraitHandles.Clear();
	}

	private void OnSwitchAnimHalf()
	{
		SetPortrait(mSwitchToPortarit, mSwitchToFace);
		ChangePortraitColor(mCharacterDarkColor, mCharacterLightColor);
	}

	public bool IsSamePortrait(string portrait)
	{
		if (string.IsNullOrEmpty(portrait) && string.IsNullOrEmpty(mCurrentPortraitPath))
		{
			return true;
		}
		return portrait == mCurrentPortraitPath;
	}

	public void ChangeFace(string type)
	{
		if ((Object)null != (Object)(object)mPortraitFace)
		{
			mPortraitFace.ChangeFace(type);
		}
	}

	public void ChangeStatus(ETalkPortraitStatusType status, string portraitPath = "", string face = "")
	{
		if (mStatus == status)
		{
			return;
		}
		switch (status)
		{
		case ETalkPortraitStatusType.Enter:
			if (mIsLeavedTalk)
			{
				mAnimation.Play(mAniMoveIn);
				if ((Object)null != (Object)(object)mPortraitFace)
				{
					mPortraitFace.SetColor(mCharacterLightColor);
				}
			}
			SetPortrait(portraitPath, face);
			mIsLeavedTalk = false;
			mIsInTalk = true;
			break;
		case ETalkPortraitStatusType.Leave:
			mAnimation.Play(mAniMoveOut);
			mIsLeavedTalk = true;
			mIsInTalk = false;
			break;
		case ETalkPortraitStatusType.Talk:
			if (mIsLeavedTalk || !mIsInTalk)
			{
				SetPortrait(portraitPath, face);
				ChangePortraitColor(mCharacterDarkColor, mCharacterLightColor);
				mAnimation.Play(mAniLight);
			}
			else if (!IsSamePortrait(portraitPath))
			{
				if ((Object)null != (Object)(object)mAnimation)
				{
					mAnimation.Play(mAniSwitch);
				}
				mSwitchToPortarit = portraitPath;
				mSwitchToFace = face;
			}
			else
			{
				SetPortrait(portraitPath, face);
				ChangePortraitColor(mCharacterDarkColor, mCharacterLightColor);
				mAnimation.Play(mAniLight);
			}
			mIsLeavedTalk = false;
			mIsInTalk = true;
			break;
		case ETalkPortraitStatusType.Shutup:
			ChangePortraitColor(mCharacterLightColor, mCharacterDarkColor);
			mAnimation.Play(mAniDark);
			break;
		}
		mStatus = status;
	}

	private void SetPortrait(string potraitPath, string faceType)
	{
		if (string.IsNullOrEmpty(potraitPath))
		{
			CleanPortrait();
			mCurrentPortraitPath = "";
			return;
		}
		if ((Object)null == (Object)(object)mPortraitFace || !IsSamePortrait(potraitPath))
		{
			CleanPortrait();
			mPortraitFace = CreatePortrait(potraitPath);
			if ((Object)null != (Object)(object)mPortraitFace)
			{
				mPortraitFace.LoadMain();
			}
		}
		if ((Object)null != (Object)(object)mPortraitFace)
		{
			mPortraitFace.ChangeFace(faceType);
		}
		mCurrentPortraitPath = potraitPath;
	}

	private void ChangePortraitColor(Color from, Color to)
	{
		UniTaskExtensions.Forget(StoryPortraitTime.TimeLerp(mCharacterChangeColorDuration, (float t) =>
		{
			Color color = Color.Lerp(from, to, t);
			if ((Object)null != (Object)(object)mPortraitFace)
			{
				mPortraitFace.SetColor(color);
			}
		}));
	}

	private void CleanPortrait()
	{
		if ((Object)null != (Object)(object)mPortraitFace)
		{
			Object.Destroy((Object)(object)((Component)mPortraitFace).gameObject);
			mPortraitFace = null;
		}
	}

	private GameObject LoadPortrait(string path)
	{
		var handle = Services.LoadPortrait(path);
		mPortraitHandles.Add(handle);
		return handle.Result;
	}

	private static GameObject InstantiatePortrait(GameObject prefab, Transform parent)
	{
		GameObject value = Object.Instantiate(prefab, parent, false);
		value.transform.localPosition = Vector3.zero;
		value.transform.localRotation = Quaternion.identity;
		value.transform.localScale = Vector3.one;
		value.SetActive(true);
		return value;
	}

	private PortraitFace CreatePortrait(string path)
	{
		GameObject result = LoadPortrait(path);
		if ((Object)null == (Object)(object)result)
		{
			Debug.LogError("TalkDialogCharacter: original error branch.");
			return null;
		}
		PortraitFace component = InstantiatePortrait(result, (Transform)(object)mCharacterOffsetAdaptRect).GetComponent<PortraitFace>();
		if ((Object)null != (Object)(object)component)
		{
			component.RegionProvider = Services.Region;
			component.SetDirection(mTalkPortraitDirectionType);
		}
		ProcessPortraitFilp(component);
		return component;
	}

	private void ProcessPortraitFilp(PortraitFace portrait)
	{
		if (!((Object)null == (Object)(object)portrait))
		{
			ETalkPortraitDirectionType eTalkPortraitDirectionType = mTalkPortraitDirectionType;
			if (eTalkPortraitDirectionType != ETalkPortraitDirectionType.Left && eTalkPortraitDirectionType == ETalkPortraitDirectionType.Right && !portrait.NeedFlipOnRight)
			{
				((Component)portrait).transform.localScale = sRightPortraitFlipDisable;
			}
		}
	}

	static TalkDialogCharacter()
	{
	}
}

}
