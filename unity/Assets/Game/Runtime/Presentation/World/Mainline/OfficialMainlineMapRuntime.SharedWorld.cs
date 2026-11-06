using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Offline.World.MainlinePlayerState;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private MainlineSharedWorld mainlineSharedWorld;
        private object mainlineSharedPlayer;
        private object mainlineSharedPartInfo;
        private IEnumerator mainlineSharedLoad;
        private Coroutine mainlineSharedLoading;

        private void InitializeMainlineSharedWorld()
        {
            if (avatar == null || worldCamera == null || mainlineSharedWorld != null)
                throw new InvalidOperationException("Start one original shared world after initializing the map player and camera.");
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            mainlineSharedWorld = new MainlineSharedWorld(catalog, mapClassId, mapClassId * 2L);
            mainlineSharedLoad = mainlineSharedWorld.LoadClient(OriginalRegionStatus, mapClassId * 2L + 1);
            mainlineSharedLoading = StartCoroutine(LoadMainlineSharedWorld());
        }

        // Follow the original PlayerMap fallback for maps with one supported
        // status. Mixed maps consume the saved goddess/public-region records.
        private int OriginalRegionStatus(int region)
        {
            if (mainlineSharedPartInfo == null)
            {
                var type = mainlineSharedWorld.Assemblies.CommonType("MapInfoManager");
                mainlineSharedPartInfo = type.GetMethod("GetMapPartInfo").Invoke(
                    type.GetProperty("Instance").GetValue(null), new object[] { mapClassId });
            }
            int status = regions.IsPublicRegion(region) ? 2 : 1;
            var originalStatus = Enum.ToObject(mainlineSharedWorld.Assemblies.CommonType("MapRegionStatus"), status);
            bool supported = (bool)mainlineSharedPartInfo.GetType().GetMethod("RegionStatusSupported")
                .Invoke(mainlineSharedPartInfo, new[] { originalStatus });
            return supported ? status : Convert.ToInt32(NativeMap14SharedPrefabs.Get(mainlineSharedPartInfo, "MapDefaultRegionStatus"));
        }

        private IEnumerator LoadMainlineSharedWorld()
        {
            bool completed = false;
            try
            {
                while (mainlineSharedLoad.MoveNext()) yield return mainlineSharedLoad.Current;
                (mainlineSharedLoad as IDisposable)?.Dispose();
                mainlineSharedLoad = null;
                var snapshots = new MainlineMapMiniMapSnapshots(OfficialWorldMapAssetCatalog.LoadDefault(),
                    new ProfileSlotCatalog(persistentDataPath), slotId);
                mainlineSharedPlayer = MainlineSharedPlayerProjection.Create(mainlineSharedWorld,
                    progressionSave.Save, 1, map => map == mapClassId ? regions.regionPoints : snapshots.ReadRegionPoints(map));
                SynchronizeOriginalMapEntities();
                mainlineSharedWorld.StartClient(mainlineSharedPlayer, navigation.currentGrid.X, navigation.currentGrid.Y);
                if (mapClassId == 14) InitializeNativeMap14();
                completed = true;
            }
            finally
            {
                mainlineSharedLoading = null;
                if (!completed) DisposeMainlineSharedWorld();
            }
        }

        private void SynchronizeOriginalMapEntities()
        {
            if (mainlineSharedWorld?.IsLoaded != true) return;
            if (mainlineSharedPlayer != null) MainlineSharedPlayerProjection.SynchronizeActivations(
                mainlineSharedWorld, mainlineSharedPlayer, progressionSave.Save);
            if (mainlineSharedPlayer != null) MainlineSharedPlayerProjection.SynchronizeRegionProgress(
                mainlineSharedPlayer, mapClassId, regions.regionPoints);
            var presentation = CreateEntityPresentation();
            var states = mapEntities.ToDictionary(pair => pair.Key,
                pair => new MainlineSharedEntityState(pair.Value.EntityClassId,
                    presentation.Read(pair.Value, pair.Value.State, pair.Value.Active).State,
                    pair.Value.Grid.X, pair.Value.Grid.Y));
            if (mainlineSharedWorld.IsReady)
            {
                mainlineSharedWorld.SynchronizeClientState(states, OriginalRegionStatus);
                mainlineSharedWorld.SynchronizeClientCharacter(navigation.currentGrid.X, navigation.currentGrid.Y);
            }
            else mainlineSharedWorld.SynchronizeEntityStates(states);
        }

        private void DisposeMainlineSharedWorld()
        {
            var loading = mainlineSharedLoading;
            mainlineSharedLoading = null;
            if (loading != null) StopCoroutine(loading);
            try { (mainlineSharedLoad as IDisposable)?.Dispose(); }
            finally
            {
                mainlineSharedLoad = null;
                try { mainlineSharedWorld?.Dispose(); }
                finally
                {
                    mainlineSharedWorld = null;
                    mainlineSharedPlayer = null;
                    mainlineSharedPartInfo = null;
                }
            }
        }
    }
}
