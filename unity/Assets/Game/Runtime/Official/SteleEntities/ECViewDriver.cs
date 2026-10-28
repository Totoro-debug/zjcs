// Original ECViewDriver view expansion, ordering and lifecycle dispatch.
// The owning offline entity supplies start/remove instead of MyEntity events.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.SteleEntities
{
    [DefaultExecutionOrder(-1)]
    public sealed class ECViewDriver : MonoBehaviour
    {
        [SerializeReference] private List<IECView> mECViews;
        private bool prepared, registered;
        public bool Registered => registered;

        private void Awake() => Prepare();
        private void Prepare()
        {
            if (prepared) return;
            if (mECViews == null) throw new InvalidOperationException("Original stele view list is missing.");
            int index = 0;
            while (mECViews.Count - index > 0)
            {
                IECView view = mECViews[index++];
                if (view == null) continue;
                if (view.Append != null) mECViews.Add(view.Append);
                if (view.AppendMulti == null) continue;
                foreach (IECView item in view.AppendMulti) if (item != null) mECViews.Add(item);
            }
            mECViews.Sort((x, y) => x.Order.CompareTo(y.Order));
            foreach (IECView view in mECViews) view?.Init(this);
            prepared = true;
        }

        public void LoadEntity(ISteleEntityViewServices entity, bool newEntity)
        {
            if (registered) throw new InvalidOperationException("Original entity views are already loaded.");
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            Prepare(); registered = true;
            foreach (IECView view in mECViews) view?.Load(entity, !newEntity);
        }

        public void RemoveEntity()
        {
            if (!registered) return;
            foreach (IECView view in mECViews) view?.Unload();
            registered = false;
        }

        public T GetView<T>() where T : class, IECView
        {
            if (mECViews != null) foreach (IECView view in mECViews) if (view is T result) return result;
            return null;
        }
        private void OnEnable() { if (registered) foreach (IECView view in mECViews) view?.OnShow(); }
        private void OnDisable() { if (registered) foreach (IECView view in mECViews) view?.OnHide(); }
        private void OnApplicationPause(bool pause) { if (registered) foreach (IECView view in mECViews) view.OnApplicationPause(pause); }
        private void OnDestroy() => RemoveEntity();
    }
}
