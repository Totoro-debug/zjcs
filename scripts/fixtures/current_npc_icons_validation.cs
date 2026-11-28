using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleRoundUI;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

public static class CurrentNpcIconsValidation
{
    [Serializable] sealed class Fixture { public Expected[] sprites; public TextureExpected[] textures; }
    [Serializable] sealed class RectExpected { public float x,y,width,height; }
    [Serializable] sealed class Shape { public Vector2[] points; }
    [Serializable] sealed class Expected
    {
        public string path,name,guid; public RectExpected rect; public Vector2 pivot; public Vector4 border;
        public float ppu; public Vector2[] vertices,uv; public int[] triangles; public Shape[] physics_shapes;
    }
    [Serializable] sealed class Settings { public int m_FilterMode,m_Aniso,m_WrapU,m_WrapV,m_WrapW; public float m_MipBias; }
    [Serializable] sealed class Mip { public int level,width,height,rgba_size; public string rgba_sha256; }
    [Serializable] sealed class TextureExpected
    { public string path,pixels_sha256; public int format,width,height; public bool readable; public Mip[] mips; public Settings settings; }
    static void Require(bool value,string reason) { if (!value) throw new InvalidOperationException("Current NPC images: "+reason); }
    static void Near(float value,float expected,string reason) => Require(Math.Abs(value-expected)<=.000001f,reason);
    static void Point(Vector2 value,Vector2 expected,string reason) { Near(value.x,expected.x,reason+" x"); Near(value.y,expected.y,reason+" y"); }
    static string Hash(byte[] bytes)
    { using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
    public static void Check()
    {
        var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"npc-icons-fixture.json")));
        Require(SystemInfo.supportsAsyncGPUReadback,"GPU readback support");
        var sprites=new List<Sprite>();
        foreach(var e in fixture.sprites)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(e.path); Require(sprite!=null,e.path+" native Sprite import");
            Require(sprite.name==e.name && AssetDatabase.AssetPathToGUID(e.path)==e.guid,e.name+" original name and binding");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string _,out long id)&&id==21300000,e.name+" native object ID");
            Near(sprite.rect.x,e.rect.x,e.name+" rect x"); Near(sprite.rect.y,e.rect.y,e.name+" rect y");
            Near(sprite.rect.width,e.rect.width,e.name+" rect width"); Near(sprite.rect.height,e.rect.height,e.name+" rect height");
            Point(sprite.pivot,e.pivot,e.name+" pivot"); Near(sprite.pixelsPerUnit,e.ppu,e.name+" PPU");
            for(int axis=0;axis<4;axis++) Near(sprite.border[axis],e.border[axis],e.name+" border");
            Require(sprite.vertices.Length==e.vertices.Length && sprite.uv.Length==e.uv.Length,e.name+" vertex/UV length");
            for(int i=0;i<e.vertices.Length;i++) { Point(sprite.vertices[i],e.vertices[i],e.name+" vertex"); Point(sprite.uv[i],e.uv[i],e.name+" UV"); }
            Require(sprite.triangles.Select(value=>(int)value).SequenceEqual(e.triangles),e.name+" original triangle order");
            Require(sprite.GetPhysicsShapeCount()==e.physics_shapes.Length,e.name+" physics-shape count");
            for(int i=0;i<e.physics_shapes.Length;i++)
            {
                var points=new List<Vector2>(); sprite.GetPhysicsShape(i,points);
                Require(points.Count==e.physics_shapes[i].points.Length,e.name+" physics points");
                for(int p=0;p<points.Count;p++) Point(points[p],e.physics_shapes[i].points[p],e.name+" physics point");
            }
            sprites.Add(sprite);
        }
        foreach(var e in fixture.textures)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(e.path); Require(texture!=null,e.path+" native texture import");
            Require(texture.width==e.width && texture.height==e.height && texture.mipmapCount==e.mips.Length && (int)texture.format==e.format
                && texture.isReadable==e.readable,e.path+" dimensions, mip chain, Windows format and readability");
            Require((int)texture.filterMode==e.settings.m_FilterMode && texture.anisoLevel==e.settings.m_Aniso
                && (int)texture.wrapModeU==e.settings.m_WrapU && (int)texture.wrapModeV==e.settings.m_WrapV
                && (int)texture.wrapModeW==e.settings.m_WrapW,e.path+" original sampler");
            Near(texture.mipMapBias,e.settings.m_MipBias,e.path+" mip bias");
            using(var all=new MemoryStream())
            {
                foreach(var mip in e.mips)
                {
                    var request=AsyncGPUReadback.Request(texture,mip.level,(TextureFormat)e.format); request.WaitForCompletion();
                    Require(!request.hasError,e.path+" GPU upload/readback"); var bytes=request.GetData<byte>().ToArray();
                    Require(bytes.Length==mip.rgba_size && Hash(bytes)==mip.rgba_sha256,e.path+" exact mip "+mip.level);
                    all.Write(bytes,0,bytes.Length);
                }
                Require(Hash(all.ToArray())==e.pixels_sha256,e.path+" complete original decoded pixels");
            }
        }
        var partner=Resources.Load<OfflineProfessionImageCatalog>("Partners/OfficialPartnerNpcIconImages");
        Require(partner!=null && partner.Entries.Length==fixture.sprites.Length,"complete partner/profession address catalog");
        var loader=new OfflineProfessionImageResourceLoader(partner);
        foreach(var row in partner.Entries)
        {
            using(var path=loader.LoadAsset<Sprite>(row.Path))
            using(var guid=loader.LoadAssetByGuid<Sprite>(row.AssetGuid))
                Require(path.Result==row.Asset && guid.Result==row.Asset,"local path/GUID image binding "+row.Path);
        }
        var battle=OfficialBattleNpcPortraitCatalog.Load();
        foreach(var row in battle.bindings)
        {
            Require(battle.Find(row.npcClassId)==row.sprite,"battle NPC identity "+row.npcClassId);
            if(row.sprite!=null) Require(sprites.Contains(row.sprite),"battle NPC uses current shared native data");
        }
        ContactSheet(sprites);
        Debug.Log("Current NPC image GPU validation passed: "+sprites.Count+" native Sprites, "+fixture.textures.Length+" complete textures, original geometry/UV/physics and exact GPU mip bytes; real battle and partner catalogs.");
    }
    static void ContactSheet(List<Sprite> sprites)
    {
        var root=new GameObject("Current original NPC image contact sheet");
        var cameraObject=new GameObject("NPC diagnostic camera"); var camera=cameraObject.AddComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=14; camera.transform.position=new Vector3(0,0,-30);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.06f,.09f,.12f); camera.cullingMask=1<<31;
        var material=Resources.Load<GameObject>("Combat/RoundUI/GameRoundCharacterItem_Scene").GetComponent<GameRoundCharacterItemScene>().FactionBgSr.sharedMaterial;
        for(int i=0;i<sprites.Count;i++)
        {
            var item=new GameObject(sprites[i].name); item.layer=31; item.transform.SetParent(root.transform,false);
            item.transform.localPosition=new Vector3((i%16-7.5f)*2.5f,(5-i/16)*2.5f,0);
            var renderer=item.AddComponent<SpriteRenderer>(); renderer.sprite=sprites[i]; renderer.sharedMaterial=material;
        }
        var target=new RenderTexture(2048,1408,24); var pixels=new Texture2D(2048,1408,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active; camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0,0,2048,1408),0,0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"current-npc-images.png"),pixels.EncodeToPNG());
        RenderTexture.active=previous; camera.targetTexture=null;
        Object.Destroy(root); Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(pixels);
    }
}
