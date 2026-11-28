// Complete original common promotion control; explicit local material services.
using System;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.ProfessionUI {
using Object=UnityEngine.Object;



using UnityEngine;
using UnityEngine.UI;

public class ProfessionCommonMaterial : UIBase
{
    private IProfessionMaterialServices services;
    private IProfessionMaterialServices Services => services??throw new InvalidOperationException("Bind original material controls before activation.");
    public void Configure(IProfessionMaterialServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	public enum State
	{
		AddTip,
		ShowIconTip,
		Show
	}

	[SerializeField]
	private Image mRankImage;

	[SerializeField]
	private GameObject mIconGo;

	[SerializeField]
	private WorldMultiLanguageText mCountText;

	[SerializeField]
	private ItemIconAttachment mItemIconAttachment;

	[SerializeField]
	private WorldStateSwitcher<State> mStateSwitcher;

	private long mCurrent;



	public ProfessionCommonMaterial SetQuality(Quality quality)
	{
		if ((Object)(object)mRankImage != (Object)null)
		{
			mRankImage.sprite = Services.IconConfig.GetQualityBgImage(quality);
		}
		return this;
	}

	public ProfessionCommonMaterial RefreshCount(long current, long target)
	{


		mCurrent = current;
		if (current >= target)
		{
			mCountText.SetKey("{0}/{1}", CultureNumberArg.Compact(current), CultureNumberArg.Compact(target));
		}
		else
		{
			mCountText.SetKey("{0}/{1}", Services.DecorateLack(CultureNumberArg.Compact(current)), CultureNumberArg.Compact(target));
		}
		return this;
	}

	public ProfessionCommonMaterial SetNeedCount(long count)
	{
		mCountText.SetKey("{0}", CultureNumberArg.Compact(count));
		return this;
	}

	public ProfessionCommonMaterial SetRedDot(bool show)
	{
		if ((Object)(object)mItemIconAttachment != (Object)null)
		{
			mItemIconAttachment.SetShow(ItemIconAttachmentType.RedDotTip, show);
		}
		return this;
	}

	public void Show(State? state = null)
	{
		if (!state.HasValue)
		{
			state = GetCurrentState();
		}
		mStateSwitcher.SetState(state.Value);
	}

	private State GetCurrentState()
	{
		if (mCurrent <= 0)
		{
			return State.AddTip;
		}
		return State.Show;
	}
}

}
