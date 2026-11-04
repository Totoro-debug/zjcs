using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.PartnersUI;

// Exercises the actual original UI rigs and application animation sequence.
// Actor presentation and pointer interaction require the complete-window probe.
public static class PartnerDetailActorPoseValidation
{
    public static void Run()
    {
        Validate("8402-model", "npc_8402/idle_01", "npc_8402/idle_02", true);
        Validate("8402-live", "idle_01", "click_01", false);
        Debug.Log("PARTNER_DETAIL_NATIVE_POSE_OK");
        PartnerDetailWindowNativeValidation.Run();
    }
    static void Validate(string name, string idle, string touch, bool combine)
    {
        string folder = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "detail-actor-data", name);
        var raw = File.ReadAllBytes(Path.Combine(folder, "native.bytes"));
        var data = ProfessionNativeSkeletonData.LoadPartnerUi(raw, 8402);
        var invalid = (byte[])raw.Clone(); invalid[0] ^= 1;
        bool rejected = false;
        try { ProfessionNativeSkeletonData.LoadPartnerUi(invalid, 8402); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Reject a package with a different source identity");
        var asset = ScriptableObject.CreateInstance<PartnerNativeUiSkeletonAsset>();
        // Import the exact payload into this marked isolated project.
        string path = "Assets/Editor/" + name + "-native.bytes";
        File.WriteAllBytes(path, raw); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        Set(asset, "payload", AssetDatabase.LoadAssetAtPath<TextAsset>(path)); Set(asset, "npcId", 8402); Set(asset, "defaultMix", .2f);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ProfessionNativeUiGraphic));
        var graphic = go.GetComponent<ProfessionNativeUiGraphic>();
        using var pose = new PartnerNativeUiPose(asset, graphic, (page, blend) => null, page => null);
        var skeleton = pose.CreateSkeleton(combine ? "default" : "");
        if (combine) { skeleton.SetSkin(asset.Data.CreateCombinedNpcSkin(new[] { "body_8402" })); skeleton.SetToSetupPose(); skeleton.UpdateCache(); }
        Require(pose.GetOrLoadAnimation(idle) != null && pose.GetOrLoadAnimation(touch) != null,
            "Original lazy/full illustration animations are available");
        Require(pose.GetOrLoadAnimation("missing-source-animation") == null, "Missing animation preserves original no-play branch");
        pose.Play(skeleton, idle, true);
        for (int index = 0; index < 120; index++) pose.Apply(skeleton, 1f / 60);
        Require(Math.Abs(pose.PhysicsTime - 2f) < .00001f, "Physical time advances once per update");
        for (int index = 0; index < 10; index++) pose.Apply(skeleton, 0);
        Require(Math.Abs(pose.PhysicsTime - 2f) < .00001f, "Mesh refresh does not advance physical time");
        var sequence = go.AddComponent<SkeletonGraphicAnimSequence>();
        sequence.Configure(component => pose); sequence.SetSkeletonGraphic(graphic);
        string ended = null; sequence.OnAnimEnd = animation => ended = animation;
        sequence.PlaySequence(new System.Collections.Generic.List<AppearanceAnimData> {
            new AppearanceAnimData { AnimName = touch, LoopTimes = 1 },
            new AppearanceAnimData { AnimName = idle, LoopTimes = -1 } });
        float duration = pose.GetOrLoadAnimation(touch).Duration;
        for (int index = 0; index < Math.Ceiling((duration + .5f) * 60); index++) pose.Apply(skeleton, 1f / 60);
        Require(ended == touch && pose.GetCurrent(0).Animation.Name == idle && pose.GetCurrent(0).Loop,
            "Original application sequence completes touch once and returns to original idle");
        pose.Play(touch, false, .25f, true);
        for (int index = 0; index < Math.Ceiling((duration + .1f) * 60); index++) pose.Apply(skeleton, 1f / 60);
        Require(pose.GetCurrent(0).Animation.Name == idle, "Original illustration end-resume call returns to the prior clip");
        // Interrupt the resumed idle while its outgoing click is still mixing.
        pose.Play(touch, false, .25f, true); pose.Apply(skeleton, .01f);
        for (int bone = 0; bone < asset.Data.BoneCount; bone++)
        {
            if (!skeleton.Pose.IsActive(bone)) continue;
            var matrix = skeleton.Pose.World(bone);
            Require(float.IsFinite(matrix.m00) && float.IsFinite(matrix.m11) && float.IsFinite(matrix.m03) && float.IsFinite(matrix.m13),
                "Ordered original UI constraints retain finite world pose");
        }
        using (var meshes = new ProfessionNativeMeshBatches(skeleton.SlotCount))
        {
            meshes.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true, false);
            Require(meshes.Batches.Count > 0 && meshes.Batches.Sum(batch => batch.Mesh.vertexCount) > 0,
                "Original UI skins, clipping and deformation produce actual geometry");
        }
        sequence.SetSkeletonGraphic(null);
        UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(asset);
        Debug.Log("PARTNER_DETAIL_NATIVE_RIG_OK " + name);
    }
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
