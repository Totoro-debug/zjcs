using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.PartnersUI;

public static class PartnerNativePortraitsValidation
{
    [Serializable] private sealed class Outcome { public int npc; public string animation, status, detail; public int vertices; }
    [Serializable] private sealed class Results { public Outcome[] outcomes; }

    public static void Run()
    {
        try { Check(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void Check()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_PARTNER_PORTRAIT_DIR") ?? Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var outcomes = new List<Outcome>();
        int loaded = 0;
        foreach (string path in Directory.GetFiles(Path.Combine(work, "native-portraits"), "portrait.bytes", SearchOption.AllDirectories).OrderBy(value => value))
        {
            int id = int.Parse(Path.GetFileName(Path.GetDirectoryName(path)));
            byte[] bytes = File.ReadAllBytes(path);
            var data = ProfessionNativeSkeletonData.LoadPartnerPortrait(bytes, id);
            var resource = OfficialPartnerPortraitAsset.Load(id);
            Require(resource.NpcId == id && resource.CreateSkeleton().SkeletonSha256 == data.SkeletonSha256 && resource.Pages.Length == 1,
                "actual lazy Resources package and original page binding");
            foreach (var page in resource.Pages)
                Require(resource.TextureForPage(page.Name) == page.Texture && page.Texture.mipmapCount == 1,
                    "actual native texture/page identity and original mip count");
            Require(data.Manifest == "884_156692" && data.Sex == "partner-portrait:" + id, "portrait identity");
            Reject(() => ProfessionNativeSkeletonData.LoadPartnerPortrait(bytes, id + 1));
            byte[] broken = (byte[])bytes.Clone(); broken[broken.Length - 16] ^= 1;
            Reject(() => ProfessionNativeSkeletonData.LoadPartnerPortrait(broken, id));
            loaded++;
            foreach (string name in data.AnimationNames)
            {
                var outcome = new Outcome { npc = id, animation = name };
                try
                {
                    var skeleton = new ProfessionNativeAppearanceSkeleton(data);
                    skeleton.SetSkin(data.SkinCatalog.FindSkin("default"));
                    var channels = new ProfessionNativeAnimationChannels(data, name);
                    using var batches = new ProfessionNativeMeshBatches(data.SlotCount);
                    foreach (float fraction in new[] { 0f, .17f, .51f, .93f, 1f })
                    {
                        PartnerNativePortraitPose.Evaluate(skeleton, channels, fraction * channels.Duration, false);
                        batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true, false);
                        foreach (var batch in batches.Batches)
                        {
                            Require(batch.Mesh.vertexCount > 0 && batch.Mesh.triangles.Length % 3 == 0, "original mesh topology");
                            foreach (Vector3 vertex in batch.Mesh.vertices)
                                Require(float.IsFinite(vertex.x) && float.IsFinite(vertex.y), "finite portrait geometry");
                        }
                        outcome.vertices = Math.Max(outcome.vertices, batches.Batches.Sum(batch => batch.Mesh.vertexCount));
                    }
                    Require(outcome.vertices > 0, "visible portrait geometry");
                    outcome.status = "sampled";
                }
                catch (NotSupportedException missing)
                { outcome.status = "requires-binding"; outcome.detail = missing.Message; }
                outcomes.Add(outcome);
                Debug.Log("PARTNER_PORTRAIT_SAMPLE " + JsonUtility.ToJson(outcome));
            }
        }
        Require(loaded == 54, "all 54 configured available portraits decoded");
        Require(outcomes.Where(value => value.npc == 8401).Count() == 2 &&
            outcomes.Where(value => value.npc == 8401).All(value => value.status == "sampled"), "original 8401 idle and click geometry");
        CheckExtrapolation();
        CheckOrderedConstraints();
        CheckSingleBoneIk();
        CheckAppliedPairIk();
        CheckPortraitNoRotation();
        CheckOriginalAlphaAndDeform(work);
        File.WriteAllText(Path.Combine(work, "portrait-native-sampling.json"), JsonUtility.ToJson(new Results { outcomes = outcomes.ToArray() }, true));
        Debug.Log("PARTNER_PORTRAIT_PACKAGES_PASSED: 54 identity/integrity-checked packages; original 8401 idle/click five samples and unclamped geometry. Other solver gaps are explicit in the sampling result; formal UI rendering remains pending.");
    }

    private static void CheckExtrapolation()
    {
        var pose = new ProfessionNativeBonePose(new[] {
            new ProfessionNativeBonePose.Bone { name = "root", parent = -1 },
            new ProfessionNativeBonePose.Bone { name = "source", parent = 0, x = 4, y = 6 },
            new ProfessionNativeBonePose.Bone { name = "target", parent = 0, x = 10, y = 20 } });
        var constraint = new ProfessionNativeIdleTransform.Constraint { Name = "negative original mix", Order = 0,
            Bones = new[] { 1 }, Target = 2, X = -1, Y = -.04f, OffsetScale = new Vector2(-5.960464477539063e-8f, 0) };
        Reject(() => ProfessionNativeIdleTransform.Apply(pose, new[] { constraint }));
        ProfessionNativeIdleTransform.ApplyUnclamped(pose, new[] { constraint });
        Require(Mathf.Abs(pose.World(1).m03 + 2) < .00001f && Mathf.Abs(pose.World(1).m13 - 5.44f) < .00001f,
            "negative weights extrapolate and inactive scale offsets do not alter translation");
        constraint.X = float.PositiveInfinity;
        Reject(() => ProfessionNativeIdleTransform.ApplyUnclamped(pose, new[] { constraint }));
        pose.Reset();
        var target = Matrix4x4.TRS(new Vector3(10, 20, 0), Quaternion.identity, new Vector3(-2, 3, 1));
        pose.OverrideWorld(2, target);
        constraint.X = constraint.Y = 0; constraint.Rotation = 1; constraint.OffsetRotation = 30;
        constraint.ScaleX = .5f; constraint.ScaleY = 1; constraint.OffsetScale = new Vector2(.5f, -.5f);
        ProfessionNativeIdleTransform.ApplyUnclamped(pose, new[] { constraint });
        var actual = pose.World(1);
        Require(Mathf.Abs(Mathf.Atan2(actual.m10, actual.m00) * Mathf.Rad2Deg - 150) < .0001f &&
            Mathf.Abs(new Vector2(actual.m00, actual.m10).magnitude - 1.75f) < .0001f &&
            Mathf.Abs(new Vector2(actual.m01, actual.m11).magnitude - 2.5f) < .0001f,
            "reflected target preserves rotation offset sign and nonzero scale offsets");
    }
    private static void CheckOrderedConstraints()
    {
        var pose = new ProfessionNativeBonePose(new[] {
            new ProfessionNativeBonePose.Bone { name = "root", parent = -1 },
            new ProfessionNativeBonePose.Bone { name = "solved parent", parent = 0 },
            new ProfessionNativeBonePose.Bone { name = "aiming child", parent = 1 },
            new ProfessionNativeBonePose.Bone { name = "target", parent = 0, x = 20, y = 50 },
            new ProfessionNativeBonePose.Bone { name = "unrelated solved branch", parent = 0 } });
        pose.OverrideWorld(1, Matrix4x4.TRS(new Vector3(20, 30, 0), Quaternion.Euler(0, 0, 30), Vector3.one));
        pose.OverrideWorld(4, Matrix4x4.Translate(new Vector3(50, 60, 0)));
        ProfessionNativeIdleIk.ApplyPreservingWorldConstraints(pose, new[] { new ProfessionNativeIdleIk.Constraint {
            name = "interleaved aim", order = 1, target = 3, bones = new[] { 2 }, mix = 1 } });
        var actual = pose.World(2);
        Require(Mathf.Abs(Mathf.Atan2(actual.m10, actual.m00) * Mathf.Rad2Deg - 90) < .0001f &&
            pose.World(1).m03 == 20 && pose.World(4).m03 == 50 && pose.HasWorldOverride(1) && pose.HasWorldOverride(4),
            "later IK retains earlier parent and unrelated constraint results");
    }
    private static void CheckSingleBoneIk()
    {
        var pose = new ProfessionNativeBonePose(new[] {
            new ProfessionNativeBonePose.Bone { name = "parent", parent = -1 },
            new ProfessionNativeBonePose.Bone { name = "aim", parent = 0, x = 2, y = 3, scaleX = 2, shearY = 20 },
            new ProfessionNativeBonePose.Bone { name = "target", parent = -1 },
            new ProfessionNativeBonePose.Bone { name = "other", parent = -1 } });
        var parent = Matrix4x4.TRS(new Vector3(10, 20, 0), Quaternion.Euler(0, 0, 30), new Vector3(-2, 3, 1));
        pose.OverrideWorld(0, parent);
        Vector3 desired = parent.MultiplyPoint3x4(new Vector3(2, 11, 0));
        pose.OverrideWorld(2, Matrix4x4.Translate(desired));
        pose.OverrideWorld(3, Matrix4x4.Translate(new Vector3(90, 80, 0)));
        var state = new ProfessionNativeConstraintAnimation.IkState {
            Name = "stretch", Bones = new[] { 1 }, Target = 2, Mix = 1, Stretch = true, Uniform = true };
        PartnerNativePortraitIk.ApplySingle(pose, state, 2);
        Vector3 endpoint = pose.World(1).MultiplyPoint3x4(new Vector3(2, 0, 0));
        var local = parent.inverse * pose.World(1);
        Require(Vector3.Distance(endpoint, desired) < .0001f &&
            Mathf.Abs(new Vector2(local.m01, local.m11).magnitude - 2) < .0001f &&
            pose.HasWorldOverride(0) && pose.World(3).m03 == 90,
            "single-bone stretch preserves shear, reflected nonuniform parent and unrelated constraints");
        // A second constraint consumes the already solved affine transform.
        desired = parent.MultiplyPoint3x4(new Vector3(2, 4, 0));
        pose.OverrideWorld(2, Matrix4x4.Translate(desired));
        state.Stretch = false; state.Compress = true; state.Uniform = false;
        PartnerNativePortraitIk.ApplySingle(pose, state, 2);
        local = parent.inverse * pose.World(1);
        Require(Vector3.Distance(pose.World(1).MultiplyPoint3x4(new Vector3(2, 0, 0)), desired) < .0001f &&
            Mathf.Abs(new Vector2(local.m01, local.m11).magnitude - 2) < .0001f,
            "single-bone compression consumes a prior result and preserves independent Y scale");
        pose.Reset();
        pose.OverrideWorld(2, Matrix4x4.Translate(new Vector3(2, 11, 0)));
        state.Compress = false; state.Stretch = true; state.Mix = .5f;
        PartnerNativePortraitIk.ApplySingle(pose, state, 2);
        var mixed = pose.World(1);
        Require(Mathf.Abs(Mathf.Atan2(mixed.m10, mixed.m00) * Mathf.Rad2Deg - 45) < .0001f &&
            Mathf.Abs(new Vector2(mixed.m00, mixed.m10).magnitude - 3) < .0001f,
            "single-bone partial mix interpolates angle and requested length");
        state.Mix = 0; var previous = pose.World(1);
        PartnerNativePortraitIk.ApplySingle(pose, state, 2);
        Require(pose.World(1) == previous, "zero IK mix preserves the prior result");
        state.Mix = 1.02596927f;
        PartnerNativePortraitIk.ApplySingle(pose, state, 2);
        Require(float.IsFinite(pose.World(1).m00), "original Bezier overshoot weight remains unclamped");
    }

    private static void CheckAppliedPairIk()
    {
        foreach (bool bend in new[] { true, false })
        {
            var pose = new ProfessionNativeBonePose(new[] {
                new ProfessionNativeBonePose.Bone { name = "parent", parent = -1 },
                new ProfessionNativeBonePose.Bone { name = "first", parent = 0 },
                new ProfessionNativeBonePose.Bone { name = "child", parent = 1, x = 3 },
                new ProfessionNativeBonePose.Bone { name = "target", parent = -1 },
                new ProfessionNativeBonePose.Bone { name = "other", parent = -1 } });
            var parent = Matrix4x4.TRS(new Vector3(20, 30, 0), Quaternion.Euler(0, 0, 20), new Vector3(-2, 3, 1));
            pose.OverrideWorld(0, parent);
            var first = Matrix4x4.TRS(new Vector3(2, 3, 0), Quaternion.Euler(0, 0, 30), new Vector3(2, 2, 1));
            var child = Matrix4x4.TRS(new Vector3(3, 0, 0), Quaternion.Euler(0, 0, 25), new Vector3(1.5f, 1, 1));
            pose.OverrideWorld(1, parent * first);
            pose.OverrideWorld(2, parent * first * child);
            Vector3 desired = parent.MultiplyPoint3x4(new Vector3(8, 11, 0));
            pose.OverrideWorld(3, Matrix4x4.Translate(desired));
            pose.OverrideWorld(4, Matrix4x4.Translate(new Vector3(90, 80, 0)));
            var state = new ProfessionNativeConstraintAnimation.IkState {
                Name = "applied pair", Bones = new[] { 1, 2 }, Target = 3, Mix = 1, ChildLength = 2, BendPositive = bend };
            PartnerNativePortraitIk.ApplyPair(pose, state);
            Vector3 endpoint = pose.World(2).MultiplyPoint3x4(new Vector3(2, 0, 0));
            Vector3 elbow = parent.inverse.MultiplyPoint3x4(pose.World(2).MultiplyPoint3x4(Vector3.zero)) - new Vector3(2, 3, 0);
            float cross = 6 * elbow.y - 8 * elbow.x;
            Require(Vector3.Distance(endpoint, desired) < .0001f && (bend ? cross < 0 : cross > 0) &&
                pose.HasWorldOverride(0) && pose.World(4).m03 == 90,
                "two-bone applied affine chain reaches the target with both bends and preserves unrelated constraints");
            var local = parent.inverse * pose.World(1);
            Require(Mathf.Abs(new Vector2(local.m00, local.m10).magnitude - 2) < .0001f,
                "two-bone IK preserves the existing uniform parent scale");
            state.Mix = 0; Matrix4x4 previous = pose.World(2);
            state.ChildLength = 0;
            PartnerNativePortraitIk.ApplyPair(pose, state);
            Require(pose.World(2) == previous, "zero two-bone IK mix preserves the solved chain");
            state.Mix = 1.02596927f; state.ChildLength = 2;
            PartnerNativePortraitIk.ApplyPair(pose, state);
            Require(float.IsFinite(pose.World(2).m00), "two-bone Bezier overshoot weight remains unclamped");
        }
    }

    private static void CheckPortraitNoRotation()
    {
        var bones = new[] {
            new ProfessionNativeBonePose.Bone { name = "parent", parent = -1 },
            new ProfessionNativeBonePose.Bone { name = "selective", parent = 0, inherit = 2, x = 1, y = 2 } };
        var matrix = Matrix4x4.TRS(new Vector3(10, 20, 0), Quaternion.Euler(0, 0, 30), new Vector3(-2, 3, 1));
        var restricted = new ProfessionNativeBonePose(bones); restricted.OverrideWorld(0, matrix);
        Reject(() => restricted.World(1));
        var pose = new ProfessionNativeBonePose(bones, true); pose.OverrideWorld(0, matrix);
        var result = pose.World(1); Vector3 expected = matrix.MultiplyPoint3x4(new Vector3(1, 2, 0));
        Require(Mathf.Abs(result.m00 - 2.25f) < .00001f && Mathf.Abs(result.m11 - 2.75f) < .00001f &&
            Mathf.Abs(result.m01 + Mathf.Sqrt(3) * .25f) < .00001f && Mathf.Abs(result.m10 - result.m01) < .00001f &&
            Mathf.Abs(result.m00 * result.m11 - result.m01 * result.m10 - 6) < .00001f &&
            Vector2.Distance(new Vector2(result.m03, result.m13), expected) < .00001f,
            "nonuniform no-rotation removes reflection, preserves positive stretches and retains parent-space translation");
        // Pure parent rotation has no effect on the child's own orientation.
        pose.OverrideWorld(0, Matrix4x4.Rotate(Quaternion.Euler(0, 0, 73)));
        result = pose.World(1);
        Require(Mathf.Abs(result.m00 - 1) < .00001f && Mathf.Abs(result.m11 - 1) < .00001f &&
            Mathf.Abs(result.m10) < .00001f && Mathf.Abs(result.m01) < .00001f,
            "no-rotation preserves the existing uniform branch");
    }

    private static void CheckOriginalAlphaAndDeform(string work)
    {
        var data = ProfessionNativeSkeletonData.LoadPartnerPortrait(File.ReadAllBytes(Path.Combine(work, "native-portraits/8453/portrait.bytes")), 8453);
        var skeleton = new ProfessionNativeAppearanceSkeleton(data); skeleton.SetSkin(data.SkinCatalog.FindSkin("default"));
        var animation = new ProfessionNativeAnimationChannels(data, "click_01");
        skeleton.EvaluateNativeAnimation(animation, 1.7666667699813843f, false);
        var slot = skeleton.CreateDrawSlots().Single(value => value.Index == data.FindSlot("Suimo_121"));
        Require(slot.Color.a == 91, "actual original alpha key survives color projection");
        data = ProfessionNativeSkeletonData.LoadPartnerPortrait(File.ReadAllBytes(Path.Combine(work, "native-portraits/10408/portrait.bytes")), 10408);
        var deform = new ProfessionNativeDeformAnimation(data, "click_01");
        var skin = (ProfessionNativeSkin)data.SkinCatalog.FindSkin("default");
        int index = data.FindSlot("npc_52"); var attachment = (ProfessionNativeAttachment)skin.GetAttachment(index, "npc_52");
        Require(deform.Sample(index, attachment, .5f) == null, "original sparse deform remains inactive before first key");
        var midpoint = deform.Sample(index, attachment, (.6666666865348816f + 1) * .5f);
        Require(Mathf.Abs(midpoint[8] - -1.8511962890625f * .5f) < .00002f, "original multiframe linear sparse deform interval");
        index = data.FindSlot("npc_39"); attachment = (ProfessionNativeAttachment)skin.GetAttachment(index, "npc_39");
        Require(Mathf.Abs(deform.Sample(index, attachment, 0)[34] + .005859375f) < .000001f &&
            Mathf.Abs(deform.Sample(index, attachment, 1)[30] + 1.0791015625f) < .000001f,
            "original Bezier deform sparse endpoints preserve distinct ranges");
        var middle = deform.Sample(index, attachment, .5f);
        Require(middle[30] < 0 && middle[30] > -1.0791015625f, "original Bezier deform interpolates between sparse frames");
    }
    private static void Reject(Action action)
    { try { action(); } catch (Exception) { return; } throw new InvalidOperationException("Invalid original package/constraint was accepted."); }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Native partner portrait: " + message); }
}
