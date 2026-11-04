using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using AttachmentFollower = Zjcs.Official.BattleSpinePlayables.AttachmentFollower;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Owns an original role shadow subtree, bound to its live native skeleton.</summary>
    public sealed class OfficialBattleShadowView : IDisposable
    {
        [Serializable] private sealed class Catalog { public string manifest; public Binding[] roles; }
        [Serializable] private sealed class Binding { public string prefab, name, parent, resource; public Ancestor[] ancestors; public RendererBinding[] renderers; }
        [Serializable] private sealed class Ancestor { public string name; public bool active; public float[] position, rotation, scale; }
        [Serializable] private sealed class RendererBinding { public string path; public int layerIndex, layerId, order; }
        private static Dictionary<string, Binding> bindings;
        private readonly GameObject instance;
        private readonly List<GameObject> createdAncestors = new List<GameObject>();
        private bool disposed;

        public Transform Root => instance.transform;

        public static OfficialBattleShadowView Create(int entityId, Transform entityRoot, Component skeletonRenderer)
        {
            if (entityRoot == null) throw new ArgumentNullException(nameof(entityRoot));
            if (skeletonRenderer == null) throw new ArgumentNullException(nameof(skeletonRenderer));
            if (!(skeletonRenderer is IProfessionFollowerRenderer))
                throw new ArgumentException("Bind the original role's native follower renderer.", nameof(skeletonRenderer));
            if (bindings == null)
            {
                TextAsset source = Resources.Load<TextAsset>("Combat/character-shadow-bindings");
                if (!source) throw new InvalidOperationException("Original role shadow bindings are missing.");
                Catalog catalog = JsonUtility.FromJson<Catalog>(source.text);
                if (catalog?.manifest != "884_156692" || catalog.roles == null)
                    throw new InvalidOperationException("Original role shadow source differs.");
                var next = new Dictionary<string, Binding>(StringComparer.Ordinal);
                foreach (Binding binding in catalog.roles) next.Add(binding.prefab, binding);
                bindings = next;
            }
            if (!bindings.TryGetValue(entityId.ToString(CultureInfo.InvariantCulture), out Binding original)) return null;
            GameObject prefab = Resources.Load<GameObject>(original.resource);
            if (!prefab) throw new InvalidOperationException("Original shadow prefab is missing: " + original.resource);
            return new OfficialBattleShadowView(prefab, entityRoot, skeletonRenderer, original);
        }

        private OfficialBattleShadowView(GameObject prefab, Transform parent, Component skeletonRenderer, Binding original)
        {
            try
            {
                // Older sampled model bindings omit the original view wrapper.
                // Restore its source transform for this owned subtree only.
                foreach (Ancestor ancestor in original.ancestors)
                {
                    Transform child = parent.Find(ancestor.name);
                    if (child == null)
                    {
                        child = new GameObject(ancestor.name).transform;
                        createdAncestors.Add(child.gameObject);
                        child.SetParent(parent, false);
                        child.localPosition = new Vector3(ancestor.position[0], ancestor.position[1], ancestor.position[2]);
                        child.localRotation = new Quaternion(ancestor.rotation[0], ancestor.rotation[1], ancestor.rotation[2], ancestor.rotation[3]);
                        child.localScale = new Vector3(ancestor.scale[0], ancestor.scale[1], ancestor.scale[2]);
                        child.gameObject.SetActive(ancestor.active);
                    }
                    parent = child;
                }
                instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                instance.name = original.name;
                var follower = instance.GetComponent<AttachmentFollower>();
                var boneFollower = instance.GetComponent<OfficialBattleShadowBoneFollower>();
                if (follower != null)
                {
                    if (((IProfessionFollowerRenderer)skeletonRenderer).Skeleton?.FindBone(follower.BoneName) == null)
                        throw new InvalidOperationException("The original shadow bone is not bound to the native role.");
                    follower.SetSkeletonRenderer(skeletonRenderer);
                    follower.ForceUpdate();
                }
                else if (boneFollower != null) boneFollower.Bind(skeletonRenderer);
                else throw new InvalidOperationException("The original shadow follower is missing.");
                // Existing battle renderers express the original layer/order in
                // the shared flattened scene range; keep the shadow in that range.
                foreach (RendererBinding binding in original.renderers)
                {
                    Transform target = string.IsNullOrEmpty(binding.path) ? instance.transform : instance.transform.Find(binding.path);
                    SpriteRenderer renderer = target == null ? null : target.GetComponent<SpriteRenderer>();
                    if (renderer == null || renderer.sortingLayerID != binding.layerId || renderer.sortingOrder != binding.order)
                        throw new InvalidOperationException("Original shadow renderer binding differs: " + binding.path);
                    renderer.sortingLayerID = 0;
                    renderer.sortingOrder = checked(binding.layerIndex * 1000 + binding.order);
                }
            }
            catch
            {
                Release();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Release();
        }

        private void Release()
        {
            OfficialBattleSceneBindings.DestroyCreatedObject(instance);
            for (int i = createdAncestors.Count - 1; i >= 0; i--)
                OfficialBattleSceneBindings.DestroyCreatedObject(createdAncestors[i]);
            createdAncestors.Clear();
        }
    }
}
