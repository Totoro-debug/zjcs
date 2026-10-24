using System;
using UnityEngine;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    [CreateAssetMenu(menuName = "Zjcs/World/Official Map Asset Catalog")]
    public sealed class OfficialWorldMapAssetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public int ClassId;
            public GameObject GroundPrefab;
            public GameObject MiniMapPrefab;
            public GameObject EntityPrefab;
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
            return new WorldMapAssets(definition, entry.GroundPrefab, entry.MiniMapPrefab, entry.EntityPrefab);
        }
    }
}
