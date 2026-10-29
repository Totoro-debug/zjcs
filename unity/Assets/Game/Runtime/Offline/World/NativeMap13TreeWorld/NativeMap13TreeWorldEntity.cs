using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13EntityStateViews;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityGridViews;
using Zjcs.Official.World.Map13EntityFadeHandlers;
using Zjcs.Official.World.NativeTerrain.Common;
using StageKind = Zjcs.Official.EntityWorldStages.StageComponentType;

namespace Zjcs.Offline.World.NativeMap13TreeEntities
{
    // Local data/events behind the original driver, ordered views and performances.
    // Navigation, rewards and persistence remain with the map's rule executor.
    public sealed class NativeMap13TreeWorldEntity : ECViewEntityBinding,
        IECWorldStageReplacementSource, IMap13EntityVisibilityBinding, IMap13EntityFadeOutBinding,
        IServiceProvider, IECViewInjection, IECViewMapRootLifetime, IECViewFrameSource,
        IEcEntityBufferEventHandleModule, IMap13FadeMapState
    {
        private readonly List<IMyEntityStartEventReceiver> starts = new List<IMyEntityStartEventReceiver>();
        private readonly List<IMyEntityRemoveEventReceiver> removes = new List<IMyEntityRemoveEventReceiver>();
        private readonly List<IMyEntityStartPostEventReceiver> posts = new List<IMyEntityStartPostEventReceiver>();
        private readonly List<IMyEntityRemovePreEventReceiver> pres = new List<IMyEntityRemovePreEventReceiver>();
        private readonly List<IMyEntityReplaceEventReceiver> replacements = new List<IMyEntityReplaceEventReceiver>();
        private DataInput data;
        private MyPlayableManager manager;
        private Clock clock;
        private Action<ECViewFrameUnit> updates, lates;
        private Action<EntityWorldStageUpdateEventMessage> stages;
        private bool started, removed, removing;
        public bool IsRemoving => removing && !removed;
        public override ECViewEntityData Entity => data;
        public IClientVisibilitySupporter ClientVisibilitySupporter { get; private set; }
        public Func<bool> FadeOutAction { get; set; }
        public bool Destroying => removed;
        public bool Initialized => data != null && !removed;
        public bool IsFront => true;
        public Map13LoadingPanelState LoadingPanel { get; } = new Map13LoadingPanelState { SplashStatus = SplashStatus.Hide };
        public NativeMap13TreeEntityBinding Binding { get; private set; }

        public void Initialize(NativeMap13TreeWorldAssets.Placement source, Vector2 origin,
            MyPlayableManager playables, int state, long playerId, NativeMap13TreeEntityBinding binding)
        {
            if (data != null) throw new InvalidOperationException("Original tree entity is already bound.");
            source.body.Init();
            data = new DataInput(source, origin, state);
            manager = playables ?? throw new ArgumentNullException(nameof(playables));
            clock = new Clock(playerId); Binding = binding;
        }
        public void EnsureStarted()
        {
            if (started || removed || !gameObject.activeInHierarchy) return;
            Binding.InitializeActive();
            foreach (var receiver in starts.ToArray()) receiver.MyEntity_OnStart(this, false);
            var grid = Binding.Driver.GetView<ECGridTransformView>();
            grid.ResetPosition(); grid.ResetRotation(); grid.ResetScale();
            foreach (var receiver in posts.ToArray()) receiver.MyEntity_OnStartPost(this);
            started = true;
        }
        public void Refresh(int state, bool visible)
        {
            if (removing) return;
            EnsureStarted();
            if (!started || removed) return;
            if (data.Stage.State != state)
            {
                int previous = data.Stage.State; data.Stage.State = state;
                stages?.Invoke(new EntityWorldStageUpdateEventMessage { StageComp = data.Shared, PreState = previous });
            }
            // This request composes with the original appearance/story requests.
            ClientVisibilitySupporter.SetEntityVisible(13000000 + data.Source.placementId, visible);
        }
        public bool RequestRemoval()
        {
            if (removed) return false;
            removing = true;
            if (!started || FadeOutAction == null || FadeOutAction()) { Remove(); return false; }
            return true;
        }
        public void SetGrid(Pos2d position)
        {
            var previous = data.Grid.Pos; data.Grid.Pos = position;
            data.Grid.PosUpdatedEvent.FireEvent(data.Grid, previous);
        }
        private void Update() { if (started && !removed) updates?.Invoke(default); }
        private void LateUpdate() { if (started && !removed) lates?.Invoke(default); }
        public void Remove()
        {
            if (removed) return;
            removed = true;
            if (started)
            {
                foreach (var receiver in pres.ToArray()) receiver.MyEntity_RemovePre(this);
                foreach (var receiver in removes.ToArray()) receiver.MyEntity_OnRemove(this);
            }
            started = false; updates = null; lates = null; stages = null;
        }
        private void OnDestroy() => Remove();
        public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value) => ClientVisibilitySupporter = value;
        public override void AddStartReceiver(IMyEntityStartEventReceiver value) => starts.Add(value);
        public override void RemoveStartReceiver(IMyEntityStartEventReceiver value) => starts.Remove(value);
        public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver value) => removes.Add(value);
        public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver value) => removes.Remove(value);
        public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver value) => posts.Add(value);
        public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver value) => posts.Remove(value);
        public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver value) => pres.Add(value);
        public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver value) => pres.Remove(value);
        public void AddReplaceReceiver(IMyEntityReplaceEventReceiver value) => replacements.Add(value);
        public void RemoveReplaceReceiver(IMyEntityReplaceEventReceiver value) => replacements.Remove(value);
        public object GetService(Type type)
        {
            if (type == typeof(IServiceProvider)) return this;
            if (type == typeof(IMyPlayableManager)) return manager;
            if (type == typeof(Map13PresentationServices)) return clock;
            return type.IsInstanceOfType(this) ? this : null;
        }
        public void Inject(object value)
        {
            foreach (var property in value.GetType().GetProperties().Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
            {
                var service = GetService(property.PropertyType);
                if (service != null) property.SetValue(value, service);
            }
        }
        public IDisposable SubscribeUpdate(Action<ECViewFrameUnit> value) { updates += value; return new Lease(() => updates -= value); }
        public IDisposable SubscribePreLateUpdate(Action<ECViewFrameUnit> value) { lates += value; return new Lease(() => lates -= value); }
        public IDisposable Subscribe<T>(ECViewEntityData entity, Action<T> value) where T : IEcEntityBufferEventContext
        {
            if (entity != data || typeof(T) != typeof(EntityWorldStageUpdateEventMessage))
                throw new InvalidOperationException("Original tree subscribed to an unbound local event.");
            var callback = (Action<EntityWorldStageUpdateEventMessage>)(object)value;
            stages += callback; return new Lease(() => stages -= callback);
        }
        private sealed class Lease : IDisposable
        {
            private Action dispose;
            public Lease(Action value) { dispose = value; }
            public void Dispose() { var previous = dispose; dispose = null; previous?.Invoke(); }
        }
        private sealed class Clock : Map13PresentationServices
        {
            private readonly long id;
            public Clock(long playerId) { id = playerId; }
            public override long PlayerId => id;
            public override DateTime GetDateTime() => DateTime.UtcNow;
        }
        private sealed class Ground : Map13GridGroundData
        {
            private readonly Vector2 origin;
            public Ground(Vector2 value) { origin = value; }
            public override Vector3 GridToPos(Pos2d grid) => new Vector3(origin.x + grid.X, origin.y + grid.Y, 0);
            public override Pos2d PosToGrid(Vector3 position) => new Pos2d(Mathf.RoundToInt(position.x - origin.x), Mathf.RoundToInt(position.y - origin.y));
        }
        private sealed class Body : Map13GridTransformInfo
        {
            private readonly NativeMap13TreeWorldAssets.Placement source;
            public Body(NativeMap13TreeWorldAssets.Placement value) { source = value; RealCenter = value.body.RealCenter; }
            public override List<Pos2d> GetBodyRange() => source.body.GetBodyRange();
        }
        private sealed class SharedState : ECWorldStageStateData
        {
            private readonly DataInput data;
            public SharedState(DataInput value) { data = value; }
            public override int State => data.Stage.State;
            public override OfficialStageStateInfo GetCurStateInfo() => State >= 0 && State < data.Source.stage.StateInfos.Count
                ? data.Source.stage.StateInfos[State] : null;
            public override int GetStateComponentIndex(StageKind type, int subStateIndex = 0) =>
                data.Stage.GetStateComponentIndex((Zjcs.Official.World.Map13EntityStateViews.StageComponentType)(int)type, subStateIndex);
        }
        private sealed class DataInput : ECViewEntityData
        {
            public readonly NativeMap13TreeWorldAssets.Placement Source;
            public readonly Map13WorldStageQuery Stage;
            public readonly Map13AwardItemQuery Awards;
            public readonly SharedState Shared;
            public readonly Map13VisibilityData Visibility = new Map13VisibilityData();
            public readonly Map13EntitySourceQuery EntitySource;
            public readonly Map13EntityInteractionQuery Interaction;
            public readonly Map13GridTransformData Grid;
            public DataInput(NativeMap13TreeWorldAssets.Placement source, Vector2 origin, int state)
            {
                Source = source; Stage = new Map13WorldStageQuery { State = state, Info = source.stageQuery };
                Awards = new Map13AwardItemQuery { Info = source.awards }; Shared = new SharedState(this);
                EntitySource = new Map13EntitySourceQuery { Entity = this };
                Interaction = new Map13EntityInteractionQuery { mEntSrcComp = EntitySource };
                Grid = new Map13GridTransformData { Pos = source.position, Direction = source.direction, FlipX = source.flipX,
                    Info = new Body(source), GridGround = new Ground(origin) };
            }
            public override int ClassId => Source.entityClassId;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(13, Source.placementId);
            public override T GetComponent<T>()
            {
                object value = typeof(T) == typeof(Map13WorldStageQuery) ? Stage : typeof(T) == typeof(Map13AwardItemQuery) ? Awards
                    : typeof(T) == typeof(ECWorldStageStateData) ? Shared : typeof(T) == typeof(Map13VisibilityData) ? Visibility
                    : typeof(T) == typeof(Map13EntitySourceQuery) ? EntitySource : typeof(T) == typeof(Map13EntityInteractionQuery) ? Interaction
                    : typeof(T) == typeof(Map13GridTransformData) ? Grid : null;
                return value == null ? default : (T)value;
            }
        }
    }
}
