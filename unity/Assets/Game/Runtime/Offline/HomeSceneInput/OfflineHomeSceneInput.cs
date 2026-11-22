using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeSceneInput;

namespace Zjcs.Offline.HomeSceneInput
{
    // Explicit owner of the original operations. It reads OS input only when
    // the game chooses the desktop pointer; validation supplies its own frames.
    public sealed class OfflineHomeSceneInput : IDisposable
    {
        sealed class DesktopPointer : IHomeSceneClickPointer
        {
            public bool LeftDown => Input.GetMouseButtonDown(0);
            public bool LeftUp => Input.GetMouseButtonUp(0);
            public Vector2 Position => Input.mousePosition;
            public int Width => Screen.width;
            public int Height => Screen.height;
        }
        readonly WorldOwner world;
        readonly InputOperationModule module;
        readonly IHomeSceneClickContext context;
        bool disposed;
        public IInputOperationModule InputModule => module;
        public IHomeSceneInputWorld World => world;

        public OfflineHomeSceneInput(Func<Camera> camera, Func<EventSystem> events,
            IHomeSceneClickContext context, IHomeSceneClickPointer pointer = null,
            Action<Exception> reportException = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            world = new WorldOwner(camera, events);
            var operation = new GameObjectClickOperation(pointer ?? new DesktopPointer(),
                reportException ?? Debug.LogException, world);
            module = new InputOperationModule(new[] { operation }, Debug.LogError) { Enable = true };
        }
        public void Bind(ClickableHomeObj actor)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeSceneInput));
            if (!actor) throw new ArgumentNullException(nameof(actor));
            if (actor.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Bind native scene actors under an inactive staging root before activation.");
            actor.InputOperationModule = module;
            actor.HomeData = context;
        }
        public void Tick()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeSceneInput));
            module.Update();
        }
        public void LateTick() => world.ClearFrameCache();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; module.Enable = false; world.ClearFrameCache();
        }

        // Retains EventSystemManager's per-position, per-frame raycast cache
        // and original first-hit selection. The home owner supplies its camera
        // and EventSystem instead of creating another global service.
        sealed class WorldOwner : IHomeSceneInputWorld
        {
            readonly Func<Camera> camera;
            readonly Func<EventSystem> events;
            readonly Dictionary<Vector2, List<RaycastResult>> cache = new Dictionary<Vector2, List<RaycastResult>>();
            readonly Queue<List<RaycastResult>> pool = new Queue<List<RaycastResult>>();
            EventSystem observed;
            PointerEventData data;
            bool dirty;
            public Camera Camera => camera();
            public bool EventSystemValid => CurrentEvents();
            internal WorldOwner(Func<Camera> camera, Func<EventSystem> events)
            {
                this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
                this.events = events ?? throw new ArgumentNullException(nameof(events));
            }
            EventSystem CurrentEvents()
            {
                var current = events();
                if (current != observed)
                {
                    ClearFrameCache(); observed = current;
                    data = current ? new PointerEventData(current) : null;
                }
                return current;
            }
            public bool TryGetFirstRaycastResult(Vector2 screenPos, out RaycastResult result)
            {
                var current = CurrentEvents();
                if (!current) throw new InvalidOperationException("Check EventSystemValid before requesting native UI hits.");
                dirty = true;
                if (!cache.TryGetValue(screenPos, out var hits))
                {
                    if (!pool.TryDequeue(out hits)) hits = new List<RaycastResult>();
                    data.position = screenPos;
                    current.RaycastAll(data, hits);
                    cache.Add(screenPos, hits);
                }
                result = hits.Count == 0 ? default : hits[0];
                return hits.Count > 0;
            }
            internal void ClearFrameCache()
            {
                if (!dirty) return;
                dirty = false;
                foreach (var hits in cache.Values) { hits.Clear(); pool.Enqueue(hits); }
                cache.Clear();
            }
        }
    }
}
