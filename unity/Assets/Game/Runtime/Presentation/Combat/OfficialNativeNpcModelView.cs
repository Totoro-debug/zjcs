using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Presentation.Combat
{
    // World followers and battle participants consume the same original NPC
    // skeletons. Keep atlas/material ownership and animation lifetime here.
    public sealed class OfficialNativeNpcModelView : IOfficialBattleNativeModel
    {
        [Serializable] private sealed class Catalog { public Binding[] renderers; public Model[] models; }
        [Serializable] private sealed class Binding
        { public int npcClassId, skeletonIndex; public string startingAnimation; public string[] combineSkins; }
        [Serializable] private sealed class Model { public string resource; }
        private static readonly Dictionary<string, ProfessionNativeSkeletonData> Sources = new Dictionary<string, ProfessionNativeSkeletonData>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        private readonly ProfessionNativeAppearanceSkeleton skeleton;
        private readonly ProfessionNativeSkin skin;
        private readonly ProfessionNativeMeshBatches batches;
        private readonly int classId, sortingLayer, sortingOrder;
        private ProfessionNativeAnimationChannels channels;
        private float elapsed;
        private bool loop = true, disposed;

        public Transform Transform { get; }
        public Transform ModelTransform => Transform;
        public bool IsAnimationFinished { get; private set; }

        public static bool HasBinding(int id) => id == 8402 || id == 8440 || id == 8441 || id == 8442;

        public OfficialNativeNpcModelView(int id, Transform parent, string name, int layer = 0, int order = 0)
        {
            if (!HasBinding(id)) throw new ArgumentOutOfRangeException(nameof(id), "Original NPC atlas binding is not prepared.");
            classId = id; sortingLayer = layer; sortingOrder = order;
            try
            {
                TextAsset catalogAsset = Resources.Load<TextAsset>("Map12/Native/TeamNpcBattle/catalog");
                if (!catalogAsset) throw new InvalidDataException("Original NPC skeleton catalog is missing.");
                Catalog catalog = JsonUtility.FromJson<Catalog>(catalogAsset.text);
                Binding binding = catalog.renderers.Single(value => value.npcClassId == id);
                string resource = catalog.models[binding.skeletonIndex].resource;
                if (!Sources.TryGetValue(resource, out ProfessionNativeSkeletonData data))
                {
                    TextAsset package = Resources.Load<TextAsset>(resource);
                    if (!package) throw new InvalidDataException("Original NPC skeleton package is missing: " + resource);
                    data = ProfessionNativeSkeletonData.LoadNpc(package.bytes);
                    Sources.Add(resource, data);
                }
                skeleton = new ProfessionNativeAppearanceSkeleton(data);
                skin = data.CreateCombinedNpcSkin(binding.combineSkins);
                skeleton.SetSkin(skin);
                skeleton.SetToSetupPose();
                batches = new ProfessionNativeMeshBatches(data.SlotCount);
                Transform = new GameObject(name).transform;
                Transform.SetParent(parent, false);
                // NpcModel child scale, preserved in the original world prefabs.
                Transform.localScale = Vector3.one * .3f;
                BindMaterials();
                Play(binding.startingAnimation, true, true);
            }
            catch { Dispose(); throw; }
        }

        private void BindMaterials()
        {
            NativeMap13AssetCatalog textures = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/npc-follower-texture-catalog");
            NativeMap13AssetCatalog originals = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/npc-follower-material-catalog");
            if (!textures || !originals) throw new InvalidDataException("Original NPC texture/material catalog is missing.");
            // Include attachments selected later by animation, not just the
            // attachments that happen to be visible in the setup pose.
            foreach (ProfessionNativeSkin.Entry slot in skin.Entries)
            {
                if (!(slot.Attachment is ProfessionNativeRenderAttachment attachment)) continue;
                int blend = skeleton.SlotPose.Blend(slot.Slot);
                string key = MaterialKey(attachment.Page, blend);
                if (materials.ContainsKey(key)) continue;
                if (blend != 0) throw new NotSupportedException("Bind the original NPC blend material: " + key);
                string page = Path.GetFileNameWithoutExtension(attachment.Page);
                bool weapon = page == "pWeapon_100_8440";
                string expected = "npc_" + classId;
                if (page != expected && !(classId == 8440 && weapon))
                    throw new InvalidDataException("Original NPC skin references an unexpected atlas page: " + page);
                string model = classId == 8440 ? "npc_big_model" : "npc_middle_model";
                string materialKey = classId == 8440
                    ? "CAB-35db32a9156215d66e80a363a190f94d:Material:" + (weapon ? "-8049600694219881985" : "-8287306839946497736")
                    : "CAB-b9b1514d5b015c219a6e35eb0745556e:Material:" + (classId == 8402 ? "1601366034732659983"
                        : classId == 8441 ? "-6569910388499614487" : "-8466807034191633858");
                Material original = originals.Load<Material>(materialKey);
                Texture2D texture = textures.Load<Texture2D>("Assets/AppearanceAssets/Npc/" + model + "/Atlas/High/" + page + ".png");
                var material = new Material(original) { name = "Official NPC " + classId + " " + page };
                material.mainTexture = texture;
                materials.Add(key, material);
            }
        }

        private static string MaterialKey(string page, int blend) => page + ":" + blend;
        private string Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (skeleton.Source.AnimationNames.Contains(name, StringComparer.Ordinal)) return name;
            string prefixed = "npc_" + classId + "/" + name;
            return skeleton.Source.AnimationNames.Contains(prefixed, StringComparer.Ordinal) ? prefixed : null;
        }
        public bool HasAnimation(string name) => Resolve(name) != null;
        public bool Play(string name, bool loopAnimation, bool restart = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialNativeNpcModelView));
            string resolved = Resolve(name);
            if (resolved == null) return false;
            if (!restart && channels?.Name == resolved && loop == loopAnimation) return true;
            channels = new ProfessionNativeAnimationChannels(skeleton.Source, resolved);
            loop = loopAnimation; elapsed = 0; IsAnimationFinished = false;
            Render();
            return true;
        }
        public void Tick(float deltaTime)
        {
            if (disposed) return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            elapsed += deltaTime;
            if (!loop && elapsed >= channels.Duration) { elapsed = channels.Duration; IsAnimationFinished = true; }
            Render();
        }
        private void Render()
        {
            skeleton.EvaluateNativeAnimation(channels, elapsed, loop);
            // The original constant idle constraints form a pre-physics stage. The
            // remaining skin physics must be verified before battle adoption.
            if (channels.Name.EndsWith("/idle_01", StringComparison.Ordinal))
            {
                float time = loop && channels.Duration > 0 ? elapsed % channels.Duration : elapsed;
                ProfessionNativeIdleIk.Apply(skeleton.Pose, skeleton.Source.CreateIdleIk(channels.Name, time));
                ProfessionNativeNpcIdlePath.Apply(skeleton);
                ProfessionNativeIdleTransform.Apply(skeleton.Pose,
                    ProfessionNativeIdleTransform.Bind(skeleton.Source, channels.Name, time, skeleton.SkinConstraints));
            }
            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true, true);
            int index = 0;
            foreach (ProfessionNativeMeshBatches.Batch batch in batches.Batches)
            {
                if (!materials.TryGetValue(MaterialKey(batch.Page, batch.Blend), out Material material))
                    throw new InvalidDataException("Unbound original NPC atlas page/blend: " + batch.Page + "/" + batch.Blend);
                if (index == renderers.Count)
                {
                    var child = new GameObject("NPC draw batch " + index, typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(Transform, false);
                    var renderer = child.GetComponent<MeshRenderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sortingLayerID = sortingLayer;
                    renderers.Add(renderer);
                }
                MeshRenderer current = renderers[index++];
                current.GetComponent<MeshFilter>().sharedMesh = batch.Mesh;
                current.sharedMaterial = material;
                current.sortingOrder = sortingOrder + batch.FirstDrawIndex;
                current.enabled = batch.Mesh.vertexCount > 0;
            }
            for (; index < renderers.Count; index++) renderers[index].enabled = false;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            batches?.Dispose();
            foreach (Material material in materials.Values) UnityEngine.Object.Destroy(material);
            materials.Clear();
            if (Transform) UnityEngine.Object.Destroy(Transform.gameObject);
        }
    }
}
