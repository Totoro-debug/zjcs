using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Presentation.Home
{
    [Serializable]
    public sealed class HomePlayerRuntimeData
    {
        public float[] position;
        public float[] scale;
        public float rotation_z;
        public string sorting_layer_name;
        public int sorting_layer;
        public int sorting_order;
        public string animation;
    }

    public sealed class HomePlayerActorView : IDisposable
    {
        private GameObject actor;
        private bool disposed;

        public Transform Transform => actor == null ? null : actor.transform;

        public HomePlayerActorView(HomeSceneView scene, string runtimePath, string slotId, string savesRoot)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            HomePlayerRuntimeData runtime = JsonUtility.FromJson<HomePlayerRuntimeData>(File.ReadAllText(runtimePath));
            if (runtime?.position == null || runtime.position.Length != 3
                || runtime.scale == null || runtime.scale.Length != 3
                || runtime.sorting_layer_name != "Mansion_Front01" || runtime.animation != "home/idle_01")
                throw new InvalidDataException("Official home player runtime data is missing or unsupported.");
            actor = CharacterCreationNativeModelFactory.CreateWorldModel(slotId, savesRoot, out _);
            try
            {
                actor.name = "HomePlayer";
                actor.transform.SetParent(scene.Root.transform, false);
                actor.transform.localPosition = new Vector3(runtime.position[0], runtime.position[1], runtime.position[2]);
                actor.transform.localScale = new Vector3(runtime.scale[0], runtime.scale[1], runtime.scale[2]);
                actor.transform.localRotation = Quaternion.Euler(0f, 0f, runtime.rotation_z);
                var sorting = actor.GetComponent<SortingGroup>();
                if (sorting == null) sorting = actor.AddComponent<SortingGroup>();
                sorting.sortingLayerName = "Default";
                sorting.sortingOrder = scene.GetNextSortingOrder(runtime.sorting_layer, runtime.sorting_order);
                Animator animator = actor.GetComponent<Animator>();
                if (!Zjcs.Presentation.Combat.OfficialBattleCharacterAnimationCatalog.TryResolve(
                    animator.runtimeAnimatorController, runtime.animation, out string state))
                    throw new InvalidDataException("Original home idle animation is unavailable.");
                animator.Play(Animator.StringToHash(state), 0, 0f);
                animator.Update(0f);
            }
            catch
            {
                if (actor != null) UnityEngine.Object.Destroy(actor);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (actor != null) UnityEngine.Object.Destroy(actor);
            actor = null;
        }
    }
}
