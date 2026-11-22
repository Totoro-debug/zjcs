using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.HomeUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Official.HomeOutputDetailUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Presentation.Home
{
    // Reuses the saved activity ledger and original input dispatcher. Opening a
    // detail is read-only and cannot fabricate an activity or grant an item.
    public sealed class OfflineHomeOutputDetailServices : IOutputAdditionDetailServices,
        IProfessionTimeService, ICultureTextFormatter
    {
        sealed class DesktopPointer : IHomePointerSnapshot
        {
            public bool LeftDown => Input.GetMouseButtonDown(0);
            public Vector2 Position => Input.mousePosition;
            public int Width => Screen.width;
            public int Height => Screen.height;
        }
        sealed class PointerProxy : IHomePointerSnapshot
        {
            readonly OfflineHomeOutputDetailServices owner;
            internal PointerProxy(OfflineHomeOutputDetailServices owner) { this.owner = owner; }
            public bool LeftDown => owner.PointerSource.LeftDown;
            public Vector2 Position => owner.PointerSource.Position;
            public int Width => owner.PointerSource.Width;
            public int Height => owner.PointerSource.Height;
        }
        readonly OfflineHomeOutputAddition addition;
        readonly OfficialHomeOutputDetailAssets assets;
        readonly Func<GameObject, Transform, GameObject> instantiate;
        readonly Action<GameObject> close;
        readonly Func<string, string> fallback;
        readonly Func<DateTime> now;
        readonly IHomePointerSnapshot pointer;
        public IHomePointerSnapshot PointerSource { get; set; }
        readonly InputOperationModule input;
        readonly Dictionary<Action<Vector2>, Action<OnGlobalPointerDownResult>> listeners
            = new Dictionary<Action<Vector2>, Action<OnGlobalPointerDownResult>>();

        public OfflineHomeOutputDetailServices(OfflineHomeOutputAddition addition,
            Func<GameObject, Transform, GameObject> instantiate, Action<GameObject> close,
            Func<string, string> fallback, IHomePointerSnapshot pointer = null, Func<DateTime> now = null)
        {
            this.addition = addition ?? throw new ArgumentNullException(nameof(addition));
            this.instantiate = instantiate ?? throw new ArgumentNullException(nameof(instantiate));
            this.close = close ?? throw new ArgumentNullException(nameof(close));
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            PointerSource = pointer ?? new DesktopPointer();
            this.pointer = new PointerProxy(this);
            this.now = now ?? (() => DateTime.UtcNow);
            assets = OfficialHomeOutputDetailAssets.LoadDefault();
            input = new InputOperationModule(new[] { new GlobalPointerDownOperation(this.pointer, Debug.LogException) }, Debug.LogError) { Enable = true };
        }
        public Vector2 PointerPosition => pointer.Position;
        public DateTime Now => now();
        public LanguageType LanguageType => LanguageType.zh_CN;
        public List<(List<OutputAdditionInfo> propList, DateTime endTime)> Groups(OutputGroupType type) => addition.Groups(type);
        public void Tick() => input.Update();
        public void HookGlobalPointerDown(Action<Vector2> callback)
        {
            Action<OnGlobalPointerDownResult> listener = result => callback(result.ScreenPos);
            listeners.Add(callback, listener);
            ((IInputOperationModule)input).HookOnInput(listener);
        }
        public void UnhookGlobalPointerDown(Action<Vector2> callback)
        {
            if (!listeners.TryGetValue(callback, out var listener)) return;
            ((IInputOperationModule)input).UnhookOnInput(listener);
            listeners.Remove(callback);
        }
        public GameObject Instantiate(GameObject prefab, Transform parent)
        {
            var result = instantiate(prefab, parent);
            result.SetActive(true);
            return result;
        }
        public void Close(GameObject window) => close(window);
        public string Text(string key)
        {
            string value = assets.Text(key);
            return value == key ? fallback(key) : value;
        }
        public string GetText(string key) => Text(key);
        public string GetTextFormatted(string key, params object[] args) => string.Format(Text(key), args);
        public double CalcRemainSecond(DateTime endTime) => (endTime - Now).TotalSeconds;
        public object TimeSpanArgument(TimeSpan span, int format) => new CultureTimeSpanArg(span, (ETimeSpanFormatType)format, this);
        public event Action<long> DebugTimeOffset { add { } remove { } }
    }
}
