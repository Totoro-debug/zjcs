namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System;
using UnityEngine;

public class ProfessionRedDotItemBase : ProfessionPlayerUIBase
{
	public GameObject myRedDotObj;

	private bool mIsDirty;


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
		IProfessionTipDotSystem tipDotSystem = Bindings.TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Combine(tipDotSystem.OnTipDotShowChanged, new Action(RefreshTipDotShowState));
		SetDirty();
	}

	protected override void OnDisableAndInited()
	{
		IsMeet = null;
		IProfessionTipDotSystem tipDotSystem = Bindings.TipDotSystem;
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
			myRedDotObj.SetActive(Bindings.TipDotSystem.IsShowTipDot & isActive);
		}
	}
}

}
