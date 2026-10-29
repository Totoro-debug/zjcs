using System.Collections.Generic;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
public class WindowDependence
{
	public PopupWindow Parent;

	public List<PopupWindow> Children = new List<PopupWindow>();

	public void SetParent(PopupWindow window)
	{
		Parent = window;
	}

	public void AddChild(PopupWindow window)
	{
		Children.Add(window);
	}

	public void RemoveChild(PopupWindow window)
	{
		Children.Remove(window);
	}
}

}
