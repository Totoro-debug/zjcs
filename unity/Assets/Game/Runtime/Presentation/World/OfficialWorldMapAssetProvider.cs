using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
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
