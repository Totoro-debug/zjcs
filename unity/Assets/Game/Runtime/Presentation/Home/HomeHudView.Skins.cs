using System;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private HomeSkinScreenController homeSkin;
        private bool returnToCharacterAfterHomeSkin;

        public void ShowHomeSkins(bool returnToCharacter = false)
        {
            if (homeSkin != null && homeSkin.IsOpen) return;
            returnToCharacterAfterHomeSkin = returnToCharacter;
            if (returnToCharacter && characterRoot != null) characterRoot.SetActive(false);
            try
            {
                homeSkin = new HomeSkinScreenController(host, this, CanvasTransform);
            }
            catch (Exception error)
            {
                homeSkin = null;
                SetStandardHomeHudVisible(true);
                if (returnToCharacter && characterRoot != null) characterRoot.SetActive(true);
                returnToCharacterAfterHomeSkin = false;
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
            if (returnToCharacterAfterHomeSkin && characterRoot != null)
            {
                characterRoot.SetActive(true);
                selectedTab = 0;
                ApplyTabVisuals();
            }
            returnToCharacterAfterHomeSkin = false;
        }

        private void OnCharacterNavigationRequested(string module)
        {
            if (NavigateSkillGiftSource(module)) return;
            if (string.Equals(module, "skill-gacha", StringComparison.Ordinal))
            {
                characterRoot.SetActive(false);
                host.OpenSkillGachaFromSource(() => {
                    characterRoot.SetActive(true);
                    characterRuntime.ReloadProfile();
                });
                return;
            }
            if (!string.Equals(module, "decoration-appearance", StringComparison.Ordinal)) return;
            ShowHomeSkins(true);
        }
    }
}
