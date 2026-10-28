using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Official.SteleEntities;
using Zjcs.Official.World;
using Zjcs.Offline.World.NativeMap13EntityAssets;

namespace Zjcs.Offline.World.NativeMap12Steles
{
    public sealed class NativeMap12SteleWorld : MonoBehaviour, IDisposable
    {
        private NativeMap12SteleCatalog catalog;
        private NativeMap13EntitySpriteLibrary sprites;
        private NativeMap12StelePlayableScope playables;
        private readonly Dictionary<int, NativeMap12SteleEntity> entities = new Dictionary<int, NativeMap12SteleEntity>();
        private GameObject staging;
        private Func<int, int> getCount, getState;
        private Func<int, IWorldStageOperationContext> getWindowContext;
        private Vector2 origin;
        private bool initialized, disposed;
        public IReadOnlyDictionary<int, NativeMap12SteleEntity> Entities => entities;

        public void Initialize(Vector2 mapOrigin, Func<int, int> periodicityCount,
            Func<int, int> worldStageState, Func<int, IWorldStageOperationContext> windowContext)
        {
            if (initialized || disposed) throw new InvalidOperationException("Stele world scope is not new.");
            catalog = NativeMap12SteleCatalog.Load();
            var spriteCatalog = Resources.Load<NativeMap13EntitySpriteCatalog>("Map12/Native/stele-sprite-catalog");
            if (!spriteCatalog || spriteCatalog.sprites?.Length != 2 || spriteCatalog.renderers?.Length != 2)
                throw new InvalidDataException("Original Map12 stele Sprite bindings are incomplete.");
            sprites = new NativeMap13EntitySpriteLibrary(spriteCatalog);
            origin = mapOrigin;
            getCount = periodicityCount ?? throw new ArgumentNullException(nameof(periodicityCount));
            getState = worldStageState ?? throw new ArgumentNullException(nameof(worldStageState));
            getWindowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
            staging = new GameObject("Stele original entity injection");
            staging.SetActive(false); staging.transform.SetParent(transform, false);
            playables = gameObject.AddComponent<NativeMap12StelePlayableScope>();
            playables.Initialize();
            initialized = true;
        }

        public bool TryCreate(int placementId, int entityClassId, Vector2Int grid, int direction,
            int initialState, GameObject owner)
        {
            if (!initialized || disposed) throw new InvalidOperationException("Stele world scope is unavailable.");
            if (entityClassId != 6650 && entityClassId != 6655) return false;
            var placement = catalog.FindPlacement(placementId);
            if (placement == null || placement.entityClassId != entityClassId
                || placement.x != grid.x || placement.y != grid.y || placement.direction != direction || !owner)
                throw new InvalidDataException("Original Map12 stele placement does not match its native display.");
            if (entities.ContainsKey(placementId)) throw new InvalidOperationException("Original stele was created twice.");
            var display = catalog.FindDisplay(entityClassId);
            var instance = Instantiate(display.prefab, staging.transform, false);
            NativeMap12SteleEntity entity = null;
            try
            {
                var bindings = instance.GetComponentsInChildren<NativeMap13EntitySpriteBinding>(true);
                if (bindings.Length != 1) throw new InvalidDataException("Original stele SpriteRenderer binding is ambiguous.");
                bindings[0].Bind(sprites);
                instance.transform.SetParent(owner.transform, false);
                var driver = instance.GetComponent<ECViewDriver>();
                entity = owner.AddComponent<NativeMap12SteleEntity>();
                entity.Initialize(display, placement, driver, playables, origin, grid, direction,
                    initialState, getCount(placement.periodicityGroupId), getWindowContext(placementId));
                entities.Add(placementId, entity);
                return true;
            }
            catch
            {
                if (entity) { entity.Remove(); Destroy(entity); }
                Destroy(instance);
                throw;
            }
        }

        public void Refresh()
        {
            if (!initialized || disposed) return;
            foreach (var pair in entities)
            {
                var placement = catalog.FindPlacement(pair.Key);
                pair.Value.Refresh(getState(pair.Key), getCount(placement.periodicityGroupId));
            }
        }
        public bool TrySetGrid(int placementId, Vector2Int grid)
        {
            if (!entities.TryGetValue(placementId, out var entity)) return false;
            entity.SetGrid(grid); return true;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var entity in entities.Values)
            {
                if (!entity) continue;
                if (entity.Driver) entity.Driver.gameObject.SetActive(false);
                entity.Remove();
            }
            entities.Clear();
            if (playables) playables.Dispose();
            sprites?.Dispose(); sprites = null;
            if (staging) Destroy(staging);
        }
        private void OnDestroy() => Dispose();
    }
}
