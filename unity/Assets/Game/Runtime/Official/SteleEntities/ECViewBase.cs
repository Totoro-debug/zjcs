// Selected original EC view lifecycle. The services replace the entity/DI bridge.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.StelePlayables;

namespace Zjcs.Official.SteleEntities
{
    [Serializable] public struct Pos2d { public int X, Y; }

    public interface ISteleEntityViewServices
    {
        GameObject EntityObject { get; }
        int EntityClassId { get; }
        int WorldStageState { get; }
        Vector2 RealCenter { get; }
        Pos2d Grid { get; }
        Quaternion DirectionRotation { get; }
        bool FlipX { get; }
        bool IsMoving { get; }
        List<Pos2d> BodyRange { get; }
        IPeriodicityInteractionStateSource Periodicity { get; }
        IWorldStageOperationContext OperationContext { get; }
        Vector3 GridToPosition(Pos2d grid);
        Pos2d PositionToGrid(Vector3 position);
        void StopMovement();
        int GetStateComponentIndex(int componentType, int subStateIndex);
        MyPlayable Play(GameObject prefab, GameObject source, GameObject target);
        event Action<Pos2d> GridPositionChanged;
        event Action<int> WorldStageChanged;
        event Action EntityReplaced;
    }

    public interface IECView
    {
        int Order { get; }
        IECView Append { get; }
        IEnumerable<IECView> AppendMulti { get; }
        void Init(ECViewDriver bridge);
        void Load(ISteleEntityViewServices entity, bool isStatic);
        void Unload();
        void OnShow();
        void OnHide();
        void OnApplicationPause(bool pause);
    }

    [Serializable]
    public abstract class ECViewBase : IECView
    {
        [NonSerialized] private ECViewDriver mBridge;
        [NonSerialized] protected ISteleEntityViewServices mEntity;
        [NonSerialized] protected bool mIsStatic;
        protected GameObject mOwner => mBridge.gameObject;
        protected int mClassId => mEntity.EntityClassId;
        protected virtual int mOrder => int.MaxValue;
        protected virtual IECView mAppend => null;
        protected virtual IEnumerable<IECView> mAppendMulti => null;
        int IECView.Order => mOrder;
        IECView IECView.Append => mAppend;
        IEnumerable<IECView> IECView.AppendMulti => mAppendMulti;
        void IECView.Init(ECViewDriver bridge) => mBridge = bridge;
        void IECView.Load(ISteleEntityViewServices entity, bool isStatic)
        {
            mEntity = entity; mIsStatic = isStatic;
            try { Load(); } catch (Exception error) { Debug.LogException(error, mOwner); }
        }
        void IECView.Unload()
        {
            try { Unload(); } catch (Exception error) { Debug.LogException(error, mOwner); }
            mEntity = null;
        }
        void IECView.OnShow() { try { OnShow(); } catch (Exception error) { Debug.LogException(error, mOwner); } }
        void IECView.OnHide() { try { OnHide(); } catch (Exception error) { Debug.LogException(error, mOwner); } }
        void IECView.OnApplicationPause(bool pause) { try { OnApplicationPause(pause); } catch (Exception error) { Debug.LogException(error, mOwner); } }
        protected T GetView<T>() where T : class, IECView => mBridge.GetView<T>();
        protected virtual void Load() { }
        protected virtual void Unload() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnApplicationPause(bool pause) { }
    }
}
