using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Original prefab transforms, original Mecanim graph and original material
    // fields feed the existing independent geometry endpoint.
    public sealed class PartnerNativeBattleActor : IDisposable
    {
        private readonly List<PartnerNativeBattleMesh> parts = new List<PartnerNativeBattleMesh>();
        private readonly Dictionary<string, Transform> transforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly ProfessionNativeAnimatorPose pose;
        private PartnerNativeBattleAppearance appearance;
        private bool disposed;
        public Transform Root { get; }
        public Animator Animator { get; }
        public ProfessionNativeAppearanceSkeleton Skeleton => parts[0].Skeleton;
        public IReadOnlyList<MeshRenderer> Renderers => parts.Select(part => part.Mesh.MeshRenderer).ToArray();
        public Dictionary<string, object> OriginalRow { get; }
        public ProfessionNativeAnimatorPose.AppliedFrame LastFrame { get; private set; }
        public IReadOnlyDictionary<string, Transform> OriginalTransforms => transforms;
        public PartnerNativeBattleAttachments Attachments { get; private set; }

        public PartnerNativeBattleActor(PartnerNativeBattleAssetCatalog catalog, int npcId, Transform parent, string name)
        {
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            OriginalRow = catalog.Find(npcId) ?? throw new InvalidDataException("Original partner is unbound: " + npcId);
            Root = new GameObject(name).transform; Root.SetParent(parent, false);
            try
            {
                var appearanceBinding = PartnerNativeBattleAppearanceCatalog.Find(npcId)
                    ?? throw new InvalidDataException("Original partner appearance is unbound: " + npcId);
                var rows = new[] { OriginalRow }.Concat(Arr(OriginalRow["parts"]).Select(Obj)).ToArray();
                if (appearanceBinding.EntityId != Int(OriginalRow, "entityId") || appearanceBinding.Renderers.Length != rows.Length)
                    throw new InvalidDataException("Original partner appearance identity differs.");
                for (int index = 0; index < rows.Length; index++)
                {
                    var binding = appearanceBinding.Renderers[index];
                    string path = string.Join("/", Arr(rows[index]["transforms"]).Select(Obj).Skip(1).Select(row => (string)row["name"]));
                    if (binding.Path != path || binding.Materials.Length != 1 || !binding.Materials[0])
                        throw new InvalidDataException("Original partner appearance material path differs.");
                    parts.Add(CreatePart(catalog, rows[index], Root, transforms, binding.Materials[0]));
                }
                var translator = Obj(parts[0].Settings["translator"]);
                if (!Flag(translator, "autoReset") || !Flag(translator, "useCustomMixMode")
                    || Arr(translator["layerMixModes"]).Count != 1 || Convert.ToInt32(Arr(translator["layerMixModes"])[0]) != 1
                    || Arr(translator["layerBlendModes"]).Count != 1 || Convert.ToInt32(Arr(translator["layerBlendModes"])[0]) != 1)
                    throw new NotSupportedException("Bind the original partner translator mode before drawing.");
                Animator = parts[0].Mesh.gameObject.AddComponent<Animator>();
                Animator.runtimeAnimatorController = catalog.Controllers.Find(npcId);
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; Animator.applyRootMotion = false;
                Animator.enabled = false;
                pose = new ProfessionNativeAnimatorPose(Animator, ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics,
                    (skeleton, mix) => ProfessionNativeOrderedConstraintPose.EvaluateMixNextBeforePhysics(skeleton,
                        mix.Outgoing.Animation, mix.Outgoing.Time, mix.Outgoing.Loop,
                        mix.Incoming.Animation, mix.Incoming.Time, mix.Incoming.Loop, mix.Weight), allowPartnerBlendTrees: true);
                Animator.Rebind(); Animator.Update(0); ApplyPose();
                Attachments = new PartnerNativeBattleAttachments(this, transforms, parts.Select(part =>
                    new PartnerNativeBattleAttachments.RendererBinding(part.Mesh.transform, part.Skeleton,
                        part.Settings, part.Material)).ToArray());
                foreach (var part in parts)
                    part.BindAppearanceRenderer((PartnerNativeBattleRendererPort)Attachments.OriginalRenderers.Values.Single(value => value.transform == part.Mesh.transform));
                appearance = new PartnerNativeBattleAppearance(this, appearanceBinding);
                ApplyPose();
            }
            catch { Dispose(); throw; }
        }

        public void Advance(float deltaTime)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PartnerNativeBattleActor));
            if (!float.IsFinite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            appearance?.Tick();
            Animator.Update(deltaTime);
            foreach (PartnerNativeBattleMesh part in parts.Skip(1)) part.Elapsed += deltaTime * Number(part.Settings, "timeScale");
            ApplyPose();
        }

        public void ApplyPose()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PartnerNativeBattleActor));
            LastFrame = pose.Apply(Skeleton);
            foreach (PartnerNativeBattleMesh part in parts)
            {
                if (part.IndependentAnimation != null)
                    ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(part.Skeleton, part.IndependentAnimation,
                        part.Elapsed, Flag(part.Settings, "loop"));
                part.Draw();
            }
            Attachments?.UpdateFollowers();
        }

        private static PartnerNativeBattleMesh CreatePart(PartnerNativeBattleAssetCatalog catalog, Dictionary<string, object> row,
            Transform parent, Dictionary<string, Transform> transforms, Material originalMaterial)
        {
            foreach (var source in Arr(row["transforms"]).Select(Obj))
            {
                string key = (string)source["sourceKey"];
                if (!transforms.TryGetValue(key, out Transform node))
                {
                    node = new GameObject((string)source["name"]).transform; node.SetParent(parent, false);
                    node.gameObject.layer = Convert.ToInt32(source["layer"]); node.localPosition = Vector(source["position"]);
                    node.localScale = Vector(source["scale"]); var q = Obj(source["rotation"]);
                    node.localRotation = new Quaternion(Number(q, "x"), Number(q, "y"), Number(q, "z"), Number(q, "w"));
                    node.gameObject.SetActive(Flag(source, "active")); transforms.Add(key, node);
                }
                parent = node;
            }
            return new PartnerNativeBattleMesh(catalog, row, parent, originalMaterial);
        }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            appearance?.Dispose(); appearance = null;
            foreach (PartnerNativeBattleMesh part in parts) part.Dispose(); parts.Clear();
            if (Root) Destroy(Root.gameObject);
        }
        private static void Destroy(Object value) { if (!value) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
        private static Vector3 Vector(object value) { var v = Obj(value); return new Vector3(Number(v, "x"), Number(v, "y"), Number(v, "z")); }
    }
}
