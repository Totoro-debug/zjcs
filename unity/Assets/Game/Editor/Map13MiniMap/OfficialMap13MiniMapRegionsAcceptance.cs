using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapRegionsAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapRegions.Running";
        private const string ShaderPath="Assets/Game/Runtime/Official/World/Map13MiniMap/MiniMapMaskableTilemap.shader";
        private static int errors;
        [Serializable] private sealed class Output{public string path,sha256;}
        [Serializable] private sealed class Region{public int region,tiles;public string path,guid;}
        [Serializable] private sealed class Program{public string[] keywords;}
        [Serializable] private sealed class ShaderRecord{public Program[] programs;}
        [Serializable] private sealed class TextureRecord{public string path,rgba_sha256;public int width,height,mip_count,filter,wrapU,wrapV,wrapW,aniso;public bool sRGB,readable;public float mipBias;}
        [Serializable] private sealed class Receipt{public Output[] outputs;public Region[] regions;public TextureRecord[] textures;public ShaderRecord shader;}
        [Serializable] private sealed class Proof{public string editor;public int regions,tile_entries,tiles,sprite_vertices,shader_stages,public_pixels,private_pixels,changed_pixels;public bool original_textures,all_references;}
        private static string Repository=>Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT")??throw new InvalidOperationException("Repository required");
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidOperationException("Owned workspace required");
        private static void Require(bool ok,string detail){if(!ok)throw new InvalidDataException(detail);}
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private static string AssetPath(string path)=>path.Substring("unity/".Length);
        private static Receipt Read()=>JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(Repository,"assets/reference/official-map/map13-native-minimap-regions-source-manifest.json")));
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
            Directory.CreateDirectory(Workspace);errors=0;Application.logMessageReceived+=Observe;
            SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            EditorApplication.delayCall+=()=>{try{Verify();Require(errors==0,"Runtime errors: "+errors);SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);SessionState.EraseBool(Key);EditorApplication.Exit(1);}};
        }
        private static void Verify()
        {
            var receipt=Read();Require(receipt.outputs.Length==127&&receipt.regions.Length==106&&receipt.textures.Length==2,"Complete original resource closure");
            foreach(var output in receipt.outputs)
            {
                string path=AssetPath(output.path);Require(Hash(File.ReadAllBytes(path))==output.sha256,"Resource bytes changed: "+path);
                var asset=AssetDatabase.LoadMainAssetAtPath(path);Require(asset,"Failed native import: "+path);
                if(asset is TextAsset)continue;
                var property=new SerializedObject(asset).GetIterator();
                while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue==null&&property.objectReferenceInstanceIDValue!=0)throw new InvalidDataException("Missing reference "+path+":"+property.propertyPath);
            }
            foreach(var item in receipt.textures)
            {
                var t=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath(item.path));
                Require(t&&t.width==item.width&&t.height==item.height&&t.mipmapCount==item.mip_count&&t.format==TextureFormat.RGBA32&&t.isReadable==item.readable&&t.isDataSRGB==item.sRGB&&(int)t.filterMode==item.filter&&(int)t.wrapModeU==item.wrapU&&(int)t.wrapModeV==item.wrapV&&(int)t.wrapModeW==item.wrapW&&t.anisoLevel==item.aniso&&Math.Abs(t.mipMapBias-item.mipBias)<0.00001f,"Original texture sampling/color/mips: "+item.path);
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);Require(shader&&shader.isSupported,"Original mask shader unavailable");
            bool previous=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;int stages=0;
            try{foreach(var program in receipt.shader.programs)foreach(ShaderType stage in new[]{ShaderType.Vertex,ShaderType.Fragment}){var result=ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0).CompileVariant(stage,program.keywords,ShaderCompilerPlatform.D3D,BuildTarget.StandaloneWindows64);Require(result.Success,string.Join("\n",result.Messages.Select(m=>m.message)));stages++;}}
            finally{ShaderUtil.allowAsyncCompilation=previous;}
            Require(stages==8&&!ShaderUtil.ShaderHasError(shader),"All original shader stages must compile");
            var grid=new GameObject("OriginalMap13RegionalTilemaps",typeof(Grid));var maps=new List<Tilemap>();var names=new HashSet<string>();int entries=0,vertices=0;
            foreach(var region in receipt.regions)
            {
                string path=AssetPath(region.path);Require(AssetDatabase.AssetPathToGUID(path)==region.guid,"Explicit original region binding");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab&&prefab.name=="RegionGround_"+region.region,"Original region identity");
                var go=Object.Instantiate(prefab,grid.transform,false);var map=go.GetComponent<Tilemap>();var renderer=go.GetComponent<TilemapRenderer>();
                Require(map&&renderer&&go.layer==16&&renderer.sharedMaterial.shader==shader,"Original Tilemap renderer/mask binding");
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)==0,"Unbound Tilemap script");
                int count=0;foreach(var pos in map.cellBounds.allPositionsWithin)
                {
                    var tile=map.GetTile<Tile>(pos);if(!tile)continue;count++;names.Add(tile.name);Require(tile.sprite&&tile.sprite.texture,"Original tile sprite/texture");
                    Require(tile.sprite.name=="ground_type_none1","Original source sprite identity");vertices=tile.sprite.vertices.Length;
                    Require(map.GetSprite(pos)==tile.sprite,"Native Tilemap sprite evaluation");
                    var color=map.GetColor(pos);Require(color.a>0,"Original nonempty tile color");
                }
                Require(count==region.tiles,"Native Tilemap tile-entry count: "+region.region+" "+count+"/"+region.tiles);entries+=count;maps.Add(map);
            }
            Require(entries==7577&&names.Count==12,"All original regional tile entries and Tile identities");
            // Diagnostic framing only; include every original tile, including
            // the southern regions. The formal minimap uses its own camera.
            var bounds=maps[0].localBounds;foreach(var map in maps.Skip(1))bounds.Encapsulate(map.localBounds);
            var cam=new GameObject("RegionalTilemapValidationCamera",typeof(Camera)).GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=Math.Max(bounds.extents.x,bounds.extents.y)*1.05f;cam.nearClipPlane=.3f;cam.farClipPlane=500;cam.transform.position=new Vector3(bounds.center.x,bounds.center.y,-250);cam.cullingMask=1<<16;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(0,0,0,0);cam.allowHDR=false;
            var target=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32);target.Create();cam.targetTexture=target;
            Color32[] Capture(string name){cam.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1024,1024,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply(false,false);RenderTexture.active=old;File.WriteAllBytes(Path.Combine(Workspace,name+".png"),image.EncodeToPNG());var pixels=image.GetPixels32();Object.DestroyImmediate(image);return pixels;}
            var publicImage=Capture("minimap-regions-public");
            foreach(var map in maps)map.transform.localPosition=new Vector3(0,0,-1);
            var privateImage=Capture("minimap-regions-private");
            int publicPixels=publicImage.Count(p=>p.a>0),privatePixels=privateImage.Count(p=>p.a>0),changed=Enumerable.Range(0,publicImage.Length).Count(i=>!publicImage[i].Equals(privateImage[i]));
            Require(publicPixels>10000&&privatePixels>10000&&changed>1000,"Original region public/private mask must render distinct images: "+publicPixels+"/"+privatePixels+"/"+changed);
            File.WriteAllText(Path.Combine(Workspace,"minimap-regions-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,regions=maps.Count,tile_entries=entries,tiles=names.Count,sprite_vertices=vertices,shader_stages=stages,public_pixels=publicPixels,private_pixels=privatePixels,changed_pixels=changed,original_textures=true,all_references=true},true)+"\n");
            cam.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(grid);Object.DestroyImmediate(cam.gameObject);
            Debug.Log("Map13 original minimap regions Play Mode passed: 106 native Tilemaps, 7577 original tile entries, 12 original Tiles, two exact textures, all eight Windows GPU stages and distinct public/private mask renders. Formal minimap input/save and Windows Player remain pending.");
        }
    }
}
