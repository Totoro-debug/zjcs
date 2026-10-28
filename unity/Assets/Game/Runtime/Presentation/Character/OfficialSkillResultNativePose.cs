using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Presentation.Character
{
    // Original GameEndPanelDamageTest's two looping result animations. Reuses
    // the project's independent native bone, curve, IK and slot evaluators.
    public sealed class OfficialSkillResultNativePose : MonoBehaviour
    {
        private static readonly Dictionary<string, ProfessionNativeSkeletonData> Sources = new Dictionary<string, ProfessionNativeSkeletonData>();
        private ProfessionNativeSkeletonData data;
        private ProfessionNativeBonePose bones;
        private ProfessionNativeSlotPose slots;
        private ProfessionNativeBonePose.Track[] tracks;
        private ProfessionNativeSlotPose.Clip slotClip;
        private ProfessionNativeIdleIk.Constraint[] ik;
        private Dictionary<string, int> slotIndices;
        private int[] drawOrder;
        private float started, duration;
        private CharacterNativeAttachmentGeometry geometry;
        private Matrix4x4[] worldMatrices;
        private bool[] worldReady;
        private readonly Dictionary<(string slot, string attachment), string> attachmentSkins = new Dictionary<(string, string), string>();
        public string AnimationName { get; private set; }
        public float AnimationTime { get; private set; }

        public void Initialize(string sex, string animation, IEnumerable<string> appendSkins)
        {
            if (animation != "map/win_01" && animation != "map/injuried_idle")
                throw new NotSupportedException("Unverified original skill result animation: " + animation);
            if (!Sources.TryGetValue(sex, out data)) Sources.Add(sex, data = ProfessionNativeSkeletonData.LoadResource(sex));
            var original = data.GetAnimation(animation);
            if (original.Keys.Any(key => key != "bones" && key != "slots" && key != "ik" && key != "drawOrder"))
                throw new NotSupportedException("The original result animation requires an additional timeline evaluator.");
            bones = data.CreateBonePose();
            worldMatrices = new Matrix4x4[bones.Count]; worldReady = new bool[bones.Count];
            slots = data.CreateSlotPose(bones);
            tracks = data.CreateBoneTracks(animation);
            slotClip = data.CreateSlotClip(animation);
            slotIndices = Enumerable.Range(0, slots.Count).ToDictionary(slots.Name, value => value, StringComparer.Ordinal);
            drawOrder = new int[slots.Count];
            var extraSkins = new HashSet<string>(appendSkins ?? throw new ArgumentNullException(nameof(appendSkins)), StringComparer.Ordinal);
            extraSkins.Add("default"); // Original skeleton attachment fallback.
            geometry = GetComponent<CharacterNativeAttachmentGeometry>();
            if (geometry == null) throw new InvalidOperationException("Original result attachment geometry is unavailable.");
            var textures = Resources.Load<ProfessionNativeTextureCatalog>("ProfessionUI/character-textures");
            if (textures == null) throw new InvalidOperationException("Original result append texture catalog is unavailable.");
            geometry.EnsureOriginalAppendSkins(extraSkins, page => textures.Find(sex, page).Main, slot => slots.Color(slotIndices[slot]));
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.name.StartsWith("__skin__", StringComparison.Ordinal) && extraSkins.Contains(renderer.name.Substring(8)))
                    renderer.gameObject.SetActive(true);
            var activeSkins = GetComponentsInChildren<Renderer>(true)
                .Where(value => value.name.StartsWith("__skin__", StringComparison.Ordinal) && value.gameObject.activeSelf)
                .Select(value => value.name.Substring(8)).Where(value => value != "default")
                .Prepend("default").Distinct(StringComparer.Ordinal);
            var skinBones = new HashSet<int>();
            attachmentSkins.Clear();
            foreach (string name in activeSkins) {
                var skin = data.SkinCatalog.FindSkin(name) as ProfessionNativeSkin;
                if (skin == null) throw new InvalidOperationException("Original result skin is unavailable: " + name);
                foreach (int bone in skin.Bones) skinBones.Add(bone);
                foreach (var entry in skin.Entries) attachmentSkins[(slots.Name(entry.Slot), entry.Key)] = name;
            }
            bones.SetSkinBones(skinBones.ToArray());
            ik = BindConstantIk(original);
            duration = Duration(original);
            if (!(duration > 0)) throw new InvalidOperationException("The original result animation has no duration.");
            AnimationName = animation;
            started = Time.time;
            geometry = GetComponent<CharacterNativeAttachmentGeometry>();
            if (geometry == null) throw new InvalidOperationException("Original result attachment geometry is unavailable.");
            string[] expectedBones = ((List<object>)data.SourceFields["bones"]).Select(value => Convert.ToString(Object(value)["name"])).ToArray();
            if (!expectedBones.SequenceEqual(geometry.OriginalBoneNames))
                throw new InvalidOperationException("Original result skeleton and attachment bone order differ.");
            Apply(0);
            geometry.BindOriginalPose(BoneWorld, Visible, Order);
        }

        private void Update()
        {
            if (bones != null) Apply((Time.time - started) % duration);
        }

        public void Apply(float time)
        {
            AnimationTime = time;
            Array.Clear(worldReady, 0, worldReady.Length);
            bones.Apply(tracks, time);
            ProfessionNativeIdleIk.Apply(bones, ik);
            slots.Apply(slotClip, time);
            for (int i = 0; i < slots.Count; i++) drawOrder[slots.DrawSlot(i)] = i;
        }

        private Matrix4x4 BoneWorld(int bone)
        {
            // Existing attachment vertices use .01 world units per source unit;
            // preserve the original affine matrix without Transform decomposition.
            if (!worldReady[bone]) {
                worldMatrices[bone] = transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * .01f)
                    * bones.World(bone) * Matrix4x4.Scale(Vector3.one * 100f);
                worldReady[bone] = true;
            }
            return worldMatrices[bone];
        }
        private bool Visible(string skin, string slot, string attachment)
        {
            int index = slotIndices[slot];
            return bones.IsActive(slots.Bone(index)) && slots.Attachment(index) == attachment
                && attachmentSkins.TryGetValue((slot, attachment), out string selectedSkin) && selectedSkin == skin;
        }
        private int Order(string slot) => drawOrder[slotIndices[slot]];

        private ProfessionNativeIdleIk.Constraint[] BindConstantIk(IReadOnlyDictionary<string, object> animation)
        {
            var timeline = animation.TryGetValue("ik", out var raw) ? Object(raw) : new Dictionary<string, object>();
            var boneRows = (List<object>)data.SourceFields["bones"];
            var names = boneRows.Select((value, index) => new { name = Convert.ToString(Object(value)["name"]), index })
                .ToDictionary(value => value.name, value => value.index, StringComparer.Ordinal);
            var result = new List<ProfessionNativeIdleIk.Constraint>();
            foreach (var source in data.Constraints.Where(value => value.Kind == "ik")) {
                var row = source.Fields;
                var chain = ((List<object>)row["bones"]).Select(value => names[Convert.ToString(value)]).ToArray();
                float mix = Number(row, "mix", 1);
                bool bend = Boolean(row, "bendPositive", true);
                if (timeline.TryGetValue(source.Name, out var keysRaw)) {
                    var keys = (List<object>)keysRaw;
                    if (keys.Count != 1 || Number(Object(keys[0]), "time") != 0)
                        throw new NotSupportedException("The original result IK timeline is not constant: " + source.Name);
                    row = Object(keys[0]); mix = Number(row, "mix", 1); bend = Boolean(row, "bendPositive", true);
                }
                if (mix != 0 && (Number(row, "softness") != 0 || Boolean(row, "stretch") || Boolean(row, "compress")))
                    throw new NotSupportedException("The original result IK requires soft/stretch/compress geometry.");
                result.Add(new ProfessionNativeIdleIk.Constraint { name = source.Name,
                    order = (int)Number(source.Fields, "order"), target = names[Convert.ToString(source.Fields["target"])],
                    bones = chain, mix = mix, bendPositive = bend,
                    childLength = chain.Length == 2 ? Number(Object(boneRows[chain[1]]), "length") : 0 });
            }
            return result.OrderBy(value => value.order).ToArray();
        }

        private static Dictionary<string, object> Object(object raw) => CharacterModelPackage.AsObject(raw, "original result animation");
        private static float Number(IReadOnlyDictionary<string, object> row, string key, float fallback = 0)
            => row.TryGetValue(key, out var value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        private static bool Boolean(IReadOnlyDictionary<string, object> row, string key, bool fallback = false)
            => row.TryGetValue(key, out var value) ? Convert.ToBoolean(value, CultureInfo.InvariantCulture) : fallback;
        private static float Duration(object value)
        {
            if (value is IReadOnlyDictionary<string, object> row)
                return Math.Max(Number(row, "time"), row.Values.Select(Duration).DefaultIfEmpty(0).Max());
            if (value is List<object> list) return list.Select(Duration).DefaultIfEmpty(0).Max();
            return 0;
        }
    }
}
