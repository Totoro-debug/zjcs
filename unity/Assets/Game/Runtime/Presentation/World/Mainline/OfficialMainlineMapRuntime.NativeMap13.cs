using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Offline.World.Map13Navigation;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Presentation.World.NativeMap13;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap13NativeWorldView nativeMap13World;
        private OfflineMapGridVisibility map13GridVisibility;
        private OfflineMap13NavigationSession map13NavigationSession;
        private OfflineMainlineSave nativeMap13ObservedSave;
        private sealed class Map13NativeState:IMap13NativeWorldState
        {
            private readonly OfficialMainlineMapRuntime host;
            public Map13NativeState(OfficialMainlineMapRuntime host){this.host=host;}
            public Vector3 PlayerWorldPosition=>host.avatar.transform.position;
            public int PlayerLevel=>host.progression.Player.Level;
            public int PlayerSubRank=>host.progression.Player.SubRank;
            public long PlayerCombatRating=>host.progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating;
            public bool IsPublicRegion(int region)=>host.regions.IsPublicRegion(region);
            public int GetRegionProgress(int region)=>host.regions.regionPoints.TryGetValue(region,out int points)?points:0;
            public int GetRegionGoddessId(int region)=>host.mapEntities.Values.SingleOrDefault(e=>e.Region==region&&e.TypeId==202)?.PlacementId??0;
            public void UnlockAllRegionPositions(int region)
            {
                host.map13GridVisibility.UnlockRegion(region);
                host.SaveLocalProgress();
            }
        }
        private void InitializeNativeMap13()
        {
            byte[] saved=string.IsNullOrEmpty(localSave.map13GridVisibility)?null:Convert.FromBase64String(localSave.map13GridVisibility);
            RestoreMap13Visibility(saved);
            map13NavigationSession=Map13NavigationAssets.LoadDefault().Open(localSave.map13WaterStates);
            worldCamera.transform.position=new Vector3(avatar.transform.position.x,avatar.transform.position.y,map11WorldViewSource.cameraZ);
            foreach(Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))renderer.sortingLayerName="Object";
            nativeMap13World=OfficialMap13NativeWorldView.Create(mapRoot,worldCamera,new Map13NativeState(this));
            nativeMap13ObservedSave=progressionSave.Save;
            SaveLocalProgress();
        }
        private List<MapGridPoint> FindMainlinePath(OfficialMapPathRequest request) =>
            mapClassId == 13 && map13NavigationSession != null
                ? map13NavigationSession.FindPlayerPath(request)
                : session.FindPath(request);

        private bool IsMainlineGridFit(MapGridPoint point) =>
            mapClassId == 13 && map13NavigationSession != null
                ? map13NavigationSession.CreateGround(session.Definition.CharacterMover.MoveMask).IsFit(point)
                : session.Movement.IsFit(point);

        private void RestoreMap13Visibility(byte[] saved)
        {
            var labels=JsonUtility.FromJson<Map13LabelData>(Resources.Load<TextAsset>("Map13/Native/region-label-data").text);
            int RegionAt(Pos2d p)=>p.X<0||p.Y<0||p.X>=labels.width||p.Y>=labels.height?-1:labels.regionGrid[p.Y*labels.width+p.X];
            var configuration=JsonUtility.FromJson<OfflineMapGridVisibility.Configuration>(Resources.Load<TextAsset>("Map13/grid-visibility").text);
            var bodies=JsonUtility.FromJson<OfflineMapGridVisibility.BodyConfiguration>(Resources.Load<TextAsset>("Map13/grid-entities").text);
            // These are facts of the migrated Map13 payload, not restrictions on
            // the shared visibility module or on other maps' configurations.
            if(configuration.manifest!="871_156598"||configuration.mapClassId!=13
                ||configuration.size?.Length!=2||configuration.size[0]!=240||configuration.size[1]!=240
                ||configuration.viewRange!=5||configuration.regions?.Length!=106)
                throw new InvalidDataException("Original Map13 visibility configuration is unbound.");
            configuration.characterClassId=3001;
            var publicRegions=progressionSave.Save.World.ActivatedGoddessRegions.Where(r=>r.MapClassId==13).Select(r=>r.Region);
            map13GridVisibility?.Dispose();
            map13GridVisibility=new OfflineMapGridVisibility(configuration,bodies,new Pos2d(navigation.currentGrid.X,navigation.currentGrid.Y),
                saved,publicRegions,RegionAt,id=>mapEntities.TryGetValue(id,out var entity)&&entity.Active
                    &&!localSave.completedEntityPlacements.Contains(id)&&!IsMapLogicEntityDestroyed(id));
        }
        private void RefreshNativeMap13State()
        {
            map13Trees?.Refresh();
            if(!nativeMap13World||ReferenceEquals(nativeMap13ObservedSave,progressionSave.Save))return;
            nativeMap13World.RefreshState();nativeMap13ObservedSave=progressionSave.Save;
        }
        private void SnapshotMap13Visibility()
        {
            if(map13GridVisibility==null)return;
            map13GridVisibility.MovePlayer(new Pos2d(navigation.currentGrid.X,navigation.currentGrid.Y));
            localSave.map13GridVisibility=Convert.ToBase64String(map13GridVisibility.Snapshot());
        }
        private static bool ValidMap13Visibility(string encoded)
        {
            if(string.IsNullOrEmpty(encoded))return true;
            if(encoded.Length>87384)return false;
            byte[] bytes=Convert.FromBase64String(encoded);
            return OfflineMapGridVisibility.IsValidSnapshot(bytes,240,240);
        }
        private void DisposeNativeMap13()
        {
            map13Trees?.Dispose();map13Trees=null;
            CleanMap13StoryPlayables();
            map13StoryPlayableScope?.Dispose();map13StoryPlayableScope=null;
            nativeMap13World?.Dispose();nativeMap13World=null;
            map13GridVisibility?.Dispose();map13GridVisibility=null;
            map13NavigationSession=null;
        }
    }
}
