using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAnimation;
using Zjcs.Presentation.Combat;
using ScenePlayer = Zjcs.Official.CombatAppearance.PlayerAppearance;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunScenePlayerAnimator()
        {
            Run(); SessionState.SetBool(Key + ".scenePlayerAnimator", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator ScenePlayerAnimatorSteps()
        {
            var catalog = Resources.Load<ProfessionNativeSceneAnimatorCatalog>("Combat/SceneAppearance/player-animators");
            Require(catalog && catalog.Entries.Count == 4 && catalog.Entries.All(entry => entry.Controller), "four bound current original Game controllers");
            int selections = 0, refreshes = 0, instances = 0;
            foreach (bool useController in new[] { false, true })
            {
                // Exercise the same explicit load seam through two actual Unity
                // asset-loading paths, without substituting fake controllers.
                RuntimeAnimatorController Load(string address)
                {
                    if (!useController) return catalog.Load(address);
                    var entry = catalog.Entries.SingleOrDefault(row => row.Address == address);
                    return entry == null ? null : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GetAssetPath(entry.Controller));
                }
                ScenePlayer Create(GameObject root, OfflineProfessionAppearanceBindings graph)
                {
                    instances++;
                    var animator = root.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    if (SessionState.GetBool(Key + ".sceneMecanimParts", false))
                        root.GetComponent<ProfessionNativeSceneRenderer>().BindAnimatorPose(
                            new ProfessionNativeAnimatorPose(animator, ProfessionNativeConstraintPose.EvaluateBeforePhysics));
                    var player = root.AddComponent<PlayerAnimatorAppearance>();
                    player.ConfigurePlayerAppearance(graph); player.Animator = animator; player.PlayerPhase = PlayerPhase.Game;
                    player.ConfigureAnimatorResources(catalog.Contains, Load);
                    if (useController)
                    {
                        var controller = root.AddComponent<AnimationController>(); controller.NotCreateGraph = true;
                        player.AnimController = controller;
                        player.ReapplyOwnAnimator();
                        Require(controller.myAnimator == animator && !controller.myInited, "original pre-init reapply binds Animator without fabricating a controller");
                    }
                    bool selected = false;
                    string previousSex = null;
                    player.OnAnimatorChanged += () =>
                    {
                        string address = AppearanceAddressUtils.GetPlayerAnimatorPath(graph.Sex, graph.Profession, PlayerPhase.Game, graph, catalog.Contains);
                        Require(animator.runtimeAnimatorController == catalog.Load(address), "original current sex/profession controller installed before appearance refresh");
                        if (previousSex != null)
                            Require(root.GetComponent<ProfessionNativeSceneRenderer>().State.Source.Sex == previousSex,
                                "controller-change callback precedes the actual body skeleton replacement");
                        selected = true; selections++;
                    };
                    player.OnAppearanceRefreshed += () =>
                    {
                        Require(selected && root.GetComponent<ProfessionNativeSceneRenderer>().State.Source.Sex == (graph.Sex == Sex.Male ? "male" : "female"),
                            "original controller selection precedes completed actual player appearance refresh");
                        selected = false; previousSex = graph.Sex == Sex.Male ? "male" : "female"; refreshes++;
                    };
                    return player;
                }
                void AfterInit(ScenePlayer entry)
                {
                    var player = (PlayerAnimatorAppearance)entry;
                    var animator = player.Animator;
                    string group = animator.runtimeAnimatorController.name.Contains("Fashi") ? "200" : "100";
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && animator.GetCurrentAnimatorClipInfo(0).Single().clip.name == group + "/idle_01",
                        "original refresh Update(0) enters exact current default Game motion");
                    var selectedController = animator.runtimeAnimatorController;
                    // Reuse the exact selected controller's own Damage motion as
                    // the skill override; this proves graph/parameter routing,
                    // not skeleton skill rendering or an alternative skill rule.
                    var hit = selectedController.animationClips.Single(clip => clip.name == group + "/hit_01");
                    var cue = new OfficialBattleSkillPlayableCue { kind = 1, origin_clip_name = "Skill", trigger_param = "skill",
                        my_duration = 0, end_resume_param = false, speed = 1 };
                    using (var playback = new OfficialBattleAnimatorPlayback(animator, entry.transform))
                    {
                        Require(OfficialBattleAnimatorPlayback.Supports(animator, cue), "selected current player controller supports original skill playable");
                        playback.Play(cue, hit); animator.Update(.02f); animator.Update(.02f);
                        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Skill") && animator.GetCurrentAnimatorClipInfo(0).Single().clip == hit,
                            "actual selected player graph consumes original playable override and trigger");
                    }
                    Require(animator.runtimeAnimatorController == selectedController, "temporary original playable restores the selected player controller");
                    animator.Rebind(); animator.Update(0);
                    if (player.AnimController)
                    {
                        player.AnimController.myAnimator = null; player.ReapplyOwnAnimator();
                        Require(player.AnimController.myAnimator == animator && player.AnimController.myInited
                            && player.AnimController.RuntimeAnimatorController == selectedController,
                            "initialized reapply restores the owned Animator and retains the same source controller");
                    }
                    else player.ReapplyOwnAnimator();
                    if (SessionState.GetBool(Key + ".sceneAnimatorPose", false)) CheckSceneAnimatorPose(player);
                    if (SessionState.GetBool(Key + ".sceneMecanimParts", false)) CheckSceneMecanimParts(player);
                    if (SessionState.GetBool(Key + ".sceneMixNext", false)) CheckSceneMixNext(player);
                    Debug.Log("[Current player Animator] " + selectedController.name + ": direct=" + !useController
                        + "; original idle/skill playable, controller restoration and reapply passed. Native body poses remain explicit pre-physics diagnostics.");
                }
                var steps = ScenePlayerAppearanceSteps(Create, AfterInit, useController ? "-controller" : "-direct");
                while (steps.MoveNext()) yield return steps.Current;
            }
            Require(instances == 8 && refreshes == 80 && selections == 84, "all selected current player refresh, unchanged-controller and reapply notifications");
            Debug.Log("[Current player Animator lifecycle] Eight actual native characters, four source controller identities, 80 appearance refreshes and 84 controller notifications passed.");
        }
    }
}
