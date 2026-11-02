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
                host.mapGridVisibility.UnlockRegion(region);
                host.SaveLocalProgress();
            }
        }
        private void InitializeNativeMap13()
        {
            byte[] saved=string.IsNullOrEmpty(localSave.map13GridVisibility)?null:Convert.FromBase64String(localSave.map13GridVisibility);
            RestoreMapVisibility(saved);
            map13NavigationSession=Map13NavigationAssets.LoadDefault().Open(localSave.map13WaterStates);
            worldCamera.transform.position=new Vector3(avatar.transform.position.x,avatar.transform.position.y,map11WorldViewSource.cameraZ);
            foreach(Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))renderer.sortingLayerName="Object";
            nativeMap13World=OfficialMap13NativeWorldView.Create(mapRoot,worldCamera,new Map13NativeState(this));
            AttachMapGridFog();
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

        private void RefreshNativeMap13State()
        {
            map13Trees?.Refresh();
            if(!nativeMap13World||ReferenceEquals(nativeMap13ObservedSave,progressionSave.Save))return;
            nativeMap13World.RefreshState();nativeMap13ObservedSave=progressionSave.Save;
        }
        private void DisposeNativeMap13()
        {
            DisposeMapVisibility();
            map13Trees?.Dispose();map13Trees=null;
            CleanMap13StoryPlayables();
            map13StoryPlayableScope?.Dispose();map13StoryPlayableScope=null;
            nativeMap13World?.Dispose();nativeMap13World=null;
            map13NavigationSession=null;
        }
    }
}
