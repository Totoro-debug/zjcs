using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.Map13Entities;
using Zjcs.Offline.World.NativeMap13EntityAssets;
using Zjcs.Offline.World.NativeMap13EntityViews;
using Zjcs.Offline.World.NativeMap13Skeletons;
using Zjcs.Offline.World.NativeMap13StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13EntityRendering;
using Zjcs.Official.World.Map13TreePlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.NativeMap13TreeEntities
{
    public sealed class NativeMap13TreeWorld : MonoBehaviour, IDisposable
    {
        private NativeMap13TreeWorldAssets assets;
        private NativeMap13AnimatorLibrary animations;
        private NativeMap13EntitySpriteLibrary sprites;
        private NativeMap13TreeModelAnimatorLibrary dissolve;
        private NativeMap13StoryPlayableScope playables;
        private OfflineMap13EntityPresentation presentation;
        private OfficialMapDefinition map;
        private GameObject staging;
        private Vector2 origin;
        private Func<int, int> getState;
        private Func<int, bool> isVisible;
        private long playerId;
        private bool disposed;
        private readonly Dictionary<int, NativeMap13TreeWorldEntity> entities = new Dictionary<int, NativeMap13TreeWorldEntity>();
        public IReadOnlyDictionary<int, NativeMap13TreeWorldEntity> Entities => entities;

        public void Initialize(OfficialMapDefinition definition, Vector2 mapOrigin, Func<int, int> state,
            Func<int, bool> visibility, long localPlayerId)
        {
            if (assets || disposed) throw new InvalidOperationException("Map13 tree world is not new.");
            assets = Resources.Load<NativeMap13TreeWorldAssets>("Map13/Native/tree-world-assets");
            if (!assets || assets.manifest != "871_156598" || assets.placements?.Length != 3 || assets.displays?.Length != 2)
                throw new InvalidDataException("Original Map13 tree world references are incomplete.");
            map = definition; origin = mapOrigin;
            getState = state ?? throw new ArgumentNullException(nameof(state));
            isVisible = visibility ?? throw new ArgumentNullException(nameof(visibility)); playerId = localPlayerId;
            presentation = OfflineMap13EntityPresentation.LoadDefault();
            animations = new NativeMap13AnimatorLibrary(assets.animationBinary, assets.animationCatalog, assets.animationSha256);
            sprites = new NativeMap13EntitySpriteLibrary(Resources.Load<NativeMap13EntitySpriteCatalog>(assets.spriteCatalogResource));
            dissolve = new NativeMap13TreeModelAnimatorLibrary(assets.dissolveBinary, assets.dissolveCatalog);
            staging = new GameObject("Map13 inactive tree entity injection"); staging.SetActive(false); staging.transform.SetParent(transform, false);
            playables = gameObject.AddComponent<NativeMap13StoryPlayableScope>(); playables.Initialize();
        }
        public bool TryCreate(int placementId, int entityClassId, Vector2Int grid, int state, GameObject owner)
        {
            if (entityClassId != 8363) return false;
            var source = assets.placements.SingleOrDefault(p => p.placementId == placementId);
            if (source == null || source.entityClassId != entityClassId || source.position.X != grid.x || source.position.Y != grid.y || !owner)
                throw new InvalidDataException("Original Map13 tree placement does not match its native view.");
            if (entities.ContainsKey(placementId)) throw new InvalidOperationException("Original tree was created twice.");
            var selected = presentation.Resolve(map, entityClassId, grid.x, grid.y, state, source.tag);
            var display = assets.displays.SingleOrDefault(d => d.path == selected.Display?.DisplayPath);
            if (display == null || !display.prefab) throw new InvalidDataException("Original Map13 tree display is not bound: " + selected.Display?.DisplayPath);
            var instance = Instantiate(display.prefab, staging.transform, false);
            var binding = instance.GetComponent<NativeMap13TreeEntityBinding>();
            if (!binding) throw new InvalidDataException("Original tree entity binding is missing.");
            var entity = owner.AddComponent<NativeMap13TreeWorldEntity>();
            entity.Initialize(source, origin, playables.Manager, state, playerId, binding);
            var count = owner.AddComponent<Map13AwardCountBinding>();
            count.Stage = entity.Entity.GetComponent<Zjcs.Official.World.Map13EntityStateViews.Map13WorldStageQuery>();
            count.Awards = entity.Entity.GetComponent<Zjcs.Official.World.Map13EntityStateViews.Map13AwardItemQuery>();
            binding.Prepare(animations, sprites, dissolve.Controllers, entity,
                new Zjcs.Official.EntityViews.MapClientVisibilitySupporterFactory { MapRootManagerProxy = entity });
            instance.transform.SetParent(owner.transform, false);
            entities.Add(placementId, entity);
            return true;
        }
        public bool TrySetGrid(int placementId, Vector2Int grid)
        {
            if (!entities.TryGetValue(placementId, out var entity)) return false;
            entity.SetGrid(new Pos2d(grid.x, grid.y)); return true;
        }
        public void Refresh()
        {
            if (disposed || !assets) return;
            foreach (var pair in entities)
            {
                if (pair.Value.IsRemoving)
                {
                    if (!pair.Value.RequestRemoval()) pair.Value.gameObject.SetActive(false);
                }
                else pair.Value.Refresh(getState(pair.Key), isVisible(pair.Key));
            }
        }
        public bool KeepRemovalActive(int placementId) => entities.TryGetValue(placementId, out var entity) && entity.RequestRemoval();
        private void LateUpdate() { if (!disposed) SlicedFilledRenderService.Current.LateUpdate(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var entity in entities.Values) if (entity) entity.Remove();
            entities.Clear();
            if (playables) playables.Dispose();
            presentation?.Dispose(); presentation = null;
            sprites?.Dispose(); sprites = null;
            dissolve?.Dispose(); dissolve = null;
            animations?.Dispose(); animations = null;
            if (staging) Destroy(staging);
        }
        private void OnDestroy() => Dispose();
    }
}
