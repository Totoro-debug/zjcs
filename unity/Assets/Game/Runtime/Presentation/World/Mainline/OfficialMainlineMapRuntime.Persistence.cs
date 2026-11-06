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
        private void LoadLocalProgress()
        {
            int width = session == null ? (mapClassId == 11 ? 256 : 60)
                : Mathf.CeilToInt(session.Definition.Navigation.Width);
            int height = session == null ? (mapClassId == 11 ? 256 : 60)
                : Mathf.CeilToInt(session.Definition.Navigation.Height);
            localSave = MainlineMapLocalSaveFile.Read(mapSavePath, slotId, mapClassId, width, height);
            localSaveCreated = localSave == null;
            if (localSaveCreated)
            {
                localSave = new Map10LocalSave { slotId = slotId, gridX = 24, gridY = 23 };
                MainlineMapLocalSaveFile.Normalize(localSave);
            }
            RecoverMap12ChestCompletions();
            RecoverMap12SteleCompletions();
        }

        private void SaveLocalProgress()
        {
            if (localSave == null || string.IsNullOrEmpty(mapSavePath)) return;
            localSave.gridX = navigation.currentGrid.X;
            localSave.gridY = navigation.currentGrid.Y;
            if (puzzles != null) localSave.puzzleStates = puzzles.Save();
            SnapshotMapVisibility();
            if (map13NavigationSession != null) localSave.map13WaterStates = map13NavigationSession.Water.Save();
            LocalJsonSaveFile.Write(mapSavePath, JsonUtility.ToJson(localSave, true), ValidLocalSave);
            SynchronizeOriginalMapEntities();
        }

        private bool ValidLocalSave(string json)
        {
            int width = session == null ? (mapClassId == 11 ? 256 : 60)
                : Mathf.CeilToInt(session.Definition.Navigation.Width);
            int height = session == null ? (mapClassId == 11 ? 256 : 60)
                : Mathf.CeilToInt(session.Definition.Navigation.Height);
            return MainlineMapLocalSaveFile.IsValid(json, slotId, mapClassId, width, height);
        }
    }
}
