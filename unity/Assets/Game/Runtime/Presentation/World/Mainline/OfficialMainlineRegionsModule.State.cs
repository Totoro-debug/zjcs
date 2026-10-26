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
        private sealed partial class OfficialMainlineRegionsModule
        {
            private OfficialMainlineRegionPolicy regionPolicy;
            public Dictionary<int, OfficialMainlineRegionConfig> regionConfigs => regionPolicy.regionConfigs;
            public readonly Dictionary<int, int> regionPoints = new Dictionary<int, int>();
            public Dictionary<int, int> regionPointLimits = new Dictionary<int, int>();
            public OfficialMainlineRegionFog regionFog;

            public void InitializeFog(Transform terrain)
            {
                if (!host.MapProfile.HasRegionFog) return;
                regionFog = new GameObject("Official region fog").AddComponent<OfficialMainlineRegionFog>();
                regionFog.transform.SetParent(host.mapRoot, false);
                regionFog.Initialize(host.worldCamera, terrain, IsPublicRegion, host.MapProfile);
                regionFog.BindCamera(host.miniMap.minimapCamera);
            }
        }
    }
}
