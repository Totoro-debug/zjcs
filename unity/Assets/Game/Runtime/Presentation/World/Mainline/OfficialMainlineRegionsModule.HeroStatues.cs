using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineRegionsModule
        {
            public bool IsPublicRegion(int region) => host.progressionSave.Save.World.ActivatedGoddessRegions.Any(value =>
                value.MapClassId == host.mapClassId && value.Region == region);
        }
    }
}
