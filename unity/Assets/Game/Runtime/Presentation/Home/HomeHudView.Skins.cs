using System;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private HomeSkinScreenController homeSkin;

        public void ShowHomeSkins()
        {
            if (homeSkin != null && homeSkin.IsOpen) return;
            try
            {
                homeSkin = new HomeSkinScreenController(host, this, CanvasTransform);
            }
            catch (Exception error)
            {
                homeSkin = null;
                SetStandardHomeHudVisible(true);
                Debug.LogWarning("Home skin page unavailable: " + error.Message);
                Notify("装潢页面打开失败：" + error.Message);
            }
        }

        internal void SetStandardHomeHudVisible(bool visible)
        {
            if (homePanel != null) homePanel.SetActive(visible);
            if (bottomRenderer != null && !string.IsNullOrEmpty(bottomRenderer.RootName))
                bottomRenderer.Find("/" + bottomRenderer.RootName)?.SetActive(visible);
            if (functionsPanel != null) functionsPanel.SetActive(visible && functionsPanel.activeSelf);
        }

        internal void HomeSkinClosed(HomeSkinScreenController controller)
        {
            if (homeSkin == controller) homeSkin = null;
            SetStandardHomeHudVisible(true);
        }
    }
}
