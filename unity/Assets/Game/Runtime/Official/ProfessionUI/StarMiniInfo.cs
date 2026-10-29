namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StarMiniInfo : MonoBehaviour
{
	public Transform StarListSlot;

	[Header("不同品质贴图")]
	[SerializeField]
	private MonoDictionary<Quality, Sprite> mQualitySpriteDict;

	private bool mIsInited;

	private List<Image> mStarImgList = new List<Image>();

	private List<Animator> mStarAnimatorList = new List<Animator>();

	private readonly string mAnimatorParamName = "TwinkleState";

	private int mAnimatorParamNameHash;

	public void Show(int currentStar, int maxStar, Quality quality, bool showBlankStar = false)
	{
		((Component)this).gameObject.SetActive(true);
		if (!mIsInited)
		{
			Init();
		}
		Sprite qulitySprite = GetQulitySprite(quality);
		for (int i = 0; i < StarListSlot.childCount; i++)
		{
			((Component)StarListSlot.GetChild(i)).gameObject.SetActive(i < currentStar || (showBlankStar && i < maxStar));
			Image val = mStarImgList[i];
			if ((Object)(object)val != (Object)null)
			{
				val.sprite = qulitySprite;
				((Component)val).gameObject.SetActive(i < currentStar);
			}
		}
	}

	private void Init()
	{
		for (int i = 0; i < StarListSlot.childCount; i++)
		{
			Transform child = StarListSlot.GetChild(i);
			if (child.childCount != 0)
			{
				Transform child2 = child.GetChild(0);
				mStarImgList.Add(((Component)child2).GetComponent<Image>());
				mStarAnimatorList.Add(((Component)child2).GetComponent<Animator>());
			}
		}
		mIsInited = true;
		mAnimatorParamNameHash = Animator.StringToHash(mAnimatorParamName);
	}

	public Sprite GetQulitySprite(Quality quality)
	{
		if (mQualitySpriteDict.TryGetValue(quality, out var value))
		{
			return value;
		}
		return mQualitySpriteDict[Quality.None];
	}

	private Animator GetAnimator(int star)
	{
		Animator result = null;
		for (int i = 0; i < StarListSlot.childCount; i++)
		{
			if (star > 0 && star == i + 1 && mStarAnimatorList.Count > i)
			{
				result = mStarAnimatorList[i];
			}
		}
		return result;
	}

	public void TryBlink(int star)
	{
		Animator animator = GetAnimator(star);
		if ((Object)(object)animator != (Object)null)
		{
			animator.SetBool(mAnimatorParamNameHash, true);
		}
	}

	public void StopAllBlink()
	{
		for (int i = 0; i < mStarAnimatorList.Count; i++)
		{
			if ((Object)(object)mStarAnimatorList[i] != (Object)null && ((Behaviour)mStarAnimatorList[i]).isActiveAndEnabled)
			{
				mStarAnimatorList[i].SetBool(mAnimatorParamNameHash, false);
			}
		}
	}

	public void Hide()
	{
		StopAllBlink();
		((Component)this).gameObject.SetActive(false);
	}
}

}
