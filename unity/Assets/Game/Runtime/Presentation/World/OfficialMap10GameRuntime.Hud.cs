using System;
using System.Linq;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        private void InitializeNativeWorldHud()
        {
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map11/UI/catalog").text);
            Map10RuntimeWorldInfo world = GetWorldInfo();
            foreach (OfficialMap11TeamPanelBinding binding in data.worldBindings.teamPanels)
                ui.Find("WorldCanvas", binding.path).GetComponent<TeamMiniPanel>().Refresh(
                    world.Type != 0 && world.AllowPrivateTeam, world.AllowPublicTeam, false);
            // No message has been sent when entering the local world. The original
            // cooldown controller owns subsequent countdown rendering.
            foreach (string path in data.worldBindings.quickMessageMasks)
                ui.Find("WorldCanvas", path).GetComponent<ChatQuickMsgEntryPanel>().Refresh(0, 0);
        }
    }
}
