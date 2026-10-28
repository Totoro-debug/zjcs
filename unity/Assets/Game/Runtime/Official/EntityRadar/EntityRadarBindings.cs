using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.EntityRadar
{
    public interface IRadarCharacterController
    { MyEntity MyEntity { get; } Pos2d GridPos { get; } }
    public interface IRadarCharacterManager
    {
        IRadarCharacterController MyMeUiCharacter { get; }
        event Action<IRadarCharacterController, IRadarCharacterController> OnMeMapControllerUpdated;
    }
    public interface IRadarRemoveEntityReceiver
    { void Container_OnEntityRemove(ECViewEntityData entity, object data); }
    public interface IRadarRemovalEvents
    { void Add(IRadarRemoveEntityReceiver receiver); void Remove(IRadarRemoveEntityReceiver receiver); }
    public interface IRadarGamePosData
    { bool ExistEntitys(); IEnumerable<ECViewEntityData> Entitys { get; } }
    public interface IRadarWorldQueries
    {
        int MapStageCostShowDistance { get; }
        IRadarGamePosData FindGamePosData(Pos2d position);
    }
    public sealed class RadarBodyPosInfo { public Pos2d Pos; }
    public interface IRadarBodyInfo
    { Vector2 RealCenter { get; } IEnumerable<RadarBodyPosInfo> BodyRangeInfos { get; } }
}
