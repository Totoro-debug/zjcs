using System;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.MapRootVisibility;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.World.NativeMap14RootVisibility
{
    // Required formal-host inputs. Missing original player or transfer-view
    // binding is an error; this boundary supplies no default gameplay state.
    public sealed class NativeMap14FogPlayerInputs : IFogVisibilityPlayer
    {
        private readonly Func<object> character;
        private readonly Func<EcEntityIdentifier, MyEntity> entity;
        private readonly Func<MyEntity, IFogTransferClient> transfer;
        private readonly Func<Pos2d?> uiPosition;
        private readonly NativeMap14SharedAssemblies shared;
        private object previousCharacter;
        private CharacterInput previousView;
        public NativeMap14FogPlayerInputs(NativeMap14SharedAssemblies shared, Func<object> character,
            Func<EcEntityIdentifier, MyEntity> entity, Func<MyEntity, IFogTransferClient> transfer, Func<Pos2d?> uiPosition)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            this.character = character ?? throw new ArgumentNullException(nameof(character));
            this.entity = entity ?? throw new ArgumentNullException(nameof(entity));
            this.transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
            this.uiPosition = uiPosition ?? throw new ArgumentNullException(nameof(uiPosition));
        }
        public FogCharacterData Character
        {
            get
            {
                var original = character() ?? throw new InvalidOperationException("Original map character is not bound.");
                if (!ReferenceEquals(original, previousCharacter))
                { previousCharacter = original; previousView = new CharacterInput(shared, original); }
                return previousView;
            }
        }
        public MyEntity GetEntity(EcEntityIdentifier id) => entity(id);
        public IFogTransferClient GetTransferClient(MyEntity value) => transfer(value);
        public Pos2d? MyMeUiGridPos => uiPosition();
        private static object Get(object value, string name) => NativeMap14SharedPrefabs.Get(value, name);
        private sealed class CharacterInput : FogCharacterData
        {
            private readonly NativeMap14SharedAssemblies shared;
            private readonly object original;
            public CharacterInput(NativeMap14SharedAssemblies shared, object original)
            {
                if (!shared.CommonType("CharacterComponent").IsInstanceOfType(original)) throw new ArgumentException("Original character component required.");
                this.shared = shared; this.original = original;
            }
            public override Pos2d GridPos
            {
                get { var pos = Get(Get(original, "GridTransform"), "Pos"); return new Pos2d((int)Get(pos, "X"), (int)Get(pos, "Y")); }
            }
            public override EcEntityIdentifier FindWorldTransferAction()
            {
                var agent = original.GetType().GetMethod("GetComponent", new[] { typeof(Type) }).Invoke(original, new object[] { shared.CommonType("ActionAgentComponent") });
                var action = Enum.Parse(shared.CommonType("ActionType"), "WorldTransfer");
                var id = agent.GetType().GetMethod("FindAction", new[] { shared.CommonType("ActionType") }).Invoke(agent, new[] { action });
                return new EcEntityIdentifier((long)Get(id, "ContainerId"), (int)Get(id, "EntityId"));
            }
        }
    }
}
