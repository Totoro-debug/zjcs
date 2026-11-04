using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Original application controller on its original entity node. The native
    // actor continues to consume Unity's Animator pose and original graph.
    public sealed class PartnerNativeBattleAnimationBinding : IDisposable
    {
        [Serializable] private sealed class PrivateSettings { public bool mResetAnimatorInitedOnDisable; }
        private readonly PartnerNativeBattleActor actor;
        private readonly RuntimeAnimatorController originalController;
        private bool disposed;
        public AnimationController Controller { get; private set; }
        public Dictionary<string, object> OriginalRow { get; }
        public GameObject Source => actor.Attachments.Agent.gameObject;

        public PartnerNativeBattleAnimationBinding(PartnerNativeBattleActor actor)
        {
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
            var asset = Resources.Load<TextAsset>("Combat/NativePartnerAssets/animation-bindings");
            if (!asset) throw new FileNotFoundException("Original partner application animation bindings are not packaged.");
            var root = Obj(OfficialEquipmentJson.Parse(asset.text));
            if (!Equals(root["manifest"], "884_156692")) throw new InvalidDataException("Original partner animation binding version differs.");
            OriginalRow = Arr(root["partners"]).Select(Obj).Single(row => Convert.ToInt32(row["npcId"]) == Convert.ToInt32(actor.OriginalRow["npcId"]));
            var fields = Obj(OriginalRow["fields"]);
            Transform source = actor.OriginalTransforms[(string)OriginalRow["rootKey"]];
            if (source != actor.Attachments.Agent.transform || actor.Animator.transform != actor.OriginalTransforms[(string)OriginalRow["animatorKey"]])
                throw new InvalidDataException("Original partner application controller hierarchy differs.");
            if (source.GetComponent<AnimationController>()) throw new InvalidOperationException("Original partner application controller is already bound.");
            originalController = actor.Animator.runtimeAnimatorController;
            bool active = actor.Root.gameObject.activeSelf;
            actor.Root.gameObject.SetActive(false);
            try
            {
                Controller = source.gameObject.AddComponent<AnimationController>();
                Controller.myAnimator = actor.Animator;
                Controller.InitOnAwake = Flag(fields, "InitOnAwake");
                Controller.CreateOverrideController = Flag(fields, "CreateOverrideController");
                Controller.NotCreateGraph = Flag(fields, "NotCreateGraph");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new PrivateSettings {
                    mResetAnimatorInitedOnDisable = Flag(fields, "mResetAnimatorInitedOnDisable") }), Controller);
                Controller.enabled = Flag(fields, "m_Enabled");
            }
            catch { Dispose(); throw; }
            finally { actor.Root.gameObject.SetActive(active); }
            if (Source.activeInHierarchy && Controller.InitOnAwake && !Controller.myInited)
            { Dispose(); throw new InvalidDataException("Original partner application controller failed its Awake initialization."); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!Controller) return;
            RuntimeAnimatorController owned = Controller.RuntimeAnimatorController;
            if (actor.Animator && actor.Animator.runtimeAnimatorController == owned)
                actor.Animator.runtimeAnimatorController = originalController;
            if (owned && owned != originalController) Destroy(owned);
            Controller.InitOnAwake = false;
            Destroy(Controller); Controller = null;
        }
        private static void Destroy(Object value) { if (!value) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
    }
}
