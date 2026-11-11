// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using UnityEngine;

public class HomePanelRedDotItemBase : HomePlayerPanelUIBase
{
	public GameObject myRedDotObj;

	private bool mIsDirty;

	private IProfessionTipDotSystem TipDotSystem => PanelBindings.TipDotSystem;

	public bool IsShow
	{
		get
		{
			if (!IsActive)
			{
				return false;
			}
			if (!IsMeet.HasValue)
			{
				return false;
			}
			return IsMeet.Value;
		}
	}

	public bool IsActive { get; set; } = true;

	public bool? IsMeet { get; protected set; }

	public event Action<bool> OnRedDotChanged;

	private void Awake()
	{
	}

	public virtual bool ShowRedDot()
	{
		return false;
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		IProfessionTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Combine(tipDotSystem.OnTipDotShowChanged, new Action(RefreshTipDotShowState));
		SetDirty();
	}

	protected override void OnDisableAndInited()
	{
		IsMeet = null;
		IProfessionTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Remove(tipDotSystem.OnTipDotShowChanged, new Action(RefreshTipDotShowState));
		base.OnDisableAndInited();
	}

	public virtual void Refresh()
	{
		bool flag = ShowRedDot();
		bool flag2 = flag && IsActive;
		SetRedDotActive(flag2);
		if (!IsMeet.HasValue || flag != IsMeet)
		{
			IsMeet = flag;
			OnRedDotChanged?.Invoke(flag2);
		}
	}

	public void SetDirty()
	{
		mIsDirty = true;
	}

	protected virtual void Update()
	{
		if (mIsDirty && IsEnableAndInited())
		{
			mIsDirty = false;
			Refresh();
		}
	}

	public void SetRedDotGameObject(GameObject go)
	{
		myRedDotObj = go;
		SetRedDotActive(IsShow);
	}

	public void ClearRedDotGameObject()
	{
		SetRedDotActive(isActive: false);
		myRedDotObj = null;
	}

	private void RefreshTipDotShowState()
	{
		Refresh();
	}

	public void SetRedDotActive(bool isActive)
	{
		if (!((Object)(object)myRedDotObj == (Object)null))
		{
			myRedDotObj.SetActive(TipDotSystem.IsShowTipDot & isActive);
		}
	}
}

}
