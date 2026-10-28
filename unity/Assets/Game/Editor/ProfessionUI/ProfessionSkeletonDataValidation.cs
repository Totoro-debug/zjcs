using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionSkeletonDataValidation
    {
        [Serializable] private sealed class Fixture { public Body[] bodies; }
        [Serializable] private sealed class Body
        {
            public string sex, skeletonHash, atlasHash;
            public int bones, slots, skins, animations, regions, attachments, constraints;
            public Member[] members;
        }
        [Serializable] private sealed class Member { public string name; public int[] bones, constraints; }
        [Serializable] private sealed class Geometry { public GeometryBody[] cases; }
        [Serializable] private sealed class GeometryBody { public string sex; public ProfessionNativeBonePose.Bone[] bones; public Attachment[] attachments; }
        [Serializable] private sealed class Slots { public SlotBody[] cases; }
        [Serializable] private sealed class SlotBody { public string sex; public ProfessionNativeSlotPose.Slot[] slots; }
        [Serializable] private sealed class Bones { public BoneBody[] cases; }
        [Serializable] private sealed class BoneBody { public string sex; public ProfessionNativeBonePose.Bone[] bones; }
        [Serializable] private sealed class Attachment
        {
            public string skin, key, name, kind; public int slot, slotBone, end;
            public Vector2[] expected, expectedUvs; public int[] triangles;
        }
        private sealed class Slot : IProfessionNativeAttachmentSlot { public ProfessionNativeBonePose Pose { get; set; } public object Attachment { get; set; } }
        private const string Active = "Zjcs.ProfessionSkeletonData.Active";
        private static bool ready;
        static ProfessionSkeletonDataValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 300));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Native skeleton resource validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Lossless native skeleton resources passed actual Play Mode: both selected source/atlas byte hashes, all bone/slot/skin/constraint/animation inventories, every ordered skin membership and all five attachment types through independently evaluated source geometry and UVs. Unsupported source constraint/timeline fields remain present. Data loading is not full constraint evaluation, original actor rendering, native profession page or Windows Player validation.");
                Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("skeleton-data-validation.json")));
            var geometry = JsonUtility.FromJson<Geometry>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-attachments-validation.json")));
            var slotFixture = JsonUtility.FromJson<Slots>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("slot-pose-validation.json")));
            var boneFixture = JsonUtility.FromJson<Bones>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("bone-pose-validation.json")));
            int count = 0, points = 0;
            foreach (var row in fixture.bodies)
            {
                var data = ProfessionNativeSkeletonData.LoadResource(row.sex);
                Require(data.Manifest == "871_156598" && data.Sex == row.sex && data.SkeletonSha256 == row.skeletonHash && data.AtlasSha256 == row.atlasHash && data.DataScale == .01f, "lossless packaged source identity/bytes and original asset scale");
                Require(data.BoneCount == row.bones && data.SlotCount == row.slots && data.SkinCount == row.skins && data.AnimationCount == row.animations && data.AtlasRegionCount == row.regions && data.AttachmentCount == row.attachments && data.Constraints.Count == row.constraints, "complete current source inventories");
                Require(data.GetAnimation("home/idle_01") != null && data.GetAnimation("100/idle_01") != null && data.GetAnimation("200/idle_01") != null, "three source idle animation payloads");
                Require(data.SourceFields.ContainsKey("events") && data.SourceFields.ContainsKey("physics") && data.Constraints.Any(c => c.Kind == "physics" && c.Fields.ContainsKey("step")) && data.Constraints.Any(c => c.Kind == "transform"), "source events and constraint fields retained");
                var originalPose = data.CreateBonePose(); var originalSlots = data.CreateSlotPose(originalPose);
                Require(originalPose.Count == row.bones && originalSlots.Count == row.slots && data.FindSlot(originalSlots.Name(0)) == 0 && data.FindBone("does-not-exist") == -1, "native bone/slot factory and name indices");
                var setupBones = boneFixture.cases.Single(b => b.sex == row.sex).bones;
                for (int i = 0; i < setupBones.Length; i++)
                {
                    var b = setupBones[i];
                    Require(data.FindBone(b.name) == i && originalPose.Parent(i) == b.parent && originalPose.Inheritance(i) == b.inherit && originalPose.IsActive(i) == !b.skinRequired && originalPose.LocalPosition(i) == new Vector2(b.x, b.y) && originalPose.LocalScale(i) == new Vector2(b.scaleX, b.scaleY) && originalPose.LocalRotation(i) == b.rotation && originalPose.Shear(i) == new Vector2(b.shearX, b.shearY), "every original bone setup field");
                }
                var setupSlots = slotFixture.cases.Single(s => s.sex == row.sex).slots;
                for (int i = 0; i < setupSlots.Length; i++)
                {
                    var s = setupSlots[i];
                    // JsonUtility represents null strings from the reference
                    // fixture as empty strings; the runtime source parser keeps
                    // the original absent-attachment null.
                    string attachment = string.IsNullOrEmpty(s.attachment) ? null : s.attachment;
                    Require(originalSlots.Name(i) == s.name && originalSlots.Bone(i) == s.bone && originalSlots.Blend(i) == s.blend && originalSlots.Color(i).Equals(s.color) && originalSlots.HasDarkColor(i) == s.hasDarkColor && originalSlots.DarkColor(i).Equals(s.darkColor) && originalSlots.Attachment(i) == attachment, "slot " + row.sex + "/" + i + ": " + originalSlots.Name(i) + "/" + s.name + ", color " + originalSlots.Color(i) + "/" + s.color + ", dark " + originalSlots.DarkColor(i) + "/" + s.darkColor + ", attachment " + (originalSlots.Attachment(i) ?? "<null>") + "/" + (attachment ?? "<null>"));
                }
                foreach (var member in row.members)
                {
                    var skin = (ProfessionNativeSkin)data.SkinCatalog.FindSkin(member.name);
                    Require(skin != null && skin.Bones.SequenceEqual(member.bones) && skin.Constraints.SequenceEqual(member.constraints), "all ordered bone/constraint skin memberships: " + member.name);
                }
                var reference = geometry.cases.Single(c => c.sex == row.sex);
                var pose = new ProfessionNativeBonePose(reference.bones); pose.UpdateWorld();
                foreach (var attachment in reference.attachments)
                {
                    var skin = (ProfessionNativeSkin)data.SkinCatalog.FindSkin(attachment.skin);
                    var value = (ProfessionNativeAttachment)skin.GetAttachment(attachment.slot, attachment.key);
                    Require(value.Name == attachment.name && value.SlotBone == attachment.slotBone && ReferenceEquals(value, skin.GetAttachment(attachment.slot, attachment.key)), "original attachment name override, binding and shared identity");
                    if (value is ProfessionNativeRenderAttachment render)
                    {
                        Require(render.IsMesh == (attachment.kind == "mesh") && render.VertexCount == attachment.expected.Length, "real region/mesh vertex count");
                        var vertices = new Vector3[render.VertexCount]; render.ComputeVertices(pose, vertices);
                        for (int i = 0; i < vertices.Length; i++) { Near(attachment.expected[i], vertices[i]); Require((attachment.expectedUvs[i] - render.Uv(i)).sqrMagnitude <= 0.000000000009f, "independent original atlas UV"); points++; }
                        if (render.IsMesh) { Require(render.IndexCount == attachment.triangles.Length, "original triangle count"); for (int i = 0; i < render.IndexCount; i++) Require(render.Index(i) == attachment.triangles[i], "every original triangle index"); }
                    }
                    else if (value is ProfessionNativeClippingAttachment clip)
                    {
                        Require(clip.EndSlot == attachment.end && clip.VertexCount == attachment.expected.Length, "original clipping end slot/count"); var vertices = new Vector2[clip.VertexCount]; clip.ComputeVertices(pose, vertices);
                        for (int i = 0; i < vertices.Length; i++) { Near(attachment.expected[i], vertices[i]); points++; }
                    }
                    else if (value is ProfessionNativeBoundingAttachment bounds)
                    {
                        Require(bounds.WorldVerticesLength == attachment.expected.Length * 2, "original bounding vertex count"); var vertices = new float[bounds.WorldVerticesLength]; bounds.ComputeWorldVertices(new Slot { Pose = pose, Attachment = bounds }, vertices);
                        for (int i = 0; i < vertices.Length / 2; i++) { Near(attachment.expected[i], new Vector2(vertices[i * 2], vertices[i * 2 + 1])); points++; }
                    }
                    else if (value is ProfessionNativePointAttachment point) { Near(attachment.expected[0], point.ComputePosition(pose)); points++; }
                    else throw new InvalidOperationException("Unexpected native attachment binding.");
                    count++;
                }
                Require(reference.attachments.Length == data.AttachmentCount, "all packaged attachments checked");
                var bytes = Resources.Load<TextAsset>("ProfessionUI/NativeSkeletons/" + row.sex).bytes;
                var corrupted = (byte[])bytes.Clone(); corrupted[0] ^= 1; Reject(corrupted);
                var trailing = new byte[bytes.Length + 1]; System.Array.Copy(bytes, trailing, bytes.Length); Reject(trailing);
                using (var stream = new MemoryStream(bytes))
                using (var reader = new BinaryReader(stream))
                {
                    stream.Position = 12;
                    int length = reader.ReadInt32(); stream.Position += length;
                    length = reader.ReadInt32(); stream.Position += length + 4 + 4;
                    corrupted = (byte[])bytes.Clone(); corrupted[(int)stream.Position] ^= 1; Reject(corrupted);
                }
                Debug.Log("Native original data loaded: " + row.sex + ", " + data.BoneCount + " bones, " + data.AttachmentCount + " attachments, " + data.AnimationCount + " animations.");
            }
            Require(fixture.bodies.Length == 2 && count == fixture.bodies.Sum(b => b.attachments) && points > 10000, "both complete original attachment inventories and independent geometry");
        }
        private static void Reject(byte[] bytes)
        { bool rejected = false; try { ProfessionNativeSkeletonData.Load(bytes); } catch (InvalidDataException) { rejected = true; } Require(rejected, "changed format/hash/trailing data must be rejected"); }
        private static void Near(Vector2 expected, Vector2 actual)
        { Require((actual - expected).sqrMagnitude <= .00002f + .0000000002f * expected.sqrMagnitude, "independent original vertex/UV reference"); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Native skeleton data: " + message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
