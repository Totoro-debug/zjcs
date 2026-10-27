using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Official.World;

namespace Zjcs.Offline.World.Map13Navigation
{
    // A data binding, deliberately separate from the unfinished mainline entry.
    public sealed class Map13NavigationAssets : ScriptableObject
    {
        public TextAsset DefinitionJson;
        public TextAsset SourceManifestJson;
        public TextAsset WaterJson;
        public TextAsset MoversJson;
        public string WaterSha256;
        public string MoversSha256;

        [Serializable] public sealed class MoverData
        {
            public string Manifest;
            public int MapClassId;
            public OfficialMapCharacterMover[] Movers;
        }

        public static Map13NavigationAssets LoadDefault()
        {
            var assets = Resources.Load<Map13NavigationAssets>("Map13/Native/navigation-catalog");
            if (assets == null) throw new FileNotFoundException("Map13 navigation data binding is not installed.");
            return assets;
        }

        public OfflineMap13NavigationSession Open(Map13WaterState[] saved = null)
        {
            if (DefinitionJson == null || SourceManifestJson == null || WaterJson == null || MoversJson == null)
                throw new InvalidDataException("Incomplete Map13 navigation bindings.");
            var definition = JsonUtility.FromJson<OfficialMapDefinition>(DefinitionJson.text);
            var manifest = JsonUtility.FromJson<OfficialMapSourceManifest>(SourceManifestJson.text);
            if (definition == null || definition.ClassId != 13 || manifest == null || manifest.format != 1
                || manifest.mapClassId != 13 || manifest.output == null
                || definition.SourceManifest != "Assets/Official/World/Map/13/source-manifest.json"
                || manifest.output.path != "map13.json" || manifest.output.size != DefinitionJson.bytes.Length
                || Hash(DefinitionJson.text) != manifest.output.sha256
                || Hash(manifest.sourceInputsJson) != manifest.sourceInputsSha256
                || manifest.sourceInputsSha256 != definition.SourceManifestSha256
                || Hash(WaterJson.text) != WaterSha256 || Hash(MoversJson.text) != MoversSha256)
                throw new InvalidDataException("Map13 navigation source/definition digest differs.");
            return new OfflineMap13NavigationSession(definition,
                JsonUtility.FromJson<Map13WaterDefinition>(WaterJson.text),
                JsonUtility.FromJson<MoverData>(MoversJson.text), saved);
        }

        private static string Hash(string value)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
    }
}
