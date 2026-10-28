using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.SteleEntities;
using Zjcs.Official.StelePlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;

namespace Zjcs.Offline.World.NativeMap12Steles
{
    public sealed class NativeMap12SteleEntity : MonoBehaviour, ISteleEntityViewServices, IPeriodicityInteractionStateSource
    {
        private NativeMap12SteleCatalog.Display display;
        private NativeMap12SteleCatalog.Placement placement;
        private NativeMap12StelePlayableScope playables;
        private ECViewDriver driver;
        private Vector2 origin;
        private Pos2d grid;
        private int direction, state, count;
        private bool initialized, removed;
        private IWorldStageOperationContext operationContext;
        public GameObject EntityObject => gameObject;
        public int EntityClassId => display.entityClassId;
        public int WorldStageState => state;
        public Vector2 RealCenter => display.realCenter;
        public Pos2d Grid => grid;
        public Quaternion DirectionRotation
        {
            get
            {
                switch (direction)
                {
                    case 0: return Quaternion.Euler(0, 0, 0);
                    case 2: return Quaternion.Euler(0, 0, 90);
                    case 1: return Quaternion.Euler(0, 0, 180);
                    case 3: return Quaternion.Euler(0, 0, 270);
                    default: Debug.LogError("Original DirToRotation: unsupported direction " + direction); return Quaternion.Euler(0, 0, 0);
                }
            }
        }
        public bool FlipX => false;
        public bool IsMoving => false;
        public List<Pos2d> BodyRange => display.bodyRange;
        public IPeriodicityInteractionStateSource Periodicity => this;
        public IWorldStageOperationContext OperationContext => operationContext;
        public ECViewDriver Driver => driver;
        public event Action<Pos2d> GridPositionChanged;
        public event Action<int> WorldStageChanged;
        public event Action EntityReplaced;
        public event Action<int, int> OnPeriodicityInteractionChanged;

        public void Initialize(NativeMap12SteleCatalog.Display originalDisplay,
            NativeMap12SteleCatalog.Placement originalPlacement, ECViewDriver originalDriver,
            NativeMap12StelePlayableScope scope, Vector2 mapOrigin, Vector2Int position,
            int originalDirection, int initialState, int initialCount, IWorldStageOperationContext windowContext)
        {
            if (initialized) throw new InvalidOperationException("Original stele entity already initialized.");
            display = originalDisplay ?? throw new ArgumentNullException(nameof(originalDisplay));
            placement = originalPlacement ?? throw new ArgumentNullException(nameof(originalPlacement));
            driver = originalDriver ? originalDriver : throw new ArgumentNullException(nameof(originalDriver));
            playables = scope ? scope : throw new ArgumentNullException(nameof(scope));
            origin = mapOrigin; grid = new Pos2d { X = position.x, Y = position.y };
            direction = originalDirection; state = initialState; count = initialCount;
            operationContext = windowContext;
            initialized = true;
            LoadIfActive();
        }
        private void OnEnable() => LoadIfActive();
        private void LoadIfActive()
        {
            if (!initialized || removed || !gameObject.activeInHierarchy || driver.Registered) return;
            driver.LoadEntity(this, false);
            var view = driver.GetView<ECGridTransformView>();
            view.ResetPosition(); view.ResetRotation(); view.ResetScale();
        }
        public void Refresh(int worldStageState, int periodicityCount)
        {
            if (removed) return;
            if (state != worldStageState)
            {
                int previous = state; state = worldStageState;
                if (driver.Registered) WorldStageChanged?.Invoke(previous);
            }
            if (count != periodicityCount)
            {
                count = periodicityCount;
                if (driver.Registered) OnPeriodicityInteractionChanged?.Invoke(placement.periodicityGroupId, count);
            }
        }
        public void SetGrid(Vector2Int position)
        {
            var previous = grid; grid = new Pos2d { X = position.x, Y = position.y };
            if (driver.Registered)
            {
                GridPositionChanged?.Invoke(previous);
                driver.GetView<ECGridTransformView>().SetPositionByGrid(grid);
            }
        }
        public Vector3 GridToPosition(Pos2d value) => new Vector3(origin.x + value.X + RealCenter.x, origin.y + value.Y + RealCenter.y, 0);
        public Pos2d PositionToGrid(Vector3 value) => new Pos2d
        { X = (int)((value.x - origin.x + 0.5f) / 1f) - (int)RealCenter.x,
          Y = (int)((value.y - origin.y + 0.5f) / 1f) - (int)RealCenter.y };
        public void StopMovement() => throw new InvalidOperationException("The original stele has no MoveAgent.");
        public int GetStateComponentIndex(int componentType, int subStateIndex) => state == 0 && subStateIndex == 0 && (componentType == 13 || componentType == 110) ? 0 : -1;
        public MyPlayable Play(GameObject prefab, GameObject source, GameObject target) => playables.Play(prefab, source, target);
        public int GetCurrentPeriodicityGroupId() => placement.periodicityGroupId;
        public int GetWorldPeriodicityInteractionCount(int groupId) => groupId == placement.periodicityGroupId ? count : 0;
        public int? GetPeriodicityMaxCount(int groupId) => groupId == placement.periodicityGroupId ? placement.periodicityMaxCount : (int?)null;
        public void NotifyReplaced() { if (driver.Registered) EntityReplaced?.Invoke(); }
        public void Remove()
        {
            if (removed) return;
            removed = true;
            if (driver) driver.RemoveEntity();
        }
        private void OnDestroy() => Remove();
    }
}
