using System;
using Zjcs.Official.WorldUI;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
public class PopupWindowSound : MonoBehaviour
{
	public OfficialWwiseEvent ShowSound;

	public OfficialWwiseEvent HideSound;

	private WorldPopupWindow mPopupWindow;

	public WorldPopupWindow PopupWindow
	{
		get
		{
			if ((UnityEngine.Object)(object)mPopupWindow == (UnityEngine.Object)null)
			{
				mPopupWindow = ((Component)this).GetComponentInParent<WorldPopupWindow>();
			}
			return mPopupWindow;
		}
	}

	private void Awake()
	{
		PopupWindow.OnShowing += new Action<WorldPopupWindow>(OnShowing);
		PopupWindow.OnHiding += new Action<WorldPopupWindow>(OnHiding);
	}

	private void OnDestroy()
	{
		PopupWindow.OnShowing -= new Action<WorldPopupWindow>(OnShowing);
		PopupWindow.OnHiding -= new Action<WorldPopupWindow>(OnHiding);
	}

	public void OnShowing(WorldPopupWindow window)
	{
		ShowSound.Post(((Component)this).gameObject);
	}

	public void OnHiding(WorldPopupWindow window)
	{
		HideSound.Post(((Component)this).gameObject);
	}
}
}
