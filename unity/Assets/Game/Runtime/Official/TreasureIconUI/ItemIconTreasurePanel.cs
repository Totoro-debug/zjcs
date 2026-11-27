// Original treasure UI controller; explicit local bindings only.
using Object=UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureCultivation;
namespace Zjcs.Official.TreasureUI {
using System;
using System.Collections.Generic;



using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ItemIconTreasurePanel : ItemIconTreasure
{
	public List<Sprite> QualityBgImages;

	public ItemIconTreasureQualityEffect QualityEffect;

	public AnimationClip UnactivateAnim;

	public AnimationClip NormalAnim;

	public Animation IconAnim;

	public Button ReplaceBtn;

	private TreasureReplaceInfo mReplaceInfo;



	protected override void OnEnableAndInited()
	{


		base.OnEnableAndInited();
		((UnityEvent)ReplaceBtn.onClick).AddListener((UnityAction)OnReplace);
		ITreasureReplaceEventLisener replaceListener = Services.ReplaceListener;
		replaceListener.OnTreasureStartReplace = (Action<TreasureReplaceInfo>)Delegate.Combine(replaceListener.OnTreasureStartReplace, new Action<TreasureReplaceInfo>(PrepareReplace));
		ITreasureReplaceEventLisener replaceListener2 = Services.ReplaceListener;
		replaceListener2.OnTreasureCancelReplace = (Action)Delegate.Combine(replaceListener2.OnTreasureCancelReplace, new Action(CancelReplace));
		TreasureReplaceInfo replaceInfo = Services.ReplaceListener.GetReplaceInfo();
		if (replaceInfo != null)
		{
			PrepareReplace(replaceInfo);
		}
	}

	protected override void OnDisableAndInited()
	{


		ITreasureReplaceEventLisener replaceListener = Services.ReplaceListener;
		replaceListener.OnTreasureStartReplace = (Action<TreasureReplaceInfo>)Delegate.Remove(replaceListener.OnTreasureStartReplace, new Action<TreasureReplaceInfo>(PrepareReplace));
		ITreasureReplaceEventLisener replaceListener2 = Services.ReplaceListener;
		replaceListener2.OnTreasureCancelReplace = (Action)Delegate.Remove(replaceListener2.OnTreasureCancelReplace, new Action(CancelReplace));
		((UnityEvent)ReplaceBtn.onClick).RemoveListener((UnityAction)OnReplace);
		CancelReplace();
		base.OnDisableAndInited();
	}

	private void PrepareReplace(TreasureReplaceInfo info)
	{
		TreasureState itemParamTreasure = Services.GetState(myItemData);
		if (itemParamTreasure != null && itemParamTreasure.Active)
		{
			mReplaceInfo = info;
			((Component)ReplaceBtn).gameObject.SetActive(true);
		}
	}

	private void CancelReplace()
	{
		((Component)ReplaceBtn).gameObject.SetActive(false);
		mReplaceInfo = null;
	}

	private void OnReplace()
	{
		if (myInfo != null && mReplaceInfo != null)
		{
			if (!Services.CanSetupTreasureSlot(myInfo.ClassId, mReplaceInfo.OriginIndex))
			{
				Services.ReplaceListener.FireOnTreasureCancelReplace();
				return;
			}
			Services.SetupTreasureSlot(new PlayerSetupTreasureSlotRequest
			{
				TreasureClassId = myInfo.ClassId,
				TargetIndex = mReplaceInfo.OriginIndex
			});
		}
	}

	public override void UpdateQualityImage(Quality quality)
	{
		if (QualityImg!=null)
		{
			if (quality < Quality.None || (int)quality >= QualityBgImages.Count)
			{
				return;
			}
			QualityImg.sprite = QualityBgImages[(int)quality];
		}
		QualityEffect.SetState(quality);
	}

	protected override bool SetCenterHintUIText()
	{
		if (Services.HasItem(myInfo.ClassId, 1L))
		{
			IconAnim.clip = NormalAnim;
			IconAnim.Play();
		}
		return base.SetCenterHintUIText();
	}

	protected override void MainPlayer_OnTreasureRefresh(IProfessionItemData itemData, bool isNew)
	{
		base.MainPlayer_OnTreasureRefresh(itemData, isNew);
		TreasureReplaceInfo replaceInfo = Services.ReplaceListener.GetReplaceInfo();
		if (replaceInfo != null)
		{
			PrepareReplace(replaceInfo);
		}
	}

	protected override void ShowUnactivate()
	{
		base.ShowUnactivate();
		IconAnim.clip = UnactivateAnim;
		IconAnim.Play();
	}

	public void UnhasShow(bool isShow)
	{
		Services.SetUIGray(IconGameObject, isShow);
	}
}

}
