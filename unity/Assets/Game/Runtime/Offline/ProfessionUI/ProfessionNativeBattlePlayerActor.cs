using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Official.ProfessionUI;
using PlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAnimatorAppearance;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Current original model/controller branch of entity 3100. The owning
    // battle view still supplies ECS, HUD, shadow, role lifecycle and services.
    // Unity owns Animator/LateUpdate; no second manual body tick is exposed.
    public sealed class ProfessionNativeBattlePlayerActor : IDisposable
    {
        [Serializable] private sealed class PrivateSettings { public bool mResetAnimatorInitedOnDisable; }
        private readonly HashSet<RuntimeAnimatorController> ownedControllers = new HashSet<RuntimeAnimatorController>();
        private RuntimeAnimatorController original;
        private bool configured, disposed;
        public GameObject Root { get; private set; }
        public Animator Animator { get; private set; }
        public AnimationController Controller { get; private set; }
        public PlayerAppearance Appearance { get; private set; }
        public ProfessionNativeSceneRenderer Renderer { get; private set; }

        public ProfessionNativeBattlePlayerActor(GameObject modelPrefab, Transform parent,
            OfflineProfessionScenePlayerBindings bindings, long playerId)
        {
            if (!modelPrefab || bindings == null || playerId <= 0) throw new ArgumentException("Original Game model, player services and identity are required.");
            var asset = Resources.Load<TextAsset>("Combat/player-animation-binding");
            if (!asset) throw new InvalidOperationException("Current original player application binding is missing.");
            var data = Obj(OfficialEquipmentJson.Parse(asset.text));
            if (!Equals(data["manifest"], "884_156692")) throw new InvalidOperationException("Original player binding version differs.");
            var row = Obj(data["player"]); var transforms = Arr(row["transforms"]).Select(Obj).ToArray();
            if (Convert.ToInt32(row["entityId"]) != 3100 || transforms.Length != 4
                || (string)row["animatorPath"] != "3100_view/body/PlayerModel_Game")
                throw new InvalidOperationException("Current original 3100 model/controller chain differs.");
            try
            {
                Root = new GameObject((string)transforms[0]["name"]); Root.SetActive(false);
                Root.transform.SetParent(parent, false); ApplyNode(Root, transforms[0]);
                Transform current = Root.transform;
                for (int index = 1; index < transforms.Length - 1; index++)
                {
                    var node = new GameObject((string)transforms[index]["name"]);
                    node.transform.SetParent(current, false); ApplyNode(node, transforms[index]); current = node.transform;
                }
                var model = Object.Instantiate(modelPrefab, current, false);
                ApplyNode(model, transforms[transforms.Length - 1]);
                if (bindings.BindTree(model) != 1) throw new InvalidOperationException("Bind exactly one original Game player.");
                configured = true;
                Appearance = model.GetComponent<PlayerAppearance>(); Animator = model.GetComponent<Animator>();
                Renderer = model.GetComponent<ProfessionNativeSceneRenderer>();
                original = Animator.runtimeAnimatorController;
                if (Root.transform.Find((string)row["animatorPath"]) != Animator.transform)
                    throw new InvalidOperationException("Original player Animator belongs to another node.");
                var fields = Obj(row["fields"]);
                Controller = Root.AddComponent<AnimationController>();
                Controller.myAnimator = Animator;
                Controller.InitOnAwake = Flag(fields, "InitOnAwake");
                Controller.CreateOverrideController = Flag(fields, "CreateOverrideController");
                Controller.NotCreateGraph = Flag(fields, "NotCreateGraph");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new PrivateSettings {
                    mResetAnimatorInitedOnDisable = Flag(fields, "mResetAnimatorInitedOnDisable") }), Controller);
                Controller.enabled = Flag(fields, "m_Enabled");
                Controller.OnRuntimeAnimatorControllerChanged += RememberController;
                // These are the 3100 embedded-model cross-references, distinct
                // from the standalone Game prefab's direct Animator reference.
                Appearance.AnimController = Controller; Appearance.Animator = null;
                var previous = ProfessionAppearanceSkinClipBindings.Current;
                try
                {
                    ProfessionAppearanceSkinClipBindings.Current = new OfflineProfessionSceneSkinClipBindings();
                    Root.SetActive(true);
                }
                finally { ProfessionAppearanceSkinClipBindings.Current = previous; }
                Appearance.Init(playerId);
                if (!Appearance.Inited || !Controller.myInited)
                    throw new InvalidOperationException("Original player appearance/controller initialization did not complete.");
                Renderer.UpdateAnimationAndMesh();
            }
            catch { Dispose(); throw; }
        }

        private void RememberController()
        {
            var value = Controller.RuntimeAnimatorController;
            if (value && value != original && Controller.CreateOverrideController) ownedControllers.Add(value);
        }

        private static void ApplyNode(GameObject node, Dictionary<string, object> row)
        {
            var fields = Obj(row["fields"]); var go = Obj(row["gameObject"]);
            node.name = (string)row["name"]; node.layer = Convert.ToInt32(go["m_Layer"]);
            node.transform.localPosition = Vector(Obj(fields["m_LocalPosition"]));
            node.transform.localScale = Vector(Obj(fields["m_LocalScale"]));
            var q = Obj(fields["m_LocalRotation"]);
            node.transform.localRotation = new Quaternion(Number(q, "x"), Number(q, "y"), Number(q, "z"), Number(q, "w"));
            // Root remains inactive until every original application port is bound.
            if (node.transform.parent && node.name != "3100") node.SetActive(Flag(go, "m_IsActive"));
        }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (Root) Root.SetActive(false);
            if (configured && Appearance) { Appearance.Clear(); Appearance.AppearanceCharacter.Release(true); }
            if (Controller) Controller.OnRuntimeAnimatorControllerChanged -= RememberController;
            if (Renderer) Renderer.Release();
            if (Animator && ownedControllers.Contains(Animator.runtimeAnimatorController)) Animator.runtimeAnimatorController = original;
            foreach (var value in ownedControllers) Destroy(value);
            ownedControllers.Clear();
            if (Root) Destroy(Root); Root = null;
        }
        private static void Destroy(Object value) { if (!value) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
        private static Vector3 Vector(Dictionary<string, object> value) => new Vector3(Number(value, "x"), Number(value, "y"), Number(value, "z"));
    }
}
