using System;
using System.Collections;
using System.Linq;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    public sealed partial class MainlineSharedWorld
    {
        public object ClientCharacter { get; private set; }

        private object CharacterComponent(object entity, string name) => entity.GetType()
            .GetMethod("GetComponent", new[] { typeof(Type) }).Invoke(entity, new object[] { Assemblies.CommonType(name) });

        private object CharacterPosition(int x, int y)
        {
            var position = Activator.CreateInstance(Assemblies.CommonType("Pos2d"), x, y);
            if (!(bool)Root.Ground.GetType().GetMethod("InGround").Invoke(Root.Ground, new[] { position }))
                throw new ArgumentOutOfRangeException(nameof(x), "The local character must be inside the original map.");
            return position;
        }

        private void CreateClientCharacter(int x, int y)
        {
            var entity = CreateCharacterSnapshot(x, y);
            if (!(bool)Root.Container.GetType().GetMethod("AddEntity").Invoke(Root.Container, new[] { entity }))
                throw new InvalidOperationException("Original local character registration failed.");
            ClientCharacter = entity;
        }

        private long CharacterContainerId(object position)
        {
            int area = (int)Data.Navigation.GetType().GetMethod("GridToArea").Invoke(Data.Navigation, new[] { position });
            var parser = Configuration.Manager.GetParser(Assemblies.CommonType("MapRegionInfoParser"));
            int region = (int)parser.GetType().GetMethod("AreaToRegion").Invoke(parser, new object[] { Data.MapClassId, area });
            if (!clientRegions.TryGetValue(region, out int status))
                throw new InvalidOperationException("The local character's original region is absent.");
            return ClientContainerId(status);
        }

        private object CreateCharacterSnapshot(int x, int y)
        {
            object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
            void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);
            var position = CharacterPosition(x, y);
            var spawner = CharacterComponent(Root.Root, "MapCharacterSpawnerComponent");
            int classId = (int)Get(Get(spawner, "Info"), "CharacterClassId");
            if (classId != (int)Get(Data.Navigation, "CharacterClassId"))
                throw new InvalidOperationException("Original character spawner and map navigation differ.");
            var entity = Root.Instantiate(Data.GetCommonPrefab(classId));
            var grid = CharacterComponent(entity, "GridTransformComponent");
            var character = CharacterComponent(entity, "CharacterComponent");
            var mapCharacter = CharacterComponent(entity, "IMapCharacter");
            var statuses = CharacterComponent(entity, "MapRegionStatusComponent");
            if (grid == null || character == null || mapCharacter == null || statuses == null)
                throw new InvalidOperationException("The original local character template is incomplete.");
            var player = client.Player;
            var target = Activator.CreateInstance(Assemblies.CommonType("PlayerTargetData"), Get(player, "Id"));
            // Identity/profession come from the same original local player. All
            // other runtime fields retain the original new-template defaults.
            character.GetType().GetMethod("Init").Invoke(character, new[] { target,
                Get(player, "MyProfession"), Get(player, "MySafeProfession"), (object)0L, 0L, false, false });
            Set(grid, "MyPos", position);
            var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>)
                .MakeGenericType(Assemblies.CommonType("MapRegionStatus")));
            foreach (var region in clientRegions.OrderBy(pair => pair.Key))
                list.Add(Enum.ToObject(Assemblies.CommonType("MapRegionStatus"), region.Value));
            statuses.GetType().GetMethod("Set").Invoke(statuses, new object[] { list });
            int index = (int)Root.Container.GetType().GetMethod("NewEntityIndex").Invoke(Root.Container, null);
            Set(entity, "Id", Activator.CreateInstance(Assemblies.CommonType("EcEntityIdentifier"), CharacterContainerId(position), index));
            return entity;
        }

        public void SynchronizeClientCharacter(int x, int y)
        {
            RequireActive();
            if (!IsReady || ClientCharacter == null)
                throw new InvalidOperationException("Start the original local character before synchronizing its position.");
            var position = CharacterPosition(x, y);
            var id = NativeMap14SharedPrefabs.Get(ClientCharacter, "Id");
            long previousContainer = (long)NativeMap14SharedPrefabs.Get(id, "ContainerId");
            long container = CharacterContainerId(position);
            NativeMap14SharedPrefabs.Set(CharacterComponent(ClientCharacter, "GridTransformComponent"), "Pos", position);
            if (previousContainer == container) return;
            var target = CreateCharacterSnapshot(x, y);
            var source = Activator.CreateInstance(Assemblies.CommonType("MapCharacterRemoveSourceInfo"));
            NativeMap14SharedPrefabs.Set(source, "SourceOperate", Enum.Parse(Assemblies.CommonType("MapCharacterRemoveSourceOperate"), "ToOtherStatus"));
            NativeMap14SharedPrefabs.Set(source, "MapId", previousContainer);
            var remove = Activator.CreateInstance(Assemblies.CommonType("MapEntityRemoveMsg"));
            NativeMap14SharedPrefabs.Set(remove, "ContainerId", previousContainer);
            NativeMap14SharedPrefabs.Set(remove, "EntityId", id);
            NativeMap14SharedPrefabs.Set(remove, "UserData", source);
            clientConfiguration.Dispatch("MsgMapEntityRemove", remove);
            var add = Activator.CreateInstance(Assemblies.CommonType("MapEntityAddMsg"));
            NativeMap14SharedPrefabs.Set(add, "ContainerId", container);
            NativeMap14SharedPrefabs.Set(add, "Entity", target);
            clientConfiguration.Dispatch("MsgMapEntityAdd", add);
        }
    }
}
