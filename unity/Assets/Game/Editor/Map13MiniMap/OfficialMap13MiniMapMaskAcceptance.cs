using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeTerrain.Common;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapMaskAcceptance
    {
        private const string Key = "Zjcs.Map13.MiniMapMask.Running";
        private static int errors;
        [Serializable] private sealed class Fixture { public string manifest; public int mapClassId; public Patterns patterns; public Case[] cases; }
        [Serializable] private sealed class Patterns { public Pattern[] mCornerPatterns, mEdgePatterns; }
        [Serializable] private sealed class Pattern { public string tile; public Condition DefaultCondition; }
        [Serializable] private sealed class Condition { public Point[] PosDatas; }
        [Serializable] private sealed class Point { public Pos2d Pos; public bool Locked; }
        [Serializable] private sealed class Case { public int mask, rotation; public string tile; }
        [Serializable] private sealed class Proof { public string editor; public int cases, patterns; public bool actual_tilemap, unlock_removes_last_tile, grid_filter, original_switcher; }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
            SessionState.SetBool(Key,true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;
            errors=0;Application.logMessageReceived-=Observe;Application.logMessageReceived+=Observe;
            EditorApplication.playModeStateChanged-=Mode;
            EditorApplication.playModeStateChanged+=Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if(mode!=PlayModeStateChange.EnteredPlayMode)return;
            try { Verify();Require(errors==0,"unexpected runtime error");SessionState.SetBool(Key,false);EditorApplication.Exit(0); }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        private static void Observe(string message,string trace,LogType kind)
        { if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors++; }
        private static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)??throw new MissingFieldException(type.FullName,name);
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Map13 minimap mask: "+message);}
        private static void Verify()
        {
            string repository=Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT")??throw new InvalidOperationException("Explicit repository required.");
            string output=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidOperationException("Isolated validation directory required.");
            var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(repository,"unity/Assets/Official/World/Map/13/Source/minimap-mask-patterns.json")));
            Require(fixture.manifest=="871_156598"&&fixture.mapClassId==13&&fixture.cases.Length==256,"original fixture binding");
            var tiles=fixture.patterns.mCornerPatterns.Concat(fixture.patterns.mEdgePatterns).ToDictionary(p=>p.tile,p=>ScriptableObject.CreateInstance<Tile>());
            var owner=new GameObject("Original minimap mask rule fixture");owner.SetActive(false);
            var grid=owner.AddComponent<Grid>();
            var tileObject=new GameObject("Native Tilemap",typeof(Tilemap));tileObject.transform.SetParent(owner.transform,false);
            var tilemap=tileObject.GetComponent<Tilemap>();
            var component=owner.AddComponent<MiniMapMaskHandler>();
            var type=typeof(MiniMapMaskHandler);
            Field(type,"mMaskGrid").SetValue(component,grid);Field(type,"mMaskTilemap").SetValue(component,tilemap);
            var switcher=new StateSwitcher<bool>();
            Field(typeof(StateSwitcher<bool>),"mStateConfigs").SetValue(switcher,new List<IStateConfig>());
            Field(type,"mMaskedGoSwitcher").SetValue(component,switcher);
            void Bind(string name,Pattern[] patterns)
            {
                var field=Field(type,name);var list=(IList)Activator.CreateInstance(field.FieldType);var element=field.FieldType.GetGenericArguments()[0];
                foreach(var pattern in patterns)
                {
                    var native=JsonUtility.FromJson(JsonUtility.ToJson(pattern),element);
                    Field(element,"MyTile").SetValue(native,tiles[pattern.tile]);list.Add(native);
                }
                field.SetValue(component,list);
            }
            Bind("mCornerPatterns",fixture.patterns.mCornerPatterns);Bind("mEdgePatterns",fixture.patterns.mEdgePatterns);
            type.GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(component,null);
            Require(!grid.enabled&&!switcher.State,"original Awake hides mask grid and switched content");
            var info=new MapRootEntityLiteSingleInfo{MapClassId=13,MapGroundSize=new Pos2d(5,5),MapGroundOriginPos=new Vector3(-0.5f,-0.5f,0),PosDataArray=Enumerable.Range(0,5).Select(_=>new MapRootEntityLiteGridInfo[5]).ToArray()};
            component.GridToWorld=(id,p)=>{Require(id==13,"source map id");return info.GridToPos(p);};
            var center=new Pos2d(2,2);var position=new Vector3Int(1,1,0);var locked=new HashSet<Pos2d>();
            bool Maskable(Pos2d p)=>p.X>=0&&p.Y>=0&&p.X<5&&p.Y<5;
            foreach(var test in fixture.cases)
            {
                locked.Clear();for(int bit=0;bit<8;bit++)if((test.mask&(1<<bit))!=0)locked.Add(center+Pos2d.EightDirs[bit]);
                tilemap.ClearAllTiles();component.Init(13,info,p=>locked.Contains(p),Maskable);
                Require(grid.enabled,"original Init enables mask grid");
                Require(tilemap.GetTile(position)==(string.IsNullOrEmpty(test.tile)?null:tiles[test.tile]),"selected original Tile for neighbor mask "+test.mask);
                Matrix4x4 matrix=tilemap.GetTransformMatrix(position);
                Require(Quaternion.Angle(matrix.rotation,Quaternion.Euler(0,0,test.rotation*90))<0.001f,"original tile rotation "+test.mask);
            }
            locked.Clear();locked.Add(center+Pos2d.Up);component.Init(13,info,p=>locked.Contains(p),Maskable);
            Require(tilemap.GetTile(position)!=null,"edge tile before unlock");locked.Clear();component.UnlockSomePos(new List<Pos2d>{center},center,5);
            Require(tilemap.GetTile(position)==null,"last edge removed after local visibility unlock");
            component.ShowMaskedGo();Require(switcher.State,"original masked-content switcher");
            tilemap.ClearAllTiles();component.Init(13,info,p=>false,p=>false);
            Require(tilemap.GetUsedTilesCount()==0,"nonmaskable cells retain no new tiles");
            Object.DestroyImmediate(owner);foreach(var tile in tiles.Values)Object.DestroyImmediate(tile);
            File.WriteAllText(Path.Combine(output,"minimap-mask-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,cases=256,patterns=10,actual_tilemap=true,unlock_removes_last_tile=true,grid_filter=true,original_switcher=true},true));
            Debug.Log("Map13 original minimap mask Play Mode passed: 256 neighbor configurations, 10 original ordered patterns, native Tilemap rotations, visibility unlock removal and original state switcher. Complete minimap/UI and Windows Player remain pending.");
        }
    }
}
