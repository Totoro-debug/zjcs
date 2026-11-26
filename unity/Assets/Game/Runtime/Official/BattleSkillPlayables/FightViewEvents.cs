// Original fight-view dispatch, converter and handler lifecycle; local services
// replace the original fight-root container and dependency-injection package.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Official.BattleSkillPlayables
{
    public abstract class FightViewEventArgs { }
    public abstract class FightViewArgsConverter { }
    public abstract class FightViewArgsConverter<TData> : FightViewArgsConverter
    {
        public abstract bool TryConvert(FightViewEventArgs args, out TData data);
    }

    public interface IFightViewHandlerHost
    {
        IServiceProvider ServiceProvider { get; }
        GameObject Owner { get; }
        ECViewEntityData HostEntity { get; }
        T GetView<T>() where T : ECViewBase;
    }

    [Serializable]
    public abstract class FightViewHandler
    {
        public abstract void Init(IFightViewHandlerHost host);
        public abstract void Handle(FightViewEventArgs args);
        public abstract void Release();
        public virtual void TryGetPlayables(ref HashSet<GameObject> playables) { }
    }

    [Serializable]
    public abstract class FightViewHandlerBase<TData> : FightViewHandler
    {
        [SerializeReference, SerializeField] protected FightViewArgsConverter mConverter;
        private FightViewArgsConverter<TData> mTypedConverter;

        public override void Init(IFightViewHandlerHost host)
        {
            ValidateConverter();
            try { OnInit(host); }
            catch (Exception error) { Debug.LogException(error); }
        }

        public override void Release()
        {
            try { OnRelease(); }
            catch (Exception error) { Debug.LogException(error); }
            mTypedConverter = null;
        }

        public override void Handle(FightViewEventArgs args)
        {
            if (mTypedConverter == null) return;
            try { if (mTypedConverter.TryConvert(args, out TData data)) HandleData(data); }
            catch (Exception error) { Debug.LogException(error); }
        }

        protected virtual void OnInit(IFightViewHandlerHost host) { }
        protected virtual void OnRelease() { }
        protected abstract void HandleData(TData data);

        private void ValidateConverter()
        {
            if (mConverter == null)
                Debug.LogWarning("[FightView] " + GetType().Name + " has no converter");
            else if (mConverter is FightViewArgsConverter<TData> typed) mTypedConverter = typed;
            else Debug.LogWarning("[FightView] " + GetType().Name + " converter type mismatch: expected FightViewArgsConverter<"
                + typeof(TData).Name + ">, got " + mConverter.GetType().Name);
        }
    }

    [Serializable]
    public abstract class ECFightViewEventSourceBase<TArgs> : ECViewBase, IFightViewHandlerHost,
        IFightGetUsePlayable where TArgs : FightViewEventArgs, new()
    {
        [SerializeField, SerializeReference] protected List<FightViewHandler> mHandlers;
        protected readonly TArgs mSharedArgs = new TArgs();
        private bool mDispatching, mBound;
        IServiceProvider IFightViewHandlerHost.ServiceProvider => mServiceProvider;
        GameObject IFightViewHandlerHost.Owner => mOwner;
        ECViewEntityData IFightViewHandlerHost.HostEntity => mEntity;
        T IFightViewHandlerHost.GetView<T>() => GetView<T>();

        public void TryGetPlayables(ref HashSet<GameObject> playables)
        {
            if (mHandlers == null) return;
            foreach (var handler in mHandlers) handler?.TryGetPlayables(ref playables);
        }

        protected override void Load()
        {
            base.Load();
            if (!TryBind(out string error))
            {
                Debug.LogError("[FightView] " + GetType().Name + " bind failed on " + mOwner.name + ": " + error);
                return;
            }
            mBound = true;
            if (mHandlers == null) return;
            foreach (var handler in mHandlers) handler?.Init(this);
        }

        protected override void Unload()
        {
            if (mBound)
            {
                if (mHandlers != null) foreach (var handler in mHandlers) handler?.Release();
                Unbind();
                mBound = false;
            }
            Reset(mSharedArgs);
            base.Unload();
        }

        protected void Dispatch()
        {
            if (mDispatching)
            {
                Debug.LogError("[FightView] " + GetType().Name + " re-entered during dispatch on " + mOwner?.name
                    + "; drop this event to avoid mSharedArgs corruption");
                return;
            }
            mDispatching = true;
            try
            {
                if (mHandlers == null) return;
                foreach (var handler in mHandlers) handler?.Handle(mSharedArgs);
            }
            finally { Reset(mSharedArgs); mDispatching = false; }
        }

        protected abstract bool TryBind(out string error);
        protected abstract void Unbind();
        protected abstract void Reset(TArgs args);
    }
}
