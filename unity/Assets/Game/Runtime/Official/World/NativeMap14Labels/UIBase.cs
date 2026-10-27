using System;
using UnityEngine;

namespace Zjcs.Official.World.NativeMap14Labels
{
    // Original UIBase initialization/enable/disable ordering; logging uses Unity directly.
    public class UIBase : MonoBehaviour
    {
        protected bool _Inited;
        public IMap14LabelServices Services { get; set; }
        protected virtual void Start() { }
        protected virtual void OnDestroy() { }
        protected void InitWrapper(Action callback) { InitStart(); callback?.Invoke(); InitFinish(); }
        protected bool IsEnableAndInited() => _Inited && isActiveAndEnabled;
        protected void InitStart() { if (IsEnableAndInited()) _OnDisableAndInited(); }
        protected void InitFinish() { _Inited = true; if (IsEnableAndInited()) _OnEnableAndInited(); }
        protected virtual void OnEnable() { if (IsEnableAndInited()) _OnEnableAndInited(); }
        private void _OnEnableAndInited()
        {
            try { OnEnableAndInited(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        protected virtual void OnEnableAndInited() { }
        protected virtual void OnDisable() { if (_Inited) _OnDisableAndInited(); }
        private void _OnDisableAndInited()
        {
            try { OnDisableAndInited(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        protected virtual void OnDisableAndInited() { }
        public virtual void ResetInitState() => ResetInitState(true);
        public void ResetInitState(bool needDisable)
        {
            if (_Inited) { _Inited = false; if (needDisable && gameObject.activeInHierarchy) _OnDisableAndInited(); }
        }
    }
}
