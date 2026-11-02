using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneMecanimParts()
        {
            RunSceneAnimatorPose();
            SessionState.SetBool(Key + ".sceneMecanimParts", true);
        }

        // Observe and forward the actual official part interface; no replacement
        // art, animation names, part configuration or PlayAnimation implementation.
        private sealed class SceneObservedPart : IAppearanceSkeletonPart
        {
            internal readonly IAppearanceSkeletonPart Original;
            internal int Calls;
            internal string Last;
            internal SceneObservedPart(IAppearanceSkeletonPart original) { Original = original; }
            public GameObject GameObject => Original.GameObject;
            public GameObject SkeletonTarget => Original.SkeletonTarget;
            public bool HasAnim => Original.HasAnim;
            public IProfessionAppearancePartFollower SkeletonFollower => Original.SkeletonFollower;
            public void Init(GameObject root) => Original.Init(root);
            public void TryPlayAnim(string name) { Calls++; Last = name; Original.TryPlayAnim(name); }
        }

        private static void CheckSceneMecanimParts(PlayerAnimatorAppearance player)
        {
            var native = player.GetComponent<ProfessionNativeSceneRenderer>();
            var parts = player.GetComponent<AppearanceMecanimPartHandler>();
            Require(parts, "actual original Mecanim part handler, not the renderer-only handler");
            var animator = player.Animator; var controller = animator.runtimeAnimatorController;
            var original = parts.LoadedSkeletonDict[SkinType.pWeapon];
            var observed = new SceneObservedPart(original.Part);
            Require(observed.HasAnim, "original weapon animation dictionary is initialized");
            AnimationClip carrier = null; object firstIdentity = null;
            int notices = 0; object identity = null; float motionTime = -1, lastTime = -1;
            ProfessionPartClipApplied applied = (animation, name, layer, weight, time, previous, backward) =>
            {
                Require(layer == 0 && weight == 1 && !backward && animation is ProfessionNativeAnimationChannels channels
                    && channels.Name == name, "native animation identity and actual unmixed application parameters");
                notices++; identity = animation; motionTime = time; lastTime = previous;
            };
            native.OnClipApplied += applied;
            void Reset()
            {
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
                animator.runtimeAnimatorController = controller; animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0);
                native.UpdateAnimationAndMesh();
            }
            try
            {
                Reset();
                parts.LoadedSkeletonDict[SkinType.pWeapon] = new AppearanceSkeletonPartEntry(observed, original.PoolEntry);
                parts.SetSkins(player.AppearanceRuntimeData.SkinMap);
                native.UpdateAnimationAndMesh(); native.UpdateAnimationAndMesh();
                Require(observed.Calls == 0, "reapplying one native clip does not restart the original independent part");
                var idle = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
                var hit = controller.animationClips.Single(clip => clip.name == idle.name.Replace("idle_01", "hit_01"));
                var cue = new OfficialBattleSkillPlayableCue { kind = 1, origin_clip_name = "Skill", trigger_param = "skill", speed = 1 };
                using (var playback = new OfficialBattleAnimatorPlayback(animator, player.transform))
                {
                    playback.Play(cue, hit); animator.Update(.02f); animator.Update(.02f);
                    var first = native.ApplyAnimatorPose();
                    firstIdentity = first.Animation;
                    Require(observed.Calls == 1 && observed.Last == hit.name && ReferenceEquals(identity, first.Animation),
                        "original Mecanim handler forwards the actual native animation to the actual original part");
                    float previous = motionTime;
                    animator.Update(.1f); native.UpdateAnimationAndMesh();
                    Require(observed.Calls == 1 && motionTime > previous && lastTime == previous,
                        "repeated live sampling retains native identity and actual previous input time");
                }
                // A distinct Unity name-carrier still denotes the same native
                // animation, so the original identity guard must retain it.
                carrier = Object.Instantiate(hit); carrier.name = hit.name;
                using (var playback = new OfficialBattleAnimatorPlayback(animator, player.transform))
                {
                    playback.Play(cue, carrier); animator.Update(.02f); animator.Update(.02f);
                    var same = native.ApplyAnimatorPose();
                    Require(same.Clip == carrier && ReferenceEquals(firstIdentity, same.Animation) && observed.Calls == 1,
                        "distinct Unity carrier of the same native animation does not restart original part playback");
                }
                parts.enabled = false;
                Reset();
                Require(observed.Calls == 1, "disabled original handler unsubscribes while native body sampling continues");
                parts.enabled = true; native.UpdateAnimationAndMesh(); native.UpdateAnimationAndMesh();
                Require(observed.Calls == 2 && observed.Last == idle.name, "enabled original handler re-subscribes exactly once");
                int beforeTransition = notices;
                var matrices = Enumerable.Range(0, native.State.Pose.Count).Select(native.State.Pose.World).ToArray();
                animator.CrossFade("Skill", .5f, 0, 0); animator.Update(.01f);
                bool rejected = false;
                try { native.UpdateAnimationAndMesh(); } catch (NotSupportedException) { rejected = true; }
                Require(rejected && notices == beforeTransition
                    && matrices.SequenceEqual(Enumerable.Range(0, native.State.Pose.Count).Select(native.State.Pose.World)),
                    "unbound transition emits no application event and preserves the previous native pose");
                Reset();
                parts.LoadedSkeletonDict[SkinType.pWeapon] = original;
                parts.SetSkins(player.AppearanceRuntimeData.SkinMap);
                var weapon = original.Part.GameObject.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
                void Draw()
                {
                    native.UpdateAnimationAndMesh(); original.Part.GameObject.GetComponent<SkeletonRendererFollower>().ForceUpdate();
                    weapon.UpdateAnimationAndMesh();
                }
                string label = "mecanim-parts-" + native.State.Source.Sex + "-" + controller.name + (player.AnimController ? "-controller" : "-direct");
                Draw(); CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-visibility"); Draw();
                var bounds = SceneCharacterBounds(native, weapon);
                var pixels = CaptureSceneRendererCamera(bounds, label + "-idle");
                animator.Update(.4f); Draw();
                Require(!pixels.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-advanced")),
                    "bound animation-and-mesh entry changes actual original body and part camera pixels");
                Debug.Log("[Native Mecanim parts] " + label + ": original handler/part forwarding, native identity, duplicate carrier, disable/re-enable and rejected transition passed. Current Game clips have no source part-animation mapping; no mapping was invented.");
            }
            finally
            {
                parts.enabled = true;
                if (parts.LoadedSkeletonDict.TryGetValue(SkinType.pWeapon, out var entry) && ReferenceEquals(entry.Part, observed))
                { parts.LoadedSkeletonDict[SkinType.pWeapon] = original; parts.SetSkins(player.AppearanceRuntimeData.SkinMap); }
                Reset(); native.OnClipApplied -= applied;
                if (carrier) Object.DestroyImmediate(carrier);
            }
        }
    }
}
