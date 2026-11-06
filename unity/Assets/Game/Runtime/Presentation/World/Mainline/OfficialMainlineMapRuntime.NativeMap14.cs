using System;
using System.Collections;
using System.Linq;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Offline.World.MainlinePlayerState;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Presentation.World.NativeMap14;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap14NativeWorldView nativeMap14World;
        private float nativeMap14RefreshAt;
        private MainlineSharedWorld mainlineSharedWorld;
        private object mainlineSharedPlayer;
        private IEnumerator mainlineSharedLoad;
        private Coroutine mainlineSharedLoading;

        private void InitializeNativeMap14()
        {
            if (mapClassId != 14 || avatar == null || worldCamera == null)
                throw new InvalidOperationException("The native Map14 world requires its initialized player and camera.");
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            mainlineSharedWorld = new MainlineSharedWorld(catalog, mapClassId, mapClassId * 2L);
            mainlineSharedLoad = mainlineSharedWorld.LoadClient(
                region => regions.IsPublicRegion(region) ? 2 : 1, mapClassId * 2L + 1);
            mainlineSharedLoading = StartCoroutine(LoadNativeMap14World());
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
        }

        private IEnumerator LoadNativeMap14World()
        {
            while (mainlineSharedLoad.MoveNext()) yield return mainlineSharedLoad.Current;
            mainlineSharedLoad = null;
            var snapshots = new MainlineMapMiniMapSnapshots(OfficialWorldMapAssetCatalog.LoadDefault(),
                new ProfileSlotCatalog(persistentDataPath), slotId);
            // One local player; container identifiers keep the original private/public split.
            mainlineSharedPlayer = MainlineSharedPlayerProjection.Create(mainlineSharedWorld,
                progressionSave.Save, 1, map => map == mapClassId ? regions.regionPoints : snapshots.ReadRegionPoints(map));
            SynchronizeOriginalMapEntities();
            mainlineSharedWorld.StartClient(mainlineSharedPlayer);
            nativeMap14World = OfficialMap14NativeWorldView.Create(mapRoot, worldCamera,
                new MainlineNativeGroundState(this), mainlineSharedWorld);
        }

        private void SynchronizeOriginalMapEntities()
        {
            if (mainlineSharedWorld?.IsLoaded != true) return;
            mainlineSharedWorld.SynchronizeEntityStates(mapEntities.ToDictionary(pair => pair.Key,
                pair => new MainlineSharedEntityState(pair.Value.EntityClassId, pair.Value.State,
                    pair.Value.Grid.X, pair.Value.Grid.Y)));
            if (mainlineSharedPlayer != null) MainlineSharedPlayerProjection.SynchronizeRegionProgress(
                mainlineSharedPlayer, mapClassId, regions.regionPoints);
        }

        private void RefreshNativeMap14State()
        {
            if (nativeMap14World == null || Time.unscaledTime < nativeMap14RefreshAt) return;
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
            nativeMap14World.RefreshState();
        }

        private void DisposeNativeMap14()
        {
            if (mainlineSharedLoading != null) StopCoroutine(mainlineSharedLoading);
            mainlineSharedLoading = null;
            try
            {
                nativeMap14World?.Dispose();
                nativeMap14World = null;
            }
            finally
            {
                try { (mainlineSharedLoad as IDisposable)?.Dispose(); }
                finally
                {
                    mainlineSharedLoad = null;
                    mainlineSharedWorld?.Dispose();
                    mainlineSharedWorld = null;
                    mainlineSharedPlayer = null;
                }
            }
        }
    }
}
