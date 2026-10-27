using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public struct OfficialBattlePointerFrame
    {
        public Vector2 Position;
        public bool Down, Held, Up;
    }

    /// <summary>
    /// Desktop input adapter for the recovered preparation collider/area. It retains the original
    /// drag time, distance, free-position preview, nearest-grid selection and failed-drop rollback.
    /// Owned-unit acceptance is handled by the explicit local preparation policy.
    /// </summary>
    internal sealed class OfficialBattlePreparationView : IDisposable
    {
        private readonly FightBattleCheckpoint checkpoint;
        private readonly OfficialBattleSceneView scene;
        private readonly OfficialBattleGridMapper mapper;
        private readonly OfficialBattlePreparationCatalog catalog;
        private readonly FightBattlePreparationPositions positions;
        private readonly GameObject area;
        private readonly Dictionary<int, GameObject> pointers = new Dictionary<int, GameObject>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private int draggingId;
        private Vector2 pressPosition;
        private float elapsed;
        private bool dragging;
        private FightGridPoint target;

        public event Action PositionChanged;

        public OfficialBattlePreparationView(FightBattleCheckpoint checkpoint, Transform parent,
            OfficialBattleSceneView scene, OfficialBattleGridMapper mapper, OfficialBattleSessionOptions options)
        {
            this.checkpoint = checkpoint;
            this.scene = scene;
            this.mapper = mapper;
            catalog = OfficialBattlePreparationCatalog.Load();
            FightBattlePreparationRange range = options.ChangePositionWidth > 0 && options.ChangePositionHeight > 0
                ? new FightBattlePreparationRange(new FightGridPoint(options.ChangePositionCenterX,
                    options.ChangePositionCenterY), options.ChangePositionWidth, options.ChangePositionHeight,
                    new FightGridPoint(options.ChangePositionWidth / 2, options.ChangePositionHeight / 2))
                : catalog.RangeFor(checkpoint.StageId, new FightGridPoint(options.ChangePositionCenterX,
                    options.ChangePositionCenterY));
            positions = new FightBattlePreparationPositions(checkpoint, new[] { range }, options.ControllableUnitIds);
            area = UnityEngine.Object.Instantiate(catalog.AreaPrefab, parent, false);
            area.transform.position = mapper.GridToWorld(range.Center);
            area.transform.localPosition += new Vector3(FightBattlePreparationRange.RangeCenterOffset(range.Width),
                FightBattlePreparationRange.RangeCenterOffset(range.Height), 0);
            area.GetComponent<SpriteRenderer>().size = new Vector2(range.Width, range.Height);
            // Other battle renderers already flatten the official sorting-layer list into Default.
            var sorting = area.AddComponent<SortingGroup>();
            sorting.sortingLayerID = 0;
            sorting.sortingOrder = 18 * 1000 - 1;
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                if (!positions.CanControl(unit.Id)) continue;
                // The source player collider is only bound to the player. NPC-specific colliders
                // must be recovered from their own prefab; never substitute this player's shape.
                if (unit.Id != 1) continue;
                var pointer = UnityEngine.Object.Instantiate(catalog.PlayerPointerPrefab, parent, false);
                pointer.transform.position = mapper.GridToWorld(new FightGridPoint(unit.X, unit.Y));
                pointers.Add(unit.Id, pointer);
            }
        }

        public void SetPreparing(bool value)
        {
            if (!value) CancelDrag();
            area.SetActive(value);
            foreach (GameObject pointer in pointers.Values) pointer.SetActive(value);
        }

        public void Tick(OfficialBattlePointerFrame input, float delta)
        {
            Camera camera = Camera.main;
            if (camera == null || !area.activeInHierarchy) return;
            if (input.Down && draggingId == 0)
            {
                if (EventSystem.current != null)
                {
                    uiHits.Clear();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = input.Position }, uiHits);
                    if (uiHits.Count > 0) return;
                }
                Physics.SyncTransforms();
                Ray ray = camera.ScreenPointToRay(input.Position);
                float nearest = float.MaxValue;
                foreach (var item in pointers)
                    foreach (Collider collider in item.Value.GetComponentsInChildren<Collider>())
                        if (collider.Raycast(ray, out RaycastHit hit, float.MaxValue) && hit.distance < nearest)
                        {
                            draggingId = item.Key;
                            nearest = hit.distance;
                        }
                if (draggingId != 0)
                {
                    pressPosition = input.Position;
                    elapsed = 0;
                    dragging = false;
                }
            }
            if (draggingId == 0) return;
            elapsed += Mathf.Max(0, delta);
            if (input.Held && elapsed > catalog.DownKeepTime
                && Vector2.Distance(pressPosition, input.Position) > catalog.DragThreshold
                && mapper.TryScreenToWorld(camera, input.Position, out Vector3 world))
            {
                dragging = true;
                scene.PreviewPreparationPosition(draggingId, world);
                if (!positions.TryPreview(draggingId, mapper.WorldToGrid(world), out target))
                {
                    FightBattleUnitState unit = checkpoint.Units.Find(value => value.Id == draggingId);
                    target = new FightGridPoint(unit.X, unit.Y);
                }
            }
            if (!input.Up) return;
            bool changed = dragging && Vector2.Distance(pressPosition, input.Position) > catalog.DragThreshold
                && positions.TryMove(draggingId, target, FightBattlePhase.Preparing, false);
            CancelDrag();
            foreach (var item in pointers)
            {
                FightBattleUnitState unit = checkpoint.Units.Find(value => value.Id == item.Key);
                item.Value.transform.position = mapper.GridToWorld(new FightGridPoint(unit.X, unit.Y));
            }
            if (changed) PositionChanged?.Invoke();
        }

        private void CancelDrag()
        {
            if (draggingId != 0) scene.Refresh(checkpoint);
            draggingId = 0;
            dragging = false;
        }

        public void Dispose()
        {
            CancelDrag();
            UnityEngine.Object.Destroy(area);
            foreach (GameObject pointer in pointers.Values) UnityEngine.Object.Destroy(pointer);
            pointers.Clear();
        }
    }
}
