using UnityEngine;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.World.Map13MiniMapWindows
{
    // Explicit host ports for the original window-manager events and close call.
    public interface IMap13PopupWindowHost
    {
        GAction<GameObject> OnWindowsOpening { get; }
        GAction<GameObject> OnWindowOpened { get; }
        GAction<GameObject> OnWindowClosing { get; }
        GAction<GameObject> OnWindowClosed { get; }
        void Close(GameObject window,bool instant);
    }
    public static class Map13WindowLanguage
    {
        public static string GetTipNumberWithPlus(long num, long limit = 99L)
	{
		if (num > 99)
		{
			return Zjcs.Official.World.Map13MiniMap.MiniMapMarkLanguage.Format("ui_common_count_plus", new object[] { limit });
		}
		return num.ToString();
	}
    }
}
