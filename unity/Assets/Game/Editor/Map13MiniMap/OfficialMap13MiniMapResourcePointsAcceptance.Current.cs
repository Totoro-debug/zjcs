using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Offline.World.NativeMap13MiniMapResources;
using Object=UnityEngine.Object;
using Status=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMap13MiniMapResourcePointsAcceptance
    {
        private const string CurrentKey="Zjcs.Mainline.CurrentResourcePoints.Running";
        [Serializable] private sealed class CurrentSprite
        {
            public string path;public RectData rect;public float ppu;public Vector2 pivot;
            public Vector2[] vertices,uv;public int[] triangles;
        }
        [Serializable] private sealed class CurrentReceipt{public CurrentSprite[] sprite_geometry;}
        public static void RunCurrent(){SessionState.SetBool(CurrentKey,true);Run();}

        private static IEnumerator VerifyCurrent()
        {
            Require(!string.IsNullOrEmpty(Workspace)&&!string.IsNullOrEmpty(Repository),"Owned validation workspace required");
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            Require(pipeline,"Original URP pipeline");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            int totalBoards=0,totalPoints=0;
            int[] maps={10,11,12,13,14,16},counts={0,18,23,23,30,0};
            for(int index=0;index<maps.Length;index++)
            {
                int map=maps[index];
                var assets=Resources.Load<MiniMapResourcePointAssets>($"Map{map}/Native/current-resource-point-assets");
                Require(assets&&assets.ManagerPrefab&&assets.Boards.Count==counts[index],"Current original resource catalog: "+map);
                var receipt=JsonUtility.FromJson<CurrentReceipt>(File.ReadAllText(Path.Combine(Repository,$"assets/reference/official-map/map{map}-current-minimap-resources-source-manifest.json")));
                foreach(var expected in receipt.sprite_geometry)
                {
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(expected.path.Substring("unity/".Length));
                    Require(sprite&&sprite.rect==new Rect(expected.rect.x,expected.rect.y,expected.rect.width,expected.rect.height)&&sprite.pixelsPerUnit==expected.ppu&&Vector2.Distance(sprite.pivot,expected.pivot)<.0001f,"Original Sprite logical fields: "+expected.path);
                    Require(sprite.triangles.Select(v=>(int)v).SequenceEqual(expected.triangles)&&sprite.vertices.Length==expected.vertices.Length&&sprite.uv.Length==expected.uv.Length,"Original Sprite geometry dimensions: "+expected.path);
                    for(int v=0;v<expected.vertices.Length;v++)Require(Vector2.Distance(sprite.vertices[v],expected.vertices[v])<.00001f&&Vector2.Distance(sprite.uv[v],expected.uv[v])<.00001f,"Original Sprite mesh/Atlas UVs: "+expected.path);
                }
                var labels=new Labels();labels.Actor.MapClassId=map;
                var context=new Observed(new LocalMiniMapResourcePointContext(assets,labels));
                Require(context.MapClassId==map,"Resource map identity must come from its actual configuration");
                var host=new GameObject("CurrentResourceManager"+map);host.SetActive(false);
                var manager=Object.Instantiate(assets.ManagerPrefab,host.transform,false).GetComponent<MiniMapResourcePointManager>();
                manager.Init(context);host.SetActive(true);yield return null;
                Require(manager.MapClassId==map&&manager.ForeachLayer().Select(v=>v.Key).SequenceEqual(assets.Boards.Select(v=>v.RefreshGroup)),"Original resource layer order and map identity");
                var data=JsonUtility.FromJson<Data>(assets.SourceData.text);
                var available=new HashSet<int>(assets.Boards.Select(v=>v.RefreshGroup));var actual=new HashSet<int>();
                foreach(var item in data.ItemLocations.Where(v=>v.Data!=null))
                {
                    var expected=new HashSet<int>();foreach(int rule in item.Data.RuleIds??Array.Empty<int>())
                    {var location=data.WorldLocations.FirstOrDefault(v=>v.Type==2&&v.Id==rule);if(location!=null)expected.UnionWith(location.RefreshGroupSet.Where(available.Contains));}
                    manager.GetRefreshGroupSetByItemId(item.ItemId,actual);Require(actual.SetEquals(expected),"Original item lookup/filter: "+map+"/"+item.ItemId);
                }
                foreach(var location in data.WorldLocations.Where(v=>v.Type==1))
                {manager.GetRefreshGroupSetByMonsterId(location.Id,actual);Require(actual.SetEquals(location.RefreshGroupSet.Where(available.Contains)),"Original monster lookup/filter: "+map+"/"+location.Id);}
                int points=0;foreach(var board in assets.Boards)
                {
                    Require(board.Prefab.GetComponent<MiniMapResourcePointBoard>().RefreshGroup==board.RefreshGroup,"Original board group");
                    foreach(var transform in board.Prefab.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)==0,"Missing original board script");
                    points+=board.Prefab.GetComponentsInChildren<MiniMapResourcePointTapItem>(true).Length;
                    manager.Select(board.RefreshGroup);yield return null;yield return null;
                    Require(manager.IsBoardActive(board.RefreshGroup),"Original selection: "+map+"/"+board.RefreshGroup);
                }
                Require(context.PrefabLoads==assets.Boards.Count&&context.Created==assets.Boards.Count&&host.GetComponentsInChildren<MiniMapResourcePointTapItem>().Length==points,"Each original public board/point loaded once");
                foreach(var board in host.GetComponentsInChildren<MiniMapResourcePointBoard>())
                {
                    string path=data.Groups.Single(v=>v.Id==board.RefreshGroup).Icon;var sprite=assets.Icons.Single(v=>v.Path==path).Sprite;
                    foreach(var point in board.GetComponentsInChildren<MiniMapResourcePointTapItem>())Require(MarkerRenderer(point).sprite==sprite,"Original configured point icon");
                    var serialized=new SerializedObject(board);var rows=serialized.FindProperty("mPointDatas");
                    for(int p=0;p<rows.arraySize;p++){var row=rows.GetArrayElementAtIndex(p);int area=row.FindPropertyRelative("Area").intValue;Require(context.AreaToRegion(map,area)>=0&&context.AreaToRegion(map+1000,area)==-1,"Map-specific area to region");Require(manager.GetSelectResourcePointDatas(board.RefreshGroup,area).Active,"Original public area point");}
                }
                labels.Actor.Set(Status.Private);yield return null;Require(host.GetComponentsInChildren<MiniMapResourcePointTapItem>().Length==0,"Private regions hide resource points");
                labels.Actor.Set(Status.None);yield return null;Require(host.GetComponentsInChildren<MiniMapResourcePointTapItem>().Length==0,"Unvisited regions hide resource points");
                labels.Actor.Set(Status.Public);yield return null;Require(host.GetComponentsInChildren<MiniMapResourcePointTapItem>().Length==points,"Map-specific public status event restores points");
                if(assets.Boards.Count>0)
                {
                    int selected=assets.Boards[0].RefreshGroup;manager.CancelSelect(selected,true);Require(manager.IsBoardActive(selected),"Guide cancellation preserves explicit selection");manager.CancelSelect(selected);yield return null;Require(!manager.IsBoardActive(selected),"Explicit cancellation hides board");int loaded=context.PrefabLoads;manager.Select(selected);yield return null;Require(context.PrefabLoads==loaded,"Cached board reuse");
                    var render=RenderCurrent(host,map);while(render.MoveNext())yield return render.Current;
                }
                manager.Select(-98765);Require(context.PrefabLoads==assets.Boards.Count,"Unknown/empty layers do not create boards");
                int acquired=context.PrefabLoads+context.SpriteLoads;Object.Destroy(host);yield return null;
                Require(context.Disposed==acquired,"Every original asset lease released");context.Dispose();
                totalBoards+=assets.Boards.Count;totalPoints+=points;
                Debug.Log($"Current original Map{map} resource manager passed: {assets.Boards.Count} boards, {points} points, {assets.Icons.Count} configured icons.");
            }
            Debug.Log($"Mainline current original resource-point Play Mode passed: six maps, {totalBoards} boards/{totalPoints} points; exact Sprite geometry, local status events, item/monster lookups, cache and disposal. Formal HUD/Player remains separate.");
        }

        private static IEnumerator RenderCurrent(GameObject host,int map)
        {
            var renderers=host.GetComponentsInChildren<SpriteRenderer>();Require(renderers.Length>0,"Original resource renderers");
            Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var camera=new GameObject("ResourceCamera"+map,typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.aspect=.75f;camera.orthographicSize=Math.Max(bounds.extents.y,bounds.extents.x/.75f)*1.04f;camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-250);camera.nearClipPlane=.3f;camera.farClipPlane=500;camera.cullingMask=1<<20;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(768,1024,24);camera.targetTexture=target;for(int i=0;i<8;i++)yield return null;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(768,1024,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,768,1024),0,0);image.Apply(false,false);RenderTexture.active=previous;
            Require(image.GetPixels32().Count(p=>p.r>50||p.g>50||p.b>50)>100,"Actual original resource point rendering: "+map);
            File.WriteAllBytes(Path.Combine(Workspace,$"map{map}-current-resource-points.png"),image.EncodeToPNG());
            camera.targetTexture=null;Object.Destroy(image);Object.Destroy(target);Object.Destroy(camera.gameObject);yield return null;
        }
    }
}
