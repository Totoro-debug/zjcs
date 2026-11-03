using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Zjcs.Official.World.Map13MiniMapWindows;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{

public class MiniMapPanelCloseWindowBtn : MonoBehaviour
{
	public MiniMapFullScreenContext Context { get; set; }
	public enum CloseWindowType
	{
		Parent,
		Specified,
		Last
	}

	public Button myButton;

	public CloseWindowType CloseType;

	public PopupWindow mySpecifiedWindow;

	private PopupWindow myCloseWindow;

	private void Reset()
	{
		myButton = ((Component)this).GetComponent<Button>();
	}

	private void Awake()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		((UnityEvent)myButton.onClick).AddListener((UnityAction)HandleOnButtonClick);
		if ((Object)(object)myCloseWindow == (Object)null)
		{
			switch (CloseType)
			{
			case CloseWindowType.Parent:
				myCloseWindow = ((Component)this).GetComponentInParent<PopupWindow>();
				break;
			case CloseWindowType.Specified:
				myCloseWindow = mySpecifiedWindow;
				break;
			}
		}
		if ((Object)(object)myCloseWindow != (Object)null)
		{
			myCloseWindow.OnWindowDestory += new Action(PopupWindow_OnWindowDestory);
		}
	}

	private void HandleOnButtonClick()
	{
		switch (CloseType)
		{
		case CloseWindowType.Last:
			Context.CloseLastWindow();
			break;
		case CloseWindowType.Parent:
		case CloseWindowType.Specified:
			Context.CloseWindow(((Component)myCloseWindow).gameObject);
			break;
		}
	}

	public void SetSpecifiedWindow(PopupWindow window)
	{
		if ((Object)(object)myCloseWindow != (Object)null)
		{
			myCloseWindow.OnWindowDestory -= new Action(PopupWindow_OnWindowDestory);
		}
		myCloseWindow = window;
		myCloseWindow.OnWindowDestory += new Action(PopupWindow_OnWindowDestory);
		CloseType = CloseWindowType.Specified;
	}

	private void PopupWindow_OnWindowDestory()
	{
		if ((Object)(object)myCloseWindow != (Object)null)
		{
			myCloseWindow.OnWindowDestory -= new Action(PopupWindow_OnWindowDestory);
		}
		myCloseWindow = null;
	}
}
}
