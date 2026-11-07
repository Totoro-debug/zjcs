using System;
using UnityEngine;
using Zjcs.Presentation.Combat;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleSpineCloneAnimationValidation
    {
        public static void Validate()
        {
            var resources = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
            var settings = Resources.Load<TextAsset>("Combat/SkillClones/renderer-settings");
            Require(resources && settings, "Original native clone settings/resources are unavailable.");
            foreach (var model in resources.Models)
            {
                var root = new GameObject("Original independent clone animation check");
                try
                {
                    var renderer = root.AddComponent<ProfessionNativeSceneRenderer>();
                    renderer.ConfigureAnimation(resources, settings, "Player");
                    var animation = root.AddComponent<OfficialBattleSpineCloneAnimation>();
                    animation.Configure(renderer);
                    animation.PlayAnimation("100/skill_62_1", false);
                    Require(animation.AnimState == null, "Null data must not initialize an animation state.");
                    renderer.skeletonDataAsset = model.Asset;
                    animation.PlayAnimation("100/skill_62_1", false);
                    Require(animation.AnimState == null, "A pending request must wait for original skeleton initialization.");
                    renderer.Initialize(true, false);
                    Require(animation.AnimState.GetCurrent(0)?.Animation.Name == "100/skill_62_1"
                        && animation.AnimState.GetCurrent(1) == null, "Pending track-zero request was not restored.");
                    var state = renderer.State;
                    var skin = state.CreateSkin("original clone validation");
                    foreach (string name in new[] { "default", "body_100_" + (model.Sex == "male" ? "M" : "F") + "_01",
                        model.Sex == "male" ? "hair_G_01" : "hair_F_01" })
                        skin.AddSkin(state.Data.FindSkin(name));
                    state.SetSkin(skin); state.SetToSetupPose(); state.UpdateCache();
                    for (int index = 1; index <= 5; index++)
                    {
                        string name = "100/skill_62_" + index;
                        float physicalTime = animation.PhysicsTime;
                        animation.PlayAnimation(name, index % 2 == 0);
                        int visibleVertices = root.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                        Require(animation.AnimationTime == 0 && animation.PhysicsTime == physicalTime
                            && animation.AnimState.GetCurrent(0).Animation.Name == name,
                            "A clone replay must reset its own track, preserving the physics clock.");
                        for (int frame = 0; frame < 30; frame++)
                        {
                            animation.Tick(1f / 60);
                            renderer.LateUpdateMesh();
                            visibleVertices = Math.Max(visibleVertices, root.GetComponent<MeshFilter>().sharedMesh.vertexCount);
                        }
                        renderer.LateUpdateMesh();
                        Require(Mathf.Abs(animation.AnimationTime - .5f) < .0001f
                            && Mathf.Abs(animation.PhysicsTime - physicalTime - .5f) < .0001f,
                            "Independent clone animation/physics clocks did not advance.");
                        Require(visibleVertices > 0,
                            "The original body/skin animation produced no native mesh: " + model.Sex + "/" + name);
                    }
                    renderer.Initialize(true, false);
                    Require(animation.AnimState.GetCurrent(0) == null && animation.PhysicsTime == 0,
                        "A rebuilt clone retained the previous skeleton's track or physics history.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
                CheckHostMovement(resources, settings, model.Asset, model.Sex);
            }
            Debug.Log("Native clone component checks passed: both original bodies, all five skill_62 clips, exact clone renderer settings, pending/null-data requests, immediate native meshes, independent track/physics clocks, host translation/rotation/scale, zero delta, replay, disable/enable and rebuild release. Production appearance binding and battle rendering are covered separately.");
        }

        private static void CheckHostMovement(ProfessionNativeSceneAppearanceResources resources, TextAsset settings,
            ProfessionNativeSceneCharacterAsset asset, string sex)
        {
            for (int clip = 1; clip <= 5; clip++)
            {
                var roots = new GameObject[2];
                var renderers = new ProfessionNativeSceneRenderer[2];
                var animations = new OfficialBattleSpineCloneAnimation[2];
                try
                {
                    for (int index = 0; index < 2; index++)
                    {
                        roots[index] = new GameObject("Independent clone host movement " + index);
                        roots[index].transform.localScale = new Vector3(clip % 2 == 0 ? -.7f : .7f, .7f, 1);
                        var renderer = renderers[index] = roots[index].AddComponent<ProfessionNativeSceneRenderer>();
                        renderer.ConfigureAnimation(resources, settings, "Player");
                        animations[index] = roots[index].AddComponent<OfficialBattleSpineCloneAnimation>();
                        animations[index].Configure(renderer);
                        renderer.skeletonDataAsset = asset; renderer.Initialize(true, false);
                        var skin = renderer.State.CreateSkin("original moving clone validation");
                        foreach (string name in new[] { "default", "body_100_" + (sex == "male" ? "M" : "F") + "_01",
                            sex == "male" ? "hair_G_01" : "hair_F_01" }) skin.AddSkin(renderer.State.Data.FindSkin(name));
                        renderer.State.SetSkin(skin); renderer.State.SetToSetupPose(); renderer.State.UpdateCache();
                        animations[index].PlayAnimation("100/skill_62_" + clip, false);
                    }
                    Require(!ReferenceEquals(renderers[0].State, renderers[1].State), "Independent clones share a skeleton.");
                    float difference = 0;
                    for (int frame = 0; frame < 30; frame++)
                    {
                        var host = roots[1].transform;
                        host.SetPositionAndRotation(new Vector3(.02f * frame, .03f * Mathf.Sin(frame * .2f), 0),
                            Quaternion.Euler(0, 0, frame * 13f));
                        float delta = frame == 5 ? 0 : 1f / 60;
                        for (int index = 0; index < 2; index++)
                        {
                            float before = animations[index].PhysicsTime;
                            animations[index].Tick(delta);
                            Require(Mathf.Abs(animations[index].PhysicsTime - before - delta) < .00001f,
                                "Host movement or zero delta advanced the clone's physics clock twice.");
                            before = animations[index].PhysicsTime;
                            renderers[index].LateUpdateMesh(); renderers[index].LateUpdateMesh();
                            Require(animations[index].PhysicsTime == before, "A mesh redraw advanced clone physics.");
                            if (frame == 10)
                            {
                                animations[index].PlayAnimation("100/skill_62_" + clip, false);
                                Require(animations[index].PhysicsTime == before && animations[index].AnimationTime == 0,
                                    "A moving clone replay reset its independent physics clock.");
                            }
                        }
                        for (int bone = 0; bone < renderers[0].State.Source.BoneCount; bone++)
                        {
                            var still = renderers[0].State.Pose.World(bone);
                            var moving = renderers[1].State.Pose.World(bone);
                            float value = Mathf.Abs(still.m03 - moving.m03) + Mathf.Abs(still.m13 - moving.m13)
                                + Mathf.Abs(still.m00 - moving.m00) + Mathf.Abs(still.m10 - moving.m10);
                            Require(!float.IsNaN(value) && !float.IsInfinity(value), "Host motion produced a nonfinite clone pose.");
                            difference = Mathf.Max(difference, value);
                        }
                        if (frame == 20)
                        {
                            foreach (var root in roots) root.SetActive(false);
                            host.position += Vector3.right * .1f;
                            foreach (var root in roots) root.SetActive(true);
                        }
                    }
                    Require(difference > .00001f, "Original clone physics ignored host movement: " + sex + "/skill_62_" + clip);
                    renderers[1].Initialize(true, false);
                    Require(animations[1].PhysicsTime == 0 && animations[1].AnimState.GetCurrent(0) == null,
                        "A rebuilt moving clone retained old physical history.");
                    Debug.Log("Original clone host motion: " + sex + "/skill_62_" + clip + ", maximum pose difference=" + difference);
                }
                finally { foreach (var root in roots) if (root) UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Original native clone: " + message);
        }
    }
}
