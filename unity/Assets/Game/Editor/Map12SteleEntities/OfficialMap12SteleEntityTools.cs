using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.SteleEntities;
using Zjcs.Official.StelePlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Offline.World.NativeMap12Steles;
using Zjcs.Offline.World.NativeMap13EntityAssets;

namespace Zjcs.Editor.Map12SteleEntities
{
    public static class OfficialMap12SteleEntityTools
    {
        // This fixture checks imported native serialization and selected view
        // services. Actual map input, playback/rendering and saves need Play Mode.
        public static void Validate()
        {
            var catalog = NativeMap12SteleCatalog.Load();
            var effects = OfficialStelePlayableCatalog.Load();
            var sprites = Resources.Load<NativeMap13EntitySpriteCatalog>("Map12/Native/stele-sprite-catalog");
            using (var library = new NativeMap13EntitySpriteLibrary(sprites))
            {
                foreach (var display in catalog.displays)
                {
                    Require(display.prefab.GetComponentsInChildren<Transform>(true).Length == 4,
                        "Original stele four-node hierarchy did not import.");
                    foreach (var node in display.prefab.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0,
                            "Original stele native component did not import.");
                    foreach (string path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(display.prefab), true))
                        Require(AssetDatabase.LoadMainAssetAtPath(path) != null, "Native stele dependency failed to import: " + path);
                    var owner = new GameObject("Original stele view fixture");
                    try
                    {
                        var instance = UnityEngine.Object.Instantiate(display.prefab, owner.transform, false);
                        var binding = instance.GetComponentInChildren<NativeMap13EntitySpriteBinding>(true);
                        Require(binding != null && binding.Renderer != null, "Original stele SpriteRenderer binding is missing.");
                        binding.Bind(library);
                        Require(binding.Renderer.sprite != null && binding.Renderer.sharedMaterial != null
                            && binding.Renderer.sharedMaterial.shader != null && binding.Renderer.sharedMaterial.shader.isSupported
                            && binding.Renderer.sortingLayerID == 1182434123 && binding.Renderer.sortingOrder == 0,
                            "Original stele Sprite/material/sorting binding is unavailable.");
                        var driver = instance.GetComponent<ECViewDriver>();
                        var state = new ViewServices(owner, display.entityClassId);
                        var expected = effects.Find(display.entityClassId);
                        driver.LoadEntity(state, false);
                        Require(state.Played.SequenceEqual(new[] { expected.mCanInteractionPlayable }),
                            "Deserialized stele views did not select the original available loop.");
                        var attach = owner.GetComponent<AttachAgent>();
                        Require(attach != null && attach.GetBone("CommonEffectSlot") == instance.transform.Find("CommonEffectSlot")
                            && attach.GetBone("Statues") == instance.transform.Find("Model/Statues"),
                            "Original attachment view did not initialize before playback.");
                        var grid = driver.GetView<ECGridTransformView>();
                        var stage = driver.GetView<ECWorldStageView>();
                        Require(grid != null && stage != null && stage.CanInteractAfterMove()
                            && stage.GetCurStateCfg() != null && stage.GetStateComponentIndex(13) == 0,
                            "Original grid/provider/state registry did not deserialize.");
                        grid.ResetPosition();
                        Vector3 position = state.GridToPosition(state.Grid);
                        Require(owner.transform.localPosition == new Vector3(position.x, position.y, position.y + position.x * 0.001f),
                            "Original stele grid depth formula changed.");
                        state.Notify(111, 1);
                        Require(state.Played.Count == 1, "Unrelated periodicity group restarted the stele.");
                        state.Count = 1; state.Notify(110, 1);
                        Require(state.Played.SequenceEqual(new[] { expected.mCanInteractionPlayable, expected.mEndInteractionPlayable })
                            && stage.State == 0, "Personal completion changed the original global state or end transition.");
                        driver.RemoveEntity();
                        Require(state.SubscriberCount == 0, "Removed stele views retained local event subscriptions.");
                        int played = state.Played.Count;
                        driver.LoadEntity(state, false);
                        Require(state.Played.Count == played, "Cold completed stele replayed an original performance.");
                        driver.RemoveEntity();
                    }
                    finally { UnityEngine.Object.DestroyImmediate(owner); }
                }
            }
            Debug.Log("Map12 stele imported prefabs, exact Sprite bindings, managed-reference views, original bones/depth, periodicity and unload checks passed. Actual map/playback acceptance remains separate.");
        }

        private sealed class ViewServices : ISteleEntityViewServices, IPeriodicityInteractionStateSource
        {
            public ViewServices(GameObject owner, int classId) { EntityObject = owner; EntityClassId = classId; }
            public GameObject EntityObject { get; }
            public int EntityClassId { get; }
            public int WorldStageState => 0;
            public Vector2 RealCenter => Vector2.zero;
            public Pos2d Grid => new Pos2d { X = 170, Y = 80 };
            public Quaternion DirectionRotation => Quaternion.identity;
            public bool FlipX => false;
            public bool IsMoving => false;
            public List<Pos2d> BodyRange => new List<Pos2d> { new Pos2d() };
            public IPeriodicityInteractionStateSource Periodicity => this;
            public IWorldStageOperationContext OperationContext => null;
            public readonly List<GameObject> Played = new List<GameObject>();
            public int Count;
            public event Action<Pos2d> GridPositionChanged;
            public event Action<int> WorldStageChanged;
            public event Action EntityReplaced;
            public event Action<int, int> OnPeriodicityInteractionChanged;
            public int SubscriberCount => (GridPositionChanged?.GetInvocationList().Length ?? 0)
                + (WorldStageChanged?.GetInvocationList().Length ?? 0) + (EntityReplaced?.GetInvocationList().Length ?? 0)
                + (OnPeriodicityInteractionChanged?.GetInvocationList().Length ?? 0);
            public Vector3 GridToPosition(Pos2d value) => new Vector3(value.X - 37, value.Y - 12, 0);
            public Pos2d PositionToGrid(Vector3 value) => throw new NotSupportedException();
            public void StopMovement() => throw new NotSupportedException();
            public int GetStateComponentIndex(int type, int subState) => type == 13 && subState == 0 ? 0 : -1;
            public MyPlayable Play(GameObject prefab, GameObject source, GameObject target)
            {
                Require(source == target && source.transform.parent.gameObject == EntityObject
                    && EntityObject.GetComponent<AttachAgent>()?.GetBone("CommonEffectSlot") != null,
                    "Original stele source/target or view initialization order changed.");
                Played.Add(prefab); return null;
            }
            public int GetCurrentPeriodicityGroupId() => 110;
            public int GetWorldPeriodicityInteractionCount(int group) => Count;
            public int? GetPeriodicityMaxCount(int group) => 1;
            public void Notify(int group, int count) => OnPeriodicityInteractionChanged?.Invoke(group, count);
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
