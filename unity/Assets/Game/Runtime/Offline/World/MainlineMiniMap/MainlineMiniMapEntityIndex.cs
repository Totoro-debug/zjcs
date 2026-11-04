using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // One live map, including its private/public containers, supplies the same
    // entity objects to the original area, ground and marker-manager consumers.
    public sealed class MainlineMiniMapEntityIndex : IMiniMapManagedAreaAgent, IMiniMapMarkGridGround
    {
        private readonly MainlineMiniMapWorldData world;
        private readonly int map, width, height;
        private readonly Dictionary<EcEntityIdentifier, Entry> entries = new Dictionary<EcEntityIdentifier, Entry>();
        private readonly Dictionary<int, Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity>> areas =
            new Dictionary<int, Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity>>();
        private readonly Dictionary<Pos2d, Cell> cells = new Dictionary<Pos2d, Cell>();

        public MiniMapManagedEvents Events { get; } = new MiniMapManagedEvents();

        public MainlineMiniMapEntityIndex(MainlineMiniMapWorldData world, int map)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (!world.TryGetMapLiteData(map, out var lite))
                throw new InvalidDataException("Original live minimap world is missing.");
            this.map = map;
            width = lite.MapGroundSize.X;
            height = lite.MapGroundSize.Y;
        }

        public IMiniMapSpawnEntity GetEntity(EcEntityIdentifier id) =>
            entries.TryGetValue(id, out var entry) ? entry.Entity : null;

        public Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity> GetAreaEntity(int area) =>
            areas.TryGetValue(area, out var values)
                ? new Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity>(values) : null;

        public int GridToRegion(int mapClassId, Pos2d position) => world.GridToRegion(mapClassId, position);

        public IMiniMapMarkCell GetPosData(Pos2d position) =>
            InGround(position) && cells.TryGetValue(position, out var cell) ? cell : null;

        public void Add(IMiniMapSpawnEntity entity)
        {
            var entry = Read(entity);
            if (entries.ContainsKey(entity.Id)) throw new InvalidOperationException("Live minimap entity is already indexed.");
            Index(entry);
            // EcContainer.AddEntity publishes after inserting the entity.
            Events.AddEntityEvent.FireEvent(entity);
        }

        public bool Remove(EcEntityIdentifier id, object data = null)
        {
            if (!entries.TryGetValue(id, out var entry)) return false;
            Unindex(entry);
            Events.RemoveEntityEvent.FireEvent(entry.Entity, data);
            return true;
        }

        public void Replace(EcEntityIdentifier previousId, IMiniMapSpawnEntity entity)
        {
            if (!entries.TryGetValue(previousId, out var previous))
                throw new InvalidOperationException("Previous live minimap entity is missing.");
            var entry = Read(entity);
            if (!entity.Id.Equals(previousId) && entries.ContainsKey(entity.Id))
                throw new InvalidOperationException("Replacement live minimap entity is already indexed.");
            Unindex(previous);
            Index(entry);
            Events.ReplaceEntityEvent.FireEvent(entity, previousId);
        }

        // Position changes do not pretend that an entity was removed/replaced.
        // Its original identity, stage object and listeners remain intact.
        public void RefreshPosition(EcEntityIdentifier id)
        {
            if (!entries.TryGetValue(id, out var previous))
                throw new InvalidOperationException("Moving live minimap entity is missing.");
            var entry = Read(previous.Entity);
            if (!entry.Id.Equals(id)) throw new InvalidOperationException("Use Replace when the live entity identity changes.");
            Unindex(previous);
            Index(entry);
        }

        private Entry Read(IMiniMapSpawnEntity entity)
        {
            if (entity == null || entity.ClassId <= 0 || entity.ContainerClassId != map || !entity.Id.IsValid()
                || entity.GridTransform == null || !ReferenceEquals(entity.GridTransform.Entity, entity))
                throw new InvalidDataException("Live minimap entity identity or original grid is missing.");
            var position = entity.GridTransform.Pos;
            if (!InGround(position)) throw new InvalidDataException("Live minimap entity lies outside its map.");
            int area = world.GridToArea(map, position);
            if (world.AreaToRegion(map, area) < 0) throw new InvalidDataException("Live minimap entity region is missing.");
            var body = entity.GridTransform.GetBodyRange()
                ?? throw new InvalidDataException("Original live minimap entity body is missing.");
            return new Entry(entity, area, body.Select(offset => position + offset).Where(InGround).Distinct().ToArray());
        }

        private bool InGround(Pos2d position) => position.X >= 0 && position.Y >= 0 && position.X < width && position.Y < height;

        private void Index(Entry entry)
        {
            entries.Add(entry.Id, entry);
            if (!areas.TryGetValue(entry.Area, out var area))
                areas.Add(entry.Area, area = new Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity>());
            area.Add(entry.Id, entry.Entity);
            foreach (var position in entry.Body)
            {
                if (!cells.TryGetValue(position, out var cell)) cells.Add(position, cell = new Cell());
                cell.Values.Add(entry.Id, entry.Entity);
            }
        }

        private void Unindex(Entry entry)
        {
            entries.Remove(entry.Id);
            var area = areas[entry.Area];
            area.Remove(entry.Id);
            if (area.Count == 0) areas.Remove(entry.Area);
            foreach (var position in entry.Body)
            {
                var cell = cells[position];
                cell.Values.Remove(entry.Id);
                if (cell.Values.Count == 0) cells.Remove(position);
            }
        }

        private sealed class Entry
        {
            internal readonly IMiniMapSpawnEntity Entity;
            internal readonly EcEntityIdentifier Id;
            internal readonly int Area;
            internal readonly Pos2d[] Body;
            internal Entry(IMiniMapSpawnEntity entity, int area, Pos2d[] body)
            { Entity = entity; Id = entity.Id; Area = area; Body = body; }
        }

        private sealed class Cell : IMiniMapMarkCell
        {
            internal readonly Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity> Values =
                new Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity>();
            public bool ExistEntitys() => Values.Count > 0;
            public IEnumerable<IMiniMapSpawnEntity> Entitys => Values.Values.ToArray();
        }
    }
}
