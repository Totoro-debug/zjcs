namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HonorTitleItemProvider : MonoBehaviour, IProfessionPlayerInfoTitle
{
	[SerializeField]
	private Transform mSlot;

	[Header("自动播放显示特效")]
	[SerializeField]
	private bool mAutoPlayShowEffect;

	[Header("自动播放显示动画")]
	[SerializeField]
	private bool mAutoPlayShowAnim;

	[Header("自动播放隐藏动画")]
	[SerializeField]
	private bool mAutoPlayHideAnim;

	[Header("播放完隐藏动画后自动销毁")]
	[SerializeField]
	private bool mAutoDestroyAfterHide;

	private int mItemId;

	private Coroutine mShowAnimCoroutine;

	private Coroutine mHideAnimCoroutine;

	private Coroutine mLoopAnimCoroutine;

	private List<Coroutine> mShowEffectCoroutineList = new List<Coroutine>();

	private WaitForSeconds mShowAnimWaitForSeconds;

	private WaitForSeconds mHideAnimWaitForSeconds;

	private WaitForSeconds mLoopAnimWaitForSeconds;

	private Action mHonorItemShowAction;

	private Action mHonorItemHideAction;

	private TeamInviteResourceOperationBag mDisposableBag = new TeamInviteResourceOperationBag();

	private Action<HonorTitleItem> mInitFinishedAction;

	public HonorTitleItem TitleItem { get; private set; }

	private ITeamHonorTitleServices services;
	public void Configure(ITeamHonorTitleServices value) { services = value; }
	public void Init(object appearance) { Init(appearance as AppearanceData); }

	private void OnDisable()
	{
		StopShowEffectCoroutine();
		StopShowAnimCoroutine();
		StopHideAnimCoroutine();
		StopLoopAnimCoroutine();
	}

	public void Init(IProfessionInfoPlayer player, Action<HonorTitleItem> initAction = null)
	{
		if (player != null && player.AppearanceData != null)
		{
			Init(player.AppearanceData as AppearanceData, initAction);
		}
	}

	public void Init(AppearanceData data, Action<HonorTitleItem> initAction = null)
	{
		if (data != null && data.HonorItemDict.TryGetValue(HonorType.Title, out var value))
		{
			Init(value, initAction);
		}
	}

	public void Init(int itemId, Action<HonorTitleItem> initFinishedAction = null)
	{
		mInitFinishedAction = initFinishedAction;
		if (mHonorItemShowAction == null)
		{
			mHonorItemShowAction = OnHonorItemShowAnimFinished;
		}
		if (mHonorItemHideAction == null)
		{
			mHonorItemHideAction = OnHonorItemHideAnimFinished;
		}
		ShowTitleItem(itemId);
	}

	public void SetActive(bool value)
	{
		if (!((Object)(object)TitleItem == (Object)null))
		{
			((Component)TitleItem).gameObject.SetActive(value);
		}
	}

	public void ClearTitleItem()
	{
		mItemId = 0;
		TitleItem = null;
		mShowAnimWaitForSeconds = null;
		mHideAnimWaitForSeconds = null;
		mLoopAnimWaitForSeconds = null;
		mDisposableBag.Dispose();
		for (int i = mSlot.childCount - 1; i >= 0; i--) Object.Destroy(mSlot.GetChild(i).gameObject);
	}

	private void ShowTitleItem(int itemId)
	{

		if (!((Object)(object)TitleItem != (Object)null) || TitleItem.ItemId != itemId)
		{
			ClearTitleItem();
			mItemId = itemId;
			mDisposableBag = new TeamInviteResourceOperationBag();
			string itemPath = GetItemPath(itemId);
			GameObject result = mDisposableBag.Add(services.Resources.LoadAsset<GameObject>(itemPath)).Result;
			if ((Object)(object)result != (Object)null)
			{
				HonorTitleItem component = services.InstantiateKeepScale(result, mSlot).GetComponent<HonorTitleItem>();
				component.Configure(services);
				InitTitleItem(component);
			}
		}
	}

	private void InitTitleItem(HonorTitleItem titleItem)
	{
		TitleItem = titleItem;
		TitleItem.Init(mItemId, mHonorItemShowAction, mHonorItemHideAction);
		if (mAutoPlayShowAnim)
		{
			StopLoopAnimCoroutine();
			if (TitleItem.DelayPlayShowAnimTime <= 0f)
			{
				TitleItem.PlayAnimation(HonorTitleItem.AnimType.Show);
				mLoopAnimCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayLoopAnim());
			}
			else
			{
				TitleItem.PlayAnimation(HonorTitleItem.AnimType.Wait);
				StopShowAnimCoroutine();
				mShowAnimCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayShowAnim());
			}
		}
		if (mAutoPlayShowEffect)
		{
			StopShowEffectCoroutine();
			for (int i = 0; i < TitleItem.EffectDataList.Count; i++)
			{
				HonorTitleItem.EffectData effectData = TitleItem.EffectDataList[i];
				if (effectData.DelayPlayShowEffectTime <= 0f)
				{
					TitleItem.PlayEffectObj(i);
					continue;
				}
				Coroutine item = ((MonoBehaviour)this).StartCoroutine(PlayShowEffect(i, effectData.DelayPlayShowEffectTime));
				mShowEffectCoroutineList.Add(item);
			}
		}
		mInitFinishedAction?.Invoke(TitleItem);
	}

	private void OnHonorItemShowAnimFinished()
	{
		if (mAutoPlayHideAnim)
		{
			if (TitleItem.DelayPlayHideAnimTime <= 0f)
			{
				TitleItem.PlayAnimation(HonorTitleItem.AnimType.Hide);
				return;
			}
			StopHideAnimCoroutine();
			mHideAnimCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayHideAnim());
		}
	}

	private void OnHonorItemHideAnimFinished()
	{
		if (mAutoDestroyAfterHide)
		{
			ClearTitleItem();
		}
	}

	private IEnumerator PlayShowEffect(int index, float delayPlayShowEffectTime)
	{
		yield return (object)new WaitForSeconds(delayPlayShowEffectTime);
		if ((Object)(object)TitleItem != (Object)null)
		{
			TitleItem.PlayEffectObj(index);
		}
	}

	private IEnumerator PlayShowAnim()
	{
		if (mShowAnimWaitForSeconds == null)
		{
			mShowAnimWaitForSeconds = new WaitForSeconds(TitleItem.DelayPlayShowAnimTime);
		}
		yield return mShowAnimWaitForSeconds;
		if ((Object)(object)TitleItem != (Object)null)
		{
			TitleItem.PlayAnimation(HonorTitleItem.AnimType.Show);
		}
		mLoopAnimCoroutine = ((MonoBehaviour)this).StartCoroutine(PlayLoopAnim());
	}

	private IEnumerator PlayHideAnim()
	{
		if (mHideAnimWaitForSeconds == null)
		{
			mHideAnimWaitForSeconds = new WaitForSeconds(TitleItem.DelayPlayHideAnimTime);
		}
		yield return mHideAnimWaitForSeconds;
		if ((Object)(object)TitleItem != (Object)null)
		{
			TitleItem.PlayAnimation(HonorTitleItem.AnimType.Hide);
		}
	}

	private IEnumerator PlayLoopAnim()
	{
		if (mLoopAnimWaitForSeconds == null)
		{
			mLoopAnimWaitForSeconds = new WaitForSeconds(TitleItem.Animation.clip.length);
		}
		yield return mLoopAnimWaitForSeconds;
		if ((Object)(object)TitleItem != (Object)null)
		{
			TitleItem.PlayAnimation(HonorTitleItem.AnimType.Loop);
		}
	}

	private void StopShowEffectCoroutine()
	{
		if (mShowEffectCoroutineList.Count <= 0)
		{
			return;
		}
		foreach (Coroutine mShowEffectCoroutine in mShowEffectCoroutineList)
		{
			((MonoBehaviour)this).StopCoroutine(mShowEffectCoroutine);
		}
		mShowEffectCoroutineList.Clear();
	}

	private void StopShowAnimCoroutine()
	{
		if (mShowAnimCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mShowAnimCoroutine);
			mShowAnimCoroutine = null;
		}
	}

	private void StopHideAnimCoroutine()
	{
		if (mHideAnimCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mHideAnimCoroutine);
			mHideAnimCoroutine = null;
		}
	}

	private void StopLoopAnimCoroutine()
	{
		if (mLoopAnimCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mLoopAnimCoroutine);
			mLoopAnimCoroutine = null;
		}
	}

	private string GetItemPath(int itemId)
	{
		return $"UI/Honor/HonorTitleItem/HonorTitleItem_{itemId}";
	}
}

}
