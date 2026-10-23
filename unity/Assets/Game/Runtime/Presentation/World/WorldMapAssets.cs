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

        public WorldMapAssets(OfficialMapDefinition definition, GameObject groundPrefab, GameObject miniMapPrefab)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            GroundPrefab = groundPrefab ?? throw new ArgumentNullException(nameof(groundPrefab));
            MiniMapPrefab = miniMapPrefab ?? throw new ArgumentNullException(nameof(miniMapPrefab));
        }
    }
}
