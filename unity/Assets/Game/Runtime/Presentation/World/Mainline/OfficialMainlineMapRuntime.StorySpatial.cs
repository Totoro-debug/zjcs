using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        [Serializable] private sealed class StorySpatialRange { public int X, Y; }
        [Serializable] private sealed class StorySpatialEntity
        {
            public int EntityClassId, TagType;
            public string[] RegisterKeys;
        }
        [Serializable] private sealed class StorySpatialNodeFields
        {
            public string RegisterKey, Tag;
            public int SelectGetType, LoEntityId;
            public Vector2 Offset;
        }
        [Serializable] private sealed class StorySpatialPayload
        {
            public string manifest;
            public int mapClassId;
            public StorySpatialRange PlayerArroundRangeSize;
            public float PlayerTalkDistException;
            public Vector2 GetEntityPosOffset;
            public StorySpatialEntity[] Entities;
        }

        private StorySpatialPayload storySpatial;
        private readonly Dictionary<int, StorySpatialEntity> storySpatialEntities =
            new Dictionary<int, StorySpatialEntity>();

        private void InitializeStorySpatial()
        {
            if (!MapProfile.HasNativeUi && mapClassId != 14) return;
            TextAsset source = Resources.Load<TextAsset>(MapResource("story-spatial"));
            storySpatial = source == null ? null : JsonUtility.FromJson<StorySpatialPayload>(source.text);
            int expectedCount = mapClassId == 12 ? 138 : mapClassId == 13 ? 150 : mapClassId == 14 ? 158 : 113;
            if (storySpatial == null || storySpatial.manifest != (mapClassId == 11 ? "854_156383" : "871_156598")
                || storySpatial.mapClassId != mapClassId
                || storySpatial.Entities == null || storySpatial.Entities.Length != expectedCount
                || storySpatial.PlayerArroundRangeSize == null || storySpatial.PlayerArroundRangeSize.X <= 0
                || storySpatial.PlayerArroundRangeSize.Y <= 0 || storySpatial.PlayerTalkDistException <= 0)
                throw new InvalidDataException("Original Map" + mapClassId + " story spatial bindings are malformed.");
            storySpatialEntities.Clear();
            foreach (StorySpatialEntity entity in storySpatial.Entities)
            {
                if (entity == null || entity.EntityClassId <= 0 || entity.RegisterKeys == null
                    || storySpatialEntities.ContainsKey(entity.EntityClassId))
                    throw new InvalidDataException("Original Map" + mapClassId + " story entity binding is malformed.");
                storySpatialEntities.Add(entity.EntityClassId, entity);
            }
            // This source closure covers the pinned map placements and story
            // additions. Exploration instances have their own prefab bindings.
            if (mapEntities.Values.Any(entity => entity.PlacementId < 1000000000
                && !storySpatialEntities.ContainsKey(entity.EntityClassId)))
                throw new InvalidDataException("Map" + mapClassId + " entity has no original story prefab binding.");
        }

        private Vector2 ResolveRegisteredStoryPoint(string key)
        {
            foreach (Map10RuntimeEntity entity in mapEntities.Values)
            {
                if (!storySpatialEntities.TryGetValue(entity.EntityClassId, out StorySpatialEntity binding)
                    || !binding.RegisterKeys.Contains(key)) continue;
                GameObject instance = entityView == null ? null : entityView.FindEntityObject(entity.PlacementId);
                if (instance != null)
                {
                    if (!instance.activeInHierarchy) continue;
                    // Original StoryNodeRegister entries point to the prefab root.
                    OfficialMapNavigationView.WorldToGrid(session.Definition.Navigation,
                        new OfficialMapVector3 { X = instance.transform.position.x, Y = instance.transform.position.y },
                        out int x, out int y);
                    return new Vector2(x, y);
                }
                if (mapClassId == 14) return new Vector2(entity.Grid.X, entity.Grid.Y);
            }
            throw new InvalidDataException("Original story object is not registered in the loaded map: " + key);
        }

        private bool HasStoryTalkTag(int classId) => storySpatialEntities.TryGetValue(classId,
            out StorySpatialEntity binding) && (binding.TagType & 1) != 0;

        private StorySpatialNodeFields StorySpatialFields(Map10RuntimeStoryNode node)
        {
            if (mapClassId == 13 || mapClassId == 14)
                return JsonUtility.FromJson<StorySpatialNodeFields>(node.sourceJson);
            return new StorySpatialNodeFields
            {
                RegisterKey = node.registerKey,
                Tag = node.tag,
                SelectGetType = node.selectGetType,
                LoEntityId = node.loEntityId,
                Offset = new Vector2(node.offsetX, node.offsetY)
            };
        }

        private Vector2 ResolveStoryTalkCenter()
        {
            Vector3 player = avatar.transform.position;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            if (battleController == null && storySpatial != null)
            {
                // Both original local-player prefabs (3001 and 3002) carry StoryTalk.
                if (HasStoryTalkTag(3001)) Append(player);
                foreach (Map10RuntimeEntity entity in mapEntities.Values)
                {
                    if (!HasStoryTalkTag(entity.EntityClassId) || !InRange(entity.Grid.X, entity.Grid.Y)) continue;
                    GameObject instance = entityView == null ? null : entityView.FindEntityObject(entity.PlacementId);
                    if (instance != null && instance.activeInHierarchy) Append(instance.transform.position);
                    else if (mapClassId == 14 && entity.Active)
                        Append(StoryWorldPoint(entity.Grid.X, entity.Grid.Y));
                }
                foreach (KeyValuePair<int, GameObject> entity in storyEntities)
                {
                    if (entity.Value == null || !storyEntityClasses.TryGetValue(entity.Key, out int classId)
                        || !HasStoryTalkTag(classId)) continue;
                    Vector2 grid = StoryGridPosition(entity.Value.transform.position);
                    // Original client-container filtering does not test NPC visibility.
                    if (InRange(grid.x, grid.y)) Append(entity.Value.transform.position);
                }
            }
            Vector3 center = hasBounds ? bounds.center : player;
            if (Vector3.Distance(player, center) > storySpatial.PlayerTalkDistException) center = player;
            OfficialMapVector3 origin = session.Definition.Navigation.Origin;
            return new Vector2(center.x - origin.X, center.y - origin.Y) + storySpatial.GetEntityPosOffset;

            bool InRange(float x, float y) => x >= navigation.currentGrid.X - storySpatial.PlayerArroundRangeSize.X
                && x <= navigation.currentGrid.X + storySpatial.PlayerArroundRangeSize.X
                && y >= navigation.currentGrid.Y - storySpatial.PlayerArroundRangeSize.Y
                && y <= navigation.currentGrid.Y + storySpatial.PlayerArroundRangeSize.Y;

            void Append(Vector3 position)
            {
                if (hasBounds) bounds.Encapsulate(position);
                else { bounds.center = position; hasBounds = true; }
            }
        }
    }
}
