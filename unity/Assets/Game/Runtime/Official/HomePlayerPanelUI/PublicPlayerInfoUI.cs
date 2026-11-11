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
using System.Collections.Generic;
using UnityEngine;

public class PublicPlayerInfoUI : MonoBehaviour
{
	public enum UIState
	{
		Show,
		Hide
	}

	public WorldStateSwitcher<UIState> StateSwitcher;

	private HashSet<object> mReferenceSet = new HashSet<object>();

	public static PublicPlayerInfoUI sInstance { get; private set; }

	public bool IsShow { get; private set; } = true;

	public event Action OnShow;

	public event Action OnHide;

	private void Awake()
	{
		sInstance = this;
	}

	private void OnDestroy()
	{
		sInstance = null;
	}

	public void TryShow(object obj, bool instant = true)
	{
		if (mReferenceSet.Remove(obj))
		{
			UpdateState(instant);
		}
	}

	public void Hide(object obj, bool instant = true)
	{
		if (mReferenceSet.Add(obj))
		{
			UpdateState(instant);
		}
	}

	private void UpdateState(bool instant = true)
	{
		if (mReferenceSet.Count > 0)
		{
			if (instant)
			{
				((Component)this).gameObject.SetActive(false);
			}
			else
			{
				StateSwitcher.SetState(UIState.Hide);
			}
			IsShow = false;
			OnHide?.Invoke();
		}
		else
		{
			if (instant)
			{
				((Component)this).gameObject.SetActive(true);
			}
			else
			{
				StateSwitcher.SetState(UIState.Show);
			}
			IsShow = true;
			OnShow?.Invoke();
		}
	}
}

}
