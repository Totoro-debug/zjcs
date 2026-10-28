using System;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // UIBase's initialization/enable/disable order, with Unity logging in place
    // of the client's injected logger and no online window service dependency.
    public abstract class WorldInitializedUI : MonoBehaviour
    {
        protected bool Inited;

        protected void InitWrapper(Action initialize)
        {
            if (Inited && isActiveAndEnabled) InvokeLifecycle(OnDisableAndInited);
            initialize?.Invoke();
            Inited = true;
            if (isActiveAndEnabled) InvokeLifecycle(OnEnableAndInited);
        }

        protected virtual void OnEnable()
        {
            if (Inited && isActiveAndEnabled) InvokeLifecycle(OnEnableAndInited);
        }

        protected virtual void OnDisable()
        {
            if (Inited) InvokeLifecycle(OnDisableAndInited);
        }

        protected virtual void OnEnableAndInited() { }
        protected virtual void OnDisableAndInited() { }

        public void ResetInitState(bool needDisable = true)
        {
            if (!Inited) return;
            Inited = false;
            if (needDisable && gameObject.activeInHierarchy) InvokeLifecycle(OnDisableAndInited);
        }

        private void InvokeLifecycle(Action action)
        {
            try { action(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
