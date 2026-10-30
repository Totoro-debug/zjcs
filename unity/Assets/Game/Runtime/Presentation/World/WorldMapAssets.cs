using System;
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
        public GameObject EntityPrefab { get; }

        public WorldMapAssets(OfficialMapDefinition definition, GameObject groundPrefab, GameObject miniMapPrefab,
            GameObject entityPrefab = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            GroundPrefab = groundPrefab;
            MiniMapPrefab = miniMapPrefab;
            EntityPrefab = entityPrefab;
        }
    }
}
