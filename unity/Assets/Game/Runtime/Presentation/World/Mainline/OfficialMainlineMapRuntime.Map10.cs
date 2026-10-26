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
        private void StartFirstBattleIntroduction()
        {
            if (HasStory("FirstBattle") || localSave.firstBattleIntroShown || dialogueWaiting
                || movementPath != null || battleController != null)
                return;
            if (firstBattleNpc == null)
                firstBattleNpc = entityView.SpawnStoryEntity(8402,
                    "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view", new Vector2(23, 23));
            PlayStory("FirstBattle", 0, () =>
            {
                movementCompletion = () => PlayStory("FirstBattle", 1, () =>
                {
                    localSave.firstBattleIntroShown = true;
                    SaveLocalProgress();
                });
                StartMovement(new MapGridPoint(23, 22), null);
            });
        }

        private MapGridPoint FindAdjacentStoryGrid()
        {
            MapGridPoint[] candidates =
            {
                new MapGridPoint(currentGrid.X, currentGrid.Y + 1),
                new MapGridPoint(currentGrid.X + 1, currentGrid.Y),
                new MapGridPoint(currentGrid.X, currentGrid.Y - 1),
                new MapGridPoint(currentGrid.X - 1, currentGrid.Y)
            };
            foreach (MapGridPoint candidate in candidates)
            {
                if (Manhattan(currentGrid, candidate) != 1) continue;
                List<MapGridPoint> path = session.FindPath(new OfficialMapPathRequest
                {
                    Start = currentGrid,
                    End = candidate,
                    PreciseTarget = true
                });
                if (path != null && path.Count > 0) return candidate;
            }
            throw new InvalidOperationException("Map10 has no free adjacent cell for the official story NPC.");
        }
    }
}
