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
        private OfficialMainlineUiCatalog nativeUi;
        private OfficialMainlineUiCatalog NativeUi => nativeUi ??= new OfficialMainlineUiCatalog(MapProfile);
        private OfficialMainlineInteractionCatalog interactionCatalog;
        private OfficialMainlineInteractionCatalog Interactions => interactionCatalog ??= OfficialMainlineInteractionCatalog.Load();
    }
}
