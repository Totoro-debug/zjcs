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
        private sealed partial class OfficialMainlineNavigationModule
        {
            private const float MovementSpeed = 4f;
            public Map10RuntimeEntity pendingInteraction;
            public List<MapGridPoint> movementPath;
            private int movementIndex;
            public bool IsMoving => movementPath != null;
            public Action movementCompletion;
            public MapGridPoint currentGrid;
            public bool storyTransferActive;
            public bool storyMovementLocked;
        }
    }
}
