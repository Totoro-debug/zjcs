// Selected original player-info implementation; only service boundaries and C# decompilation repairs differ.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerInfoUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HomeIntroductionPopBtn : UIBase
{
    private IHomePlayerInfoBindings InfoBindings;
    public void Configure(IHomePlayerInfoBindings bindings)
    {
        if (gameObject.activeInHierarchy) throw new InvalidOperationException("Bind the inactive original introduction button.");
        InfoBindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }

	[SerializeField]
	private Button mOpenBtn;

	private List<string> mContent;

	private HomeInfoTextKey mTitleTextArgs;

	public void Init(HomeInfoTextKey titleTextArgs, List<string> content)
	{
		InitWrapper(() =>
		{
			mTitleTextArgs = titleTextArgs;
			mContent = content;
		});
	}

	protected override void OnEnableAndInited()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		mOpenBtn.onClick.AddListener((UnityAction)OnClick);
	}

	protected override void OnDisableAndInited()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		mOpenBtn.onClick.RemoveListener((UnityAction)OnClick);
	}

	private void OnClick()
	{
		InfoBindings.PopupIntroduction(this, mTitleTextArgs, mContent, InfoBindings.PointerPosition);
	}
}

}
