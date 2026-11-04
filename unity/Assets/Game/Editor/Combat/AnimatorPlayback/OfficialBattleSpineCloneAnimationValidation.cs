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
            }
            Debug.Log("Native clone component checks passed: both original bodies, all five skill_62 clips, exact clone renderer settings, pending/null-data requests, immediate native meshes, independent track/physics clocks and rebuild release. Production appearance binding, world movement and battle rendering are not covered.");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Original native clone: " + message);
        }
    }
}
