using System;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Applies the recovered fight prefab hierarchy to a reused world character.</summary>
    internal sealed class OfficialBattleAttachedModelView : IDisposable
    {
        private const int PlayerEntityId = 3100;
        private const float PlayerBattleModelScale = 1.25f;
        private readonly Transform previousParent;
        private readonly Vector3 previousPosition, previousScale;
        private readonly Quaternion previousRotation;
        private readonly bool previousActive;
        private readonly Animator animator;
        private readonly Transform mapModelRoot;
        private readonly Vector3 previousMapModelPosition, previousMapModelScale;
        private readonly Quaternion previousMapModelRotation;
        private readonly AnimatorStateInfo[] previousStates;
        private bool disposed;

        public Transform Source { get; }
        public Transform Root { get; }
        public Transform Model { get; }

        public OfficialBattleAttachedModelView(Transform source, Transform parent, int entityId)
        {
            Source = source != null ? source : throw new ArgumentNullException(nameof(source));
            var binding = OfficialBattleSceneBindings.Find(entityId)?.model;
            if (binding?.transforms == null || binding.transforms.Length == 0)
                throw new InvalidOperationException("Official attached model hierarchy is missing: " + entityId);
            previousParent = source.parent;
            previousPosition = source.localPosition;
            previousRotation = source.localRotation;
            previousScale = source.localScale;
            previousActive = source.gameObject.activeSelf;
            animator = source.GetComponent<Animator>();
            if (animator == null) animator = source.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != source)
            {
                mapModelRoot = animator.transform;
                while (mapModelRoot.parent != source && mapModelRoot.parent != null)
                    mapModelRoot = mapModelRoot.parent;
                previousMapModelPosition = mapModelRoot.localPosition;
                previousMapModelRotation = mapModelRoot.localRotation;
                previousMapModelScale = mapModelRoot.localScale;
            }
            previousStates = new AnimatorStateInfo[animator == null ? 0 : animator.layerCount];
            for (int layer = 0; layer < previousStates.Length; layer++)
                previousStates[layer] = animator.GetCurrentAnimatorStateInfo(layer);
            Root = new GameObject("Battle attached unit " + entityId).transform;
            Root.SetParent(parent, false);
            Transform current = Root;
            foreach (var transform in binding.transforms)
            {
                var child = new GameObject(transform.name).transform;
                child.SetParent(current, false);
                child.localPosition = new Vector3(transform.position[0], transform.position[1], transform.position[2]);
                child.localRotation = new Quaternion(transform.rotation[0], transform.rotation[1],
                    transform.rotation[2], transform.rotation[3]);
                child.localScale = new Vector3(transform.scale[0], transform.scale[1], transform.scale[2]);
                current = child;
            }
            Model = current;
            source.SetParent(Model, false);
            source.localPosition = Vector3.zero;
            source.localRotation = Quaternion.identity;
            // The recovered battle hierarchy owns the model scale. Carrying the map-view
            // scale into it multiplies the official 0.3 model scale a second time. Map
            // wrappers also put that scale and offset on the direct character child.
            source.localScale = entityId == PlayerEntityId
                ? Vector3.one * PlayerBattleModelScale
                : Vector3.one;
            if (mapModelRoot != null)
            {
                mapModelRoot.localPosition = Vector3.zero;
                mapModelRoot.localRotation = Quaternion.identity;
                mapModelRoot.localScale = Vector3.one;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Source != null)
            {
                Source.SetParent(previousParent, false);
                Source.localPosition = previousPosition;
                Source.localRotation = previousRotation;
                Source.localScale = previousScale;
                if (mapModelRoot != null)
                {
                    mapModelRoot.localPosition = previousMapModelPosition;
                    mapModelRoot.localRotation = previousMapModelRotation;
                    mapModelRoot.localScale = previousMapModelScale;
                }
                Source.gameObject.SetActive(previousActive);
                if (animator != null)
                {
                    for (int layer = 0; layer < previousStates.Length; layer++)
                        if (animator.HasState(layer, previousStates[layer].fullPathHash))
                            animator.Play(previousStates[layer].fullPathHash, layer, previousStates[layer].normalizedTime);
                    animator.Update(0f);
                }
            }
            if (Root != null) OfficialBattleSceneBindings.DestroyCreatedObject(Root.gameObject);
        }

    }
}
