using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.OfflineWorld;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineHudModule
        {
            public OfficialMap10UiCatalog uiCatalog;
            public OfficialWorldUiRenderer ui;
            private Coroutine noticeRoutine;
            public CommerceScreenController commerceScreen;
            public GameObject firstPayEntry;

            public void Mount()
            {
                uiCatalog = OfficialMap10UiCatalog.LoadDefault();
                ui = new OfficialWorldUiRenderer(uiCatalog);
                ui.Mount(host.uiParent, BindWorldControl, host.mapClassId,
                    host.MapProfile.HasNativeUi ? host.NativeUi.Data : null);
                if (host.MapProfile.HasNativeUi) InitializeNativeWorldHud();
                ui.SetPanelActive("WorldCanvas", true);
                ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText", host.characterName);
                ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/LevelBgImg/LevelTxt",
                    host.progression.Player.Level.ToString(CultureInfo.InvariantCulture) + "级");
                ui.SetPanelActive("TalkDialog", false);
                CreateCommerceHudEntries();
            }
        }
    }
}
