using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Native Map12 TeamInvite appearance. The skeleton, skin and animation
    // names come from the extracted official NPC packages; no Spine runtime is
    // required for the offline battle view.
    public sealed class OfficialMap12NpcBattleModelView : IOfficialBattleNativeModel
    {
        [Serializable] private sealed class Catalog { public RendererBinding[] renderers; public ModelBinding[] models; }
        [Serializable] private sealed class RendererBinding
        { public int npcClassId; public int skeletonIndex; public string startingAnimation; public string[] combineSkins; public float modelScale = .3f; }
        [Serializable] private sealed class ModelBinding { public string resource; }
        private readonly ProfessionNativeAppearanceSkeleton skeleton;
        private readonly ProfessionNativeMeshBatches batches;
        private ProfessionNativeAnimationChannels channels;
        private readonly RendererBinding binding;
        private readonly int classId;
        private readonly List<Material> materials = new List<Material>();
        private float elapsed;
        private string animation;
        private bool loop;
        private bool disposed;

        public Transform Transform { get; }
        public Transform ModelTransform => Transform;
        public bool IsAnimationFinished { get; private set; }

        public OfficialMap12NpcBattleModelView(int npcClassId, Transform parent, string name)
        {
            classId = npcClassId;
            TextAsset catalogAsset = Resources.Load<TextAsset>("Map12/Native/TeamNpcBattle/catalog");
            if (catalogAsset == null) throw new InvalidDataException("Map12 TeamInvite NPC catalog is missing.");
            Catalog catalog = JsonUtility.FromJson<Catalog>(catalogAsset.text);
            binding = catalog?.renderers?.SingleOrDefault(value => value.npcClassId == npcClassId);
            if (binding == null || binding.skeletonIndex < 0 || binding.combineSkins == null)
                throw new InvalidDataException("Map12 TeamInvite NPC binding is missing: " + npcClassId);
            ModelBinding[] models = catalog?.models ?? Array.Empty<ModelBinding>();
            if (binding.skeletonIndex >= models.Length) throw new InvalidDataException("Map12 NPC skeleton index is invalid: " + npcClassId);
            TextAsset package = Resources.Load<TextAsset>(models[binding.skeletonIndex].resource);
            if (package == null) throw new InvalidDataException("Map12 NPC skeleton package is missing: " + npcClassId);
            ProfessionNativeSkeletonData data = ProfessionNativeSkeletonData.LoadNpc(package.bytes);
            skeleton = new ProfessionNativeAppearanceSkeleton(data);
            skeleton.SetSkin(data.CreateCombinedNpcSkin(binding.combineSkins));
            skeleton.UpdateCache();
            skeleton.SetToSetupPose();
            batches = new ProfessionNativeMeshBatches(data.SlotCount);
            Transform = new GameObject(name).transform;
            Transform.SetParent(parent, false);
            Transform.localScale = Vector3.one * (binding.modelScale > 0 ? binding.modelScale : .3f);
            animation = binding.startingAnimation;
            channels = new ProfessionNativeAnimationChannels(data, animation);
            BuildMaterials(data);
            SampleAndRender();
        }

        public static bool HasBinding(int npcClassId)
        {
            TextAsset asset = Resources.Load<TextAsset>("Map12/Native/TeamNpcBattle/catalog");
            if (asset == null) return false;
            Catalog catalog = JsonUtility.FromJson<Catalog>(asset.text);
            return catalog?.renderers?.Any(row => row != null && row.npcClassId == npcClassId) == true;
        }

        private void BuildMaterials(ProfessionNativeSkeletonData data)
        {
            Shader shader = Shader.Find("Sprites/Default");
            foreach (ProfessionNativeMeshBatches.Slot slot in skeleton.CreateDrawSlots())
            {
                if (slot.Attachment is not ProfessionNativeRenderAttachment render) continue;
                var material = new Material(shader) { name = "Map12 NPC " + classId + " " + render.Page };
                material.mainTexture = Texture2D.whiteTexture;
                materials.Add(material);
            }
        }

        public bool HasAnimation(string name) => ResolveAnimation(name) != null;
        public bool Play(string name, bool loopAnimation, bool restart = false)
        {
            string resolved = ResolveAnimation(name);
            if (resolved == null) return false;
            if (!restart && animation == resolved && loop == loopAnimation) return true;
            animation = resolved; loop = loopAnimation; elapsed = 0f; IsAnimationFinished = false;
            ProfessionNativeAnimationChannels next = new ProfessionNativeAnimationChannels(skeleton.Source, animation);
            channels = next;
            SampleAndRender(next);
            return true;
        }
        public void Tick(float deltaTime)
        {
            if (disposed) return;
            elapsed += Mathf.Max(0f, deltaTime);
            float duration = channels.Duration;
            if (!loop && duration > 0f && elapsed >= duration) { elapsed = duration; IsAnimationFinished = true; }
            SampleAndRender();
        }
        private string ResolveAnimation(string requested)
        {
            if (string.IsNullOrEmpty(requested)) return null;
            if (skeleton.Source.AnimationNames.Contains(requested, StringComparer.Ordinal)) return requested;
            string prefix = "npc_" + classId + "/" + requested;
            return skeleton.Source.AnimationNames.Contains(prefix, StringComparer.Ordinal) ? prefix : null;
        }
        private void SampleAndRender(ProfessionNativeAnimationChannels active = null)
        {
            if (active != null) { animation = active.Name; }
            ProfessionNativeAnimationChannels current = active ?? channels;
            skeleton.EvaluateNativeAnimation(current, elapsed);
            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true);
            int index = 0;
            foreach (ProfessionNativeMeshBatches.Batch batch in batches.Batches)
            {
                while (Transform.childCount <= index)
                {
                    var child = new GameObject("NPC mesh " + Transform.childCount, typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(Transform, false);
                    child.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                MeshRenderer renderer = Transform.GetChild(index).GetComponent<MeshRenderer>();
                Transform.GetChild(index).gameObject.SetActive(true);
                Transform.GetChild(index).GetComponent<MeshFilter>().sharedMesh = batch.Mesh;
                renderer.sharedMaterial = materials.Count == 0 ? null : materials[Mathf.Min(index, materials.Count - 1)];
                renderer.sortingOrder = batch.FirstDrawIndex;
                renderer.enabled = batch.Mesh.vertexCount > 0;
                index++;
            }
            for (; index < Transform.childCount; index++) Transform.GetChild(index).gameObject.SetActive(false);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; batches.Dispose();
            foreach (Material material in materials) UnityEngine.Object.Destroy(material);
            if (Transform != null) UnityEngine.Object.Destroy(Transform.gameObject);
        }
    }
}
