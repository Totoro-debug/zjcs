using System;
using System.Collections.Generic;
using System.Reflection;
using GConfig;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.World.Map14DisplayTags;

namespace Zjcs.Offline.World.NativeMap14DisplayData
{
    // Own parser scope over exact original bytes. No global Parsers or player
    // singleton is replaced; the map host supplies original logical identities.
    public sealed class NativeMap14DisplayData : IDisposable,IMapEntityTagInputs
    {
        [Serializable] private sealed class Catalog { public string Manifest; public Entry[] Entries; }
        [Serializable] private sealed class Entry
        { public string OriginalPath; public string Resource; public string Sha256; public string Parser; public string Config; }
        private readonly NativeMap14SharedAssemblies shared;
        private GameObject owner;
        private ConfigManager manager;
        private object mapParser, areaParser, tagParser, identities;
        private bool disposed;

        public NativeMap14DisplayData(NativeMap14SharedAssemblies shared)
        {
            this.shared=shared??throw new ArgumentNullException(nameof(shared));
            try
            {
                var asset=Resources.Load<TextAsset>("Map14/DisplayData/catalog");
                if (!asset) throw new InvalidOperationException("Original display table catalog is absent.");
                var catalog=JsonUtility.FromJson<Catalog>(asset.text);
                if (catalog.Manifest!="871_156598" || catalog.Entries.Length!=3) throw new InvalidOperationException("Original display table version/closure differs.");
                var paths=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var entry in catalog.Entries)
                {
                    var bytes=Resources.Load<TextAsset>(entry.Resource);
                    if (!bytes) throw new InvalidOperationException("Original display table is absent: "+entry.OriginalPath);
                    NativeMap14SharedAssemblies.Verify(bytes.bytes,entry.Sha256);
                    paths.Add(entry.OriginalPath,entry.Resource+".bytes");
                }
                var settings=Resources.Load<TextAsset>("Map14/SharedConfig/game_settings.g");
                if (!settings) throw new InvalidOperationException("Original display settings dependency is absent.");
                NativeMap14SharedAssemblies.Verify(settings.bytes,NativeMap14SharedConfiguration.SettingsSha256);
                paths.Add(NativeMap14SharedConfiguration.SourcePath,"Map14/SharedConfig/game_settings.g.bytes");
                owner=new GameObject("Map14 original display table scope");manager=owner.AddComponent<ConfigManager>();
                var previous=GConfig.FileLoader.sFileLoader;
                try { GConfig.FileLoader.SetupFileLoader(new DefaultFileLoader());manager.Initialize("",false,0); }
                finally { GConfig.FileLoader.SetupFileLoader(previous); }
                manager.PathTransformFunc=path=>paths.TryGetValue(path,out var local)?local:throw new InvalidOperationException("Original display path is unbound: "+path);
                Load("GameSettingsParser","GameSettings");
                foreach(var entry in catalog.Entries) Load(entry.Parser,entry.Config);
                mapParser=manager.GetParser(shared.CommonType("MapInfoParser"));
                areaParser=manager.GetParser(shared.CommonType("MapAreaInfoParser"));
                tagParser=manager.GetParser(shared.CommonType("MapLogicEntityTagInfoParser"));
                identities=Activator.CreateInstance(shared.CommonType("MapLoEntityIdManager"));
            }
            catch { Dispose();throw; }
        }
        private void Load(string parserName,string configName)
        {
            var type=shared.CommonType(parserName);manager.RegisterTypes(new[]{type});
            var parser=manager.GetParser(type)??throw new InvalidOperationException("Original display parser registration failed: "+parserName);
            var attr=shared.CommonType(configName).GetCustomAttribute<ConfigAttribute>()??throw new InvalidOperationException("Original display config attribute is absent.");
            var request=parser.Parse(attr.LoadPhases,false)??throw new InvalidOperationException("Original display parse phase was rejected.");
            manager.Wait(new[]{request});
            if(!request.Success)throw new InvalidOperationException("Original display table parse failed: "+configName);
            parser.PostParse();
        }
        public object GetMapInfo(int mapClassId)
        { RequireActive();return mapParser.GetType().GetMethod("Get",new[]{typeof(int)}).Invoke(mapParser,new object[]{mapClassId}); }
        public object GetAreaInfo(int mapClassId,int areaId)
        { RequireActive();return areaParser.GetType().GetMethod("Get",new[]{typeof(int),typeof(int)}).Invoke(areaParser,new object[]{mapClassId,areaId}); }
        public void SetLogicalEntity(int logicalId,object originalIdentifier)
        {
            RequireActive();
            if(originalIdentifier==null || originalIdentifier.GetType()!=shared.CommonType("EcEntityIdentifier"))throw new ArgumentException("Original entity identity is required.",nameof(originalIdentifier));
            identities.GetType().GetMethod("SetLoEntId2EntId").Invoke(identities,new[]{(object)logicalId,originalIdentifier});
        }
        public string GetEntityTag(object originalEntity)
        {
            RequireActive();
            if(originalEntity==null || originalEntity.GetType()!=shared.CommonType("EcEntity"))throw new ArgumentException("Original shared entity is required.",nameof(originalEntity));
            return OfficialMapEntityTagQuery.GetEntityTag(originalEntity,this);
        }
        int IMapEntityTagInputs.GetLogicalEntityId(object originalEntity)
        {
            var id=NativeMap14SharedPrefabs.Get(originalEntity,"Id");
            return (int)identities.GetType().GetMethod("GetLoEntIdByEntId").Invoke(identities,new[]{id});
        }
        string IMapEntityTagInputs.GetEntityTag(object originalEntity,int logicalId)
        {
            int mapClassId=(int)NativeMap14SharedPrefabs.Get(NativeMap14SharedPrefabs.Get(originalEntity,"Container"),"ClassId");
            return (string)tagParser.GetType().GetMethod("GetEntityTag").Invoke(tagParser,new object[]{mapClassId,logicalId});
        }
        private void RequireActive(){if(disposed)throw new ObjectDisposedException(nameof(NativeMap14DisplayData));}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(identities!=null)identities.GetType().GetMethod("Clear").Invoke(identities,null);
            identities=null;mapParser=null;areaParser=null;tagParser=null;
            if(owner){if(Application.isPlaying)UnityEngine.Object.Destroy(owner);else UnityEngine.Object.DestroyImmediate(owner);}
            manager=null;owner=null;
        }
    }
}
