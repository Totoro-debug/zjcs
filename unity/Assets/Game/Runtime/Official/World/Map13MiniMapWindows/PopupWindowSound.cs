using System;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
public class PopupWindowSound : MonoBehaviour
{
	public OfficialWwiseEvent ShowSound;

	public OfficialWwiseEvent HideSound;

	private PopupWindow mPopupWindow;

	public PopupWindow PopupWindow
	{
		get
		{
			if ((Object)(object)mPopupWindow == (Object)null)
			{
				mPopupWindow = ((Component)this).GetComponentInParent<PopupWindow>();
			}
			return mPopupWindow;
		}
	}

	private void Awake()
	{
		PopupWindow.OnShowing += new Action<PopupWindow>(OnShowing);
		PopupWindow.OnHiding += new Action<PopupWindow>(OnHiding);
	}

	private void OnDestroy()
	{
		PopupWindow.OnShowing -= new Action<PopupWindow>(OnShowing);
		PopupWindow.OnHiding -= new Action<PopupWindow>(OnHiding);
	}

	public void OnShowing(PopupWindow window)
	{
		ShowSound.Post(((Component)this).gameObject);
	}

	public void OnHiding(PopupWindow window)
	{
		HideSound.Post(((Component)this).gameObject);
	}
}

}
