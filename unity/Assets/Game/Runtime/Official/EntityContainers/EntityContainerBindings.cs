using System;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using EcEntitySourceType = Zjcs.Official.World.Map13EntityVisibilityViews.EcEntitySourceType;

namespace Zjcs.Official.EntityContainers
{
    public interface IClientEntityContainer : IEntityLoadSharedContainer, IECViewMapRootLifetime
    {
        long Id { get; }
        int ClassId { get; }
        IDisposable SubscribeStartEntity(Action<ECViewEntityData, bool> callback);
        IDisposable SubscribeDestroyEntity(Action<ECViewEntityData, object> callback);
        IDisposable SubscribePrepareReplaceEntity(Action<ECViewEntityData, ECViewEntityData> callback);
        IDisposable SubscribeReplaceEntity(Action<ECViewEntityData, EcEntityIdentifier> callback);
    }
    public interface IClientEntitySource
    {
        EcEntitySourceType EntitySourceType { get; set; }
        EntityOwnershipData Ownership { get; set; }
    }
    public interface IClientEntityGrid { Pos2d Pos { get; set; } }
    public interface IClientEntityServices
    {
        long MainPlayerId { get; }
        ECViewEntityData GetCommonPrefab(int classId);
        ECViewEntityData Instantiate(ECViewEntityData prefab);
        void SetContainer(ECViewEntityData entity, IClientEntityContainer container);
        void SetIdentifier(ECViewEntityData entity, EcEntityIdentifier identifier);
        void Awake(ECViewEntityData entity);
        IClientEntitySource Source(ECViewEntityData entity);
        IClientEntityGrid Grid(ECViewEntityData entity);
        bool HasMoveAgent(MyEntity entity);
        void AddMoveAgent(MyEntity entity);
    }
}
