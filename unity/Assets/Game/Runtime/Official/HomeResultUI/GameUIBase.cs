// Original selected HomeUI result notification; service/type bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeResultUI
{
using System;

public class GameUIBase : UIBase
{
	protected bool myInited;

	private IHomeResultFight mGame;

	public IHomeResultFightRoot mFightRoot { protected get; set; }

	protected IHomeResultFight myGame
	{
		get
		{
			if (mGame != null)
			{
				return mGame;
			}
			if (mFightRoot.HasFightRoot)
			{
				mGame = mFightRoot.Fight;
			}
			return mGame;
		}
	}

	protected override void Start()
	{
		base.Start();
		if (mFightRoot.HasFightRoot)
		{
			if (mFightRoot.Initialized)
			{
				Instance_OnGameInited();
			}
			mFightRoot.OnInitialized += new Action(Instance_OnGameInited);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (mFightRoot.HasFightRoot)
		{
			mFightRoot.OnInitialized -= new Action(Instance_OnGameInited);
		}
	}

	private void Instance_OnGameInited()
	{
		if (mFightRoot.HasFightRoot && !(mFightRoot.Fight == null))
		{
			InitWrapper(null);
		}
	}
}
}
