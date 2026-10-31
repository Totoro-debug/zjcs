using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        [Serializable] private sealed class DeformCases { public DeformCase[] cases; public AnimationCount[] counts; }
        [Serializable] private sealed class DeformCase
        {
            public string sex, animation, skin, slot, attachment, end;
            public float time;
            public float[] vertices, setup;
        }

        public static void RunDeforms()
        {
            Run();
            SessionState.SetBool(Key + ".deforms", true);
        }

        private static IEnumerator DeformSteps()
        {
            string folder = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = JsonUtility.FromJson<DeformCases>(File.ReadAllText(Path.Combine(folder, "deform-cases.json")));
            Require(fixture != null && fixture.cases.Length == 8, "eight original player deformation tracks");
            CheckWeightedDeformation();
            foreach (var body in fixture.counts)
            {
                var data = ProfessionNativeSkeletonData.LoadResource(body.sex);
                var names = (IReadOnlyDictionary<string, object>)data.SourceFields["animations"];
                Require(names.Count == body.animations, "original deformation animation inventory");
                Require(names.Keys.Sum(name => new ProfessionNativeDeformAnimation(data, name).TrackCount) == 4,
                    "all original animations bind four deform tracks per body");
                foreach (var row in fixture.cases.Where(row => row.sex == body.sex))
                {
                    int slot = data.FindSlot(row.slot);
                    var skin = (ProfessionNativeSkin)data.SkinCatalog.FindSkin(row.skin);
                    var clip = (ProfessionNativeClippingAttachment)skin.GetAttachment(slot, row.attachment);
                    var animation = new ProfessionNativeDeformAnimation(data, row.animation);
                    if (row.time > 0) Require(animation.Sample(slot, clip, row.time - .0001f) == null, "original delayed deform key");
                    var values = animation.Sample(slot, clip, row.time);
                    Require(values != null && values.SequenceEqual(row.vertices), "original deform influence offsets");
                    var other = new ProfessionNativeClippingAttachment(row.attachment, clip.SlotBone, data.BoneCount,
                        row.setup, row.setup.Length / 2, data.FindSlot(row.end), data.SlotCount, data.DataScale);
                    Require(animation.Sample(slot, other, row.time) == null, "same-name unrelated attachment does not inherit deformation");
                    var skeleton = new ProfessionNativeAppearanceSkeleton(data);
                    skeleton.SetSkin(skin);
                    var channels = new ProfessionNativeAnimationChannels(data, row.animation);
                    skeleton.EvaluateNativeAnimation(channels, row.time, false);
                    var drawn = skeleton.CreateDrawSlots().Single(value => value.Index == slot);
                    Require(ReferenceEquals(drawn.Attachment, clip) && drawn.Deform != null && drawn.Deform.SequenceEqual(row.vertices),
                        "original attachment channel reaches the draw slot");
                    var original = new Vector2[clip.VertexCount]; var changed = new Vector2[clip.VertexCount];
                    clip.ComputeVertices(skeleton.Pose, original); clip.ComputeVertices(skeleton.Pose, changed, drawn.Deform);
                    Matrix4x4 matrix = skeleton.Pose.World(clip.SlotBone);
                    for (int index = 0; index < clip.VertexCount; index++)
                    {
                        var delta = new Vector2(matrix.m00 * row.vertices[index * 2] + matrix.m01 * row.vertices[index * 2 + 1],
                            matrix.m10 * row.vertices[index * 2] + matrix.m11 * row.vertices[index * 2 + 1]) * data.DataScale;
                        Require(Vector2.Distance(changed[index], original[index] + delta) < .00002f, "source bone-matrix deformation geometry");
                    }
                    skeleton.SetToSetupPose();
                    Require(skeleton.CreateDrawSlots().All(value => value.Deform == null), "setup pose clears deformation");
                    skeleton.EvaluateNativeAnimation(channels, row.time, false);
                    skeleton.EvaluateNativeAnimation(new ProfessionNativeAnimationChannels(data, "100/idle_01"), 0, false);
                    Require(skeleton.CreateDrawSlots().All(value => value.Deform == null), "animation switch clears prior deformation");
                    if (row.animation == "100/skill_50")
                    {
                        var bodySkin = (ProfessionNativeSkin)data.SkinCatalog.FindSkin(body.sex == "male" ? "body_100_M_01" : "body_100_F_01");
                        var combined = skeleton.CreateSkin("isolated original clothing geometry fixture");
                        combined.AddSkin(skin); combined.AddSkin(bodySkin); skeleton.SetSkin(combined);
                        skeleton.EvaluateNativeAnimation(channels, row.time, false);
                        var draw = skeleton.CreateDrawSlots();
                        var unchanged = draw.Select(value => new ProfessionNativeMeshBatches.Slot(value.Index, value.Attachment,
                            value.Color, value.Blend, value.Active, value.BreakAfter)).ToArray();
                        using (var batches = new ProfessionNativeMeshBatches(data.SlotCount))
                        {
                            batches.Build(skeleton.Pose, unchanged);
                            var before = batches.Batches.SelectMany(value => value.Mesh.vertices).ToArray();
                            CaptureDeformGeometry(folder, body.sex + "-before", body.sex, batches);
                            batches.Build(skeleton.Pose, draw);
                            var after = batches.Batches.SelectMany(value => value.Mesh.vertices).ToArray();
                            Require(before.Length != after.Length || !before.SequenceEqual(after), "original clipping deformation changes native draw geometry");
                            CaptureDeformGeometry(folder, body.sex + "-deformed", body.sex, batches);
                        }
                    }
                    Debug.Log("[Original attachment deformation] " + body.sex + "/" + row.animation + ": original offsets, identity, clipping and reset verified.");
                }
                yield return null;
            }
        }

        private static void CheckWeightedDeformation()
        {
            var pose = new ProfessionNativeBonePose(new[] {
                new ProfessionNativeBonePose.Bone { name = "root", x = 10, y = 20, rotation = 30 },
                new ProfessionNativeBonePose.Bone { name = "child", parent = 0, x = 3, y = -2, rotation = -15 }
            });
            var vertices = new ProfessionNativeVertexAttachment(new[] { 2f, 0, 1, 2, .7f, 1, -3, 4, .2f }, 1, 0, 2);
            var deform = new[] { .5f, -1, 2, .75f };
            var actual = new Vector2[1]; vertices.Evaluate(pose, actual, deform);
            Vector3 first = pose.World(0).MultiplyPoint3x4(new Vector3(1.5f, 1, 0));
            Vector3 second = pose.World(1).MultiplyPoint3x4(new Vector3(-1, 4.75f, 0));
            Require(Vector2.Distance(actual[0], (Vector2)(first * .7f + second * .2f)) < .00001f,
                "source offsets are per influence and weights remain unnormalized");
            bool rejected = false;
            try { vertices.Evaluate(pose, actual, new[] { 1f }); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "mismatched deform layout rejected");
            rejected = false;
            try { vertices.Evaluate(pose, actual, new[] { 0f, float.NaN, 0, 0 }); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "nonfinite deformation rejected");
        }

        private static void CaptureDeformGeometry(string folder, string name, string sex, ProfessionNativeMeshBatches batches)
        {
            Require(batches.Batches.Count > 0, "original clothing geometry renders");
            var textures = Resources.Load<ProfessionNativeTextureCatalog>("ProfessionUI/character-textures");
            Require(textures != null, "packaged original atlas catalog");
            var root = new GameObject("Isolated original clipping geometry; pre-constraint pose");
            var materials = new List<Material>();
            Bounds bounds = batches.Batches[0].Mesh.bounds;
            foreach (var batch in batches.Batches)
            {
                bounds.Encapsulate(batch.Mesh.bounds);
                var item = new GameObject("Original atlas draw batch"); item.transform.SetParent(root.transform, false); item.layer = 29;
                item.AddComponent<MeshFilter>().sharedMesh = batch.Mesh;
                var renderer = item.AddComponent<MeshRenderer>(); renderer.sortingOrder = batch.FirstDrawIndex;
                var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = textures.Find(sex, batch.Page).Main };
                materials.Add(material); renderer.sharedMaterial = material;
            }
            var cameraObject = new GameObject("Isolated geometry camera"); cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.cullingMask = 1 << 29;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f;
            camera.transform.position = bounds.center + Vector3.back * 10; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.23f, .23f, .23f, 1);
            var target = new RenderTexture(512, 512, 24); camera.targetTexture = target;
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                var pixels = image.GetPixels32();
                Require(pixels.Any(pixel => !pixel.Equals(pixels[0])), "original clothing image contains rendered atlas pixels");
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; target.Release();
                UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
                foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
