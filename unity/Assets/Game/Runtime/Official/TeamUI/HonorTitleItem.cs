namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;
using UnityEngine;

public class HonorTitleItem : MonoBehaviour
{
	[Serializable]
	public class EffectData
	{
		public GameObject EffectObj;

		[Header("延迟播放显示特效时间")]
		public float DelayPlayShowEffectTime;
	}

	public enum AnimType
	{
		Show,
		Hide,
		Wait,
		Loop
	}

	[SerializeField]
	private Animation mAnimation;

	[SerializeField]
	private WorldStateSwitcher<AnimType> mAnimStateSwitcher;

	[SerializeField]
	private List<EffectData> mEffectDataList;

	[Header("延迟播放显示动画时间")]
	[SerializeField]
	private float mDelayPlayShowAnimTime;

	[Header("延迟播放隐藏动画时间")]
	[SerializeField]
	private float mDelayPlayHideAnimTime;

	private int mItemId;

	private GameObject mPlayableObj;

	private Component mStatePlayable;

	private Action mShowAnimAction;

	private Action mHideAnimAction;

	public int ItemId => mItemId;

	public float DelayPlayShowAnimTime => mDelayPlayShowAnimTime;

	public float DelayPlayHideAnimTime => mDelayPlayHideAnimTime;

	public List<EffectData> EffectDataList => mEffectDataList;

	public Animation Animation => mAnimation;

	private ITeamHonorTitleServices services;
	public void Configure(ITeamHonorTitleServices value) { services = value; }

	private void OnDisable()
	{
		StopPlayable();
	}

	public void Init(int itemId, Action showAnimAction = null, Action hideAnimAction = null)
	{
		mItemId = itemId;
		mShowAnimAction = showAnimAction;
		mHideAnimAction = hideAnimAction;
	}

	public void PlayAnimation(AnimType animType)
	{
		mAnimStateSwitcher.SetState(animType);
		mAnimation.Sample();
	}

	public void PlayEffectObj(int index)
	{
		if (mEffectDataList != null && mEffectDataList.Count > 0 && index >= 0 && index < mEffectDataList.Count)
		{
			GameObject effectObj = mEffectDataList[index].EffectObj;
			if (!((Object)(object)effectObj == (Object)null))
			{
				effectObj.SetActive(false);
				effectObj.SetActive(true);
			}
		}
	}

	private void PlayEffect()
	{
		StopPlayable();
		if (mItemId <= 0 || !services.TryGetPlayableGuid(mItemId, out var value))
		{
			return;
		}
		if ((Object)(object)mPlayableObj != (Object)null)
		{
			mStatePlayable = services.Play(mPlayableObj, ((Component)this).gameObject, ((Component)this).gameObject);
			return;
		}
		services.Resources.LoadAssetByGuid<GameObject>(value).Completed += (IProfessionAssetOperation<GameObject> obj) =>
		{
			if (obj != null)
			{
				mPlayableObj = obj.Result;
				mStatePlayable = services.Play(obj.Result, ((Component)this).gameObject, ((Component)this).gameObject);
			}
		};
	}

	public void StopPlayable()
	{
		if (!((Object)(object)mStatePlayable == (Object)null))
		{
			services.Stop(mStatePlayable);
			mStatePlayable = null;
		}
	}

	public void OnShowAnimFinished()
	{
		mShowAnimAction?.Invoke();
	}

	public void OnHideAnimFinished()
	{
		mHideAnimAction?.Invoke();
	}
}

}
