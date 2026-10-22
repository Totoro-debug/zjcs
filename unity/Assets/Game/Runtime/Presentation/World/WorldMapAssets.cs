using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    public interface IWorldMapAssets
    {
        WorldMapAssets Load(int classId);
    }

    public sealed class WorldMapAssets
    {
        public OfficialMapDefinition Definition { get; }
        public GameObject GroundPrefab { get; }
        public GameObject MiniMapPrefab { get; }

        public WorldMapAssets(OfficialMapDefinition definition, GameObject groundPrefab, GameObject miniMapPrefab)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            GroundPrefab = groundPrefab ?? throw new ArgumentNullException(nameof(groundPrefab));
            MiniMapPrefab = miniMapPrefab ?? throw new ArgumentNullException(nameof(miniMapPrefab));
        }
    }

    [CreateAssetMenu(menuName = "Zjcs/World/Official Map Asset Catalog")]
    public sealed class OfficialWorldMapAssetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public int ClassId;
            public GameObject GroundPrefab;
            public GameObject MiniMapPrefab;
            public TextAsset DefinitionJson;
            public TextAsset SourceManifestJson;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public static OfficialWorldMapAssetCatalog LoadDefault()
        {
            OfficialWorldMapAssetCatalog catalog = Resources.Load<OfficialWorldMapAssetCatalog>("World/OfficialWorldMapAssetCatalog");
            if (catalog == null) throw new InvalidOperationException("Official world map catalog is not installed.");
            return catalog;
        }

        public bool TryGet(int classId, out Entry entry)
        {
            foreach (Entry candidate in entries)
            {
                if (candidate != null && candidate.ClassId == classId)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public WorldMapAssets LoadMap(int classId)
        {
            if (!TryGet(classId, out Entry entry))
                throw new InvalidOperationException("No local official map resource binding for ClassId " + classId);
            if (entry.DefinitionJson == null || entry.SourceManifestJson == null)
                throw new InvalidOperationException("Map definition or source manifest is not bound for ClassId " + classId);
            OfficialMapDefinition definition = JsonUtility.FromJson<OfficialMapDefinition>(entry.DefinitionJson.text);
            if (definition == null || definition.ClassId != classId)
                throw new InvalidOperationException("Map definition identity differs from requested ClassId " + classId);
            OfficialWorldMapAssetProvider.VerifySource(definition, entry);
            return new WorldMapAssets(definition, entry.GroundPrefab, entry.MiniMapPrefab);
        }
    }

    public sealed class OfficialWorldMapAssetProvider : MonoBehaviour, IWorldMapAssets
    {
        [SerializeField] private OfficialWorldMapAssetCatalog catalog;

        public WorldMapAssets Load(int classId)
        {
            if (catalog == null || !catalog.TryGet(classId, out OfficialWorldMapAssetCatalog.Entry entry))
                throw new InvalidOperationException("No local official map resource binding for ClassId " + classId);
            if (entry.DefinitionJson == null)
                throw new InvalidOperationException("Map definition JSON is not bound for ClassId " + classId);
            if (entry.SourceManifestJson == null)
                throw new InvalidOperationException("Map source manifest is not bound for ClassId " + classId);

            OfficialMapDefinition definition = JsonUtility.FromJson<OfficialMapDefinition>(entry.DefinitionJson.text);
            if (definition == null || definition.ClassId != classId)
                throw new InvalidOperationException("Map definition identity differs from requested ClassId " + classId);
            VerifySource(definition, entry);
            return new WorldMapAssets(definition, entry.GroundPrefab, entry.MiniMapPrefab);
        }

        internal static void VerifySource(OfficialMapDefinition definition, OfficialWorldMapAssetCatalog.Entry entry)
        {
            string expectedManifestPath = "Assets/Official/World/Map/" + definition.ClassId + "/source-manifest.json";
            if (!string.Equals(definition.SourceManifest, expectedManifestPath, StringComparison.Ordinal))
                throw new InvalidOperationException("Map source manifest path differs for ClassId " + definition.ClassId);

            OfficialMapSourceManifest manifest = JsonUtility.FromJson<OfficialMapSourceManifest>(entry.SourceManifestJson.text);
            if (manifest == null || manifest.mapClassId != definition.ClassId || manifest.output == null)
                throw new InvalidOperationException("Map source manifest identity differs for ClassId " + definition.ClassId);
            if (!string.Equals(manifest.sourceInputsSha256, definition.SourceManifestSha256, StringComparison.Ordinal)
                || !string.Equals(HashUtf8(manifest.sourceInputsJson), manifest.sourceInputsSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Map source input digest differs for ClassId " + definition.ClassId);
            if (!string.Equals(manifest.output.path, "map" + definition.ClassId + ".json", StringComparison.Ordinal)
                || !string.Equals(HashUtf8(entry.DefinitionJson.text), manifest.output.sha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Map definition digest differs from its source manifest for ClassId " + definition.ClassId);
        }

        private static string HashUtf8(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
