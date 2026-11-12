using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeMainUiControls;

namespace Zjcs.Offline.HomeHudUI
{
    // One owned desktop input scope. Native HUD listeners retain the original
    // dispatcher's ordering, lazy unsubscribe and callback exception isolation.
    public sealed class OfflineHomeHudInput : IHomeMainUiInput, IDisposable
    {
        private sealed class DesktopPointer : IHomePointerSnapshot
        {
            public bool LeftDown => Input.GetMouseButtonDown(0);
            public Vector2 Position => Input.mousePosition;
            public int Width => Screen.width;
            public int Height => Screen.height;
        }

        private readonly InputOperationModule module;
        private readonly IInputOperationModule operations;
        private readonly Dictionary<Action<HomeUiGlobalPointerDownResult>, Action<OnGlobalPointerDownResult>> callbacks =
            new Dictionary<Action<HomeUiGlobalPointerDownResult>, Action<OnGlobalPointerDownResult>>();
        private bool disposed;

        public OfflineHomeHudInput(IHomePointerSnapshot pointer = null,
            Action<Exception> reportException = null, Action<string> reportDuplicate = null)
        {
            module = new InputOperationModule(new IInputOperation[] {
                new GlobalPointerDownOperation(pointer ?? new DesktopPointer(), reportException ?? Debug.LogException)
            }, reportDuplicate ?? Debug.LogError);
            operations = module;
            module.Enable = true;
        }

        public void Update()
        {
            ThrowIfDisposed();
            module.Update();
        }

        public void HookGlobalPointerDown(Action<HomeUiGlobalPointerDownResult> listener)
        {
            ThrowIfDisposed();
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            if (!callbacks.TryGetValue(listener, out var callback)) {
                callback = value => listener(new HomeUiGlobalPointerDownResult { ScreenPos = value.ScreenPos });
                callbacks.Add(listener, callback);
            }
            operations.HookOnInput(callback);
        }

        public void UnhookGlobalPointerDown(Action<HomeUiGlobalPointerDownResult> listener)
        {
            if (disposed || listener == null) return;
            if (!callbacks.TryGetValue(listener, out var callback)) return;
            operations.UnhookOnInput(callback);
            callbacks.Remove(listener);
        }

        public long BlockGlobalPointerDown()
        {
            ThrowIfDisposed();
            return operations.Block<OnGlobalPointerDownResult>();
        }

        public void Unblock(long signature)
        {
            ThrowIfDisposed();
            operations.Unblock(signature);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeHudInput));
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var callback in callbacks.Values) operations.UnhookOnInput(callback);
            callbacks.Clear();
            module.Enable = false;
            disposed = true;
        }
    }
}
