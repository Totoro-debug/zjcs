// Original selected home matching UI; service and namespace bindings only.
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.WorldUI;
using CultureTextArg=Zjcs.Official.World.NativeLabelUI.Framework.CultureTextArg;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeSocialUiControls
{
using UnityEngine;
using UnityEngine.UI;

public class GameMatchMiniSwitchPanel : GameMatchMiniPanel
{
	private enum SwitchType
	{
		InstantToFull,
		InstantToSimple,
		SwitchToFull,
		SwitchToSimple
	}

	[SerializeField]
	private WorldStateSwitcher<SwitchType> mShowStates;

	[SerializeField]
	private MultiLanguageText mSmallLeftTimeText;

	[SerializeField]
	private Button mFullContentBtn;

	[SerializeField]
	private Button mSimpleContentBtn;

	protected override void Awake()
	{
		base.Awake();
		if (mFullContentBtn != null)
		{
			mFullContentBtn.onClick.AddListener(OnShowFullContent);
		}
		if (mSimpleContentBtn != null)
		{
			mSimpleContentBtn.onClick.AddListener(OnShowSimpleContent);
		}
		ResetAnim();
	}

	protected override void OnDestory()
	{
		if (mFullContentBtn != null)
		{
			mFullContentBtn.onClick.RemoveListener(OnShowFullContent);
		}
		if (mSimpleContentBtn != null)
		{
			mSimpleContentBtn.onClick.RemoveListener(OnShowSimpleContent);
		}
		base.OnDestory();
	}

	private void ResetAnim()
	{
		mShowStates.SetState(SwitchType.InstantToSimple);
	}

	protected override void SetLeftTimeText(string timeTxt)
	{
		base.SetLeftTimeText(timeTxt);
		mSmallLeftTimeText.SetKey(timeTxt);
	}

	private void OnShowFullContent()
	{
		mShowStates.SetState(SwitchType.SwitchToFull);
	}

	private void OnShowSimpleContent()
	{
		mShowStates.SetState(SwitchType.SwitchToSimple);
	}

	protected override void ShowContent()
	{
		base.ShowContent();
		ResetAnim();
	}
}
}
