using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        private static IEnumerator CheckPlayerNativeViews(AppearanceConfig config, ProfessionAppearanceResolver resolver)
        {
            using var resources = new OfflineProfessionBattlePlayerResources();
            foreach (Sex sex in new[] { Sex.Male, Sex.Female })
            foreach (int profession in new[] { 1, 10 })
            {
                var profile = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
                profile.Wardrobe.Sex = (int)sex; profile.Wardrobe.Profession = profession;
                foreach (var pair in resolver.BuildDefaultAppearanceData(config, sex, (Profession)profession).SkinMap)
                    profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                string saved = JsonUtility.ToJson(profile), label = sex.ToString().ToLowerInvariant() + "-" + profession + "-native-view";
                var state = new FightBattleUnitState { Id = 1, CharacterType = 1, RoleEntityId = 3100, Hp = 100, MaxHp = 100 };
                var host = new GameObject("Original native player view lifecycle"); host.transform.position = new Vector3(2, -1, .4f);
                using var scope = new OfflineBattleNativePlayableScope(host.transform, null);
                scope.BindNativePrefabAdapter(PartnerNativeSkillSkeleton.BindOriginalPrefab);
                OfficialBattlePlayerNativeModelView view = null;
                var cameraObject = new GameObject("Native player view visibility camera");
                var target = new RenderTexture(384, 512, 24);
                try
                {
                    view = Create();
                    var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
                    camera.aspect = .75f; camera.orthographicSize = 2;
                    camera.transform.position = host.transform.position + new Vector3(0, 1, -100); camera.targetTexture = target;
                    for (int frame = 0; frame < 12; frame++) { Tick(); yield return null; }
                    Require(view.Transform == view.Actor.Root.transform && view.ModelTransform == view.Actor.Animator.transform,
                        "native battle view exposes original entity and nested Animator transforms");
                    var source = view.Transform.Find("3100_view");
                    Require(source.Cast<Transform>().Count(t => t.name == "ShadowBoneFollower") == 1,
                        "original attachment placeholder is replaced by one complete shadow");
                    var shadow = source.Find("ShadowBoneFollower");
                    Require(shadow.GetComponentsInChildren<SpriteRenderer>().Length == 1
                        && view.Actor.Attachments.Transforms.Values.Contains(shadow), "source attachment identities resolve the actual rendered shadow");
                    var group = view.Transform.Find("3100_view/body").GetComponent<SortingGroup>();
                    Require(group && group.sortingLayerID == 0 && group.sortingOrder == 18000
                        && shadow.GetComponentInChildren<SpriteRenderer>().sortingOrder == 17990,
                        "current original body/effect group and external shadow retain their battle ordering");
                    CaptureView("idle");
                    float physicalTime = view.Actor.Renderer.PlayerPhysicsTime;
                    view.Tick(.1f); view.Tick(.1f);
                    Require(view.Actor.Renderer.PlayerPhysicsTime == physicalTime, "native view tick never advances actor physics");
                    var move = new FightBattleMovementEvent { UnitId = 1, MoveSkillEntityId = 15300,
                        Path = new List<FightGridPoint> { new FightGridPoint(0, 0), new FightGridPoint(1, 0) } };
                    var step = new FightBattleStepEvent { Movements = new List<FightBattleMovementEvent> { move } };
                    var cue = new OfficialBattleMovementAnimationCatalog().Build(step, p => new Vector3(p.X, p.Y))[move][0];
                    Require(view.Supports(cue.SkillPlayable), "actual original player controller supports the extracted 15300 movement cue");
                    view.PlayMovement(cue.SkillPlayable, -1);
                    bool ran = false;
                    for (int frame = 0; frame < 30; frame++)
                    {
                        Tick(); yield return null;
                        if (view.Animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name.Contains("run")))
                        { ran = true; CaptureView("run"); break; }
                    }
                    Require(ran, "actual original movement trigger reaches a running clip");
                    view.Transform.position += new Vector3(.4f, .2f, 0);
                    view.Transform.localScale = new Vector3(-1, 1, 1);
                    Tick(); yield return null;
                    view.RefreshAppearance();
                    Require(view.Animator.runtimeAnimatorController is AnimatorOverrideController, "view refresh retains its independent original controller");
                    Require(view.Play("die_01", false) && view.Animator.GetBool("die"), "native view dispatches the original ordinary death graph");
                    for (int frame = 0; frame < 80; frame++) { Tick(); yield return null; }
                    Require(view.Animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name.Contains("die")) && !view.IsAnimationFinished,
                        "actual native death persists for revival instead of removing its entity");
                    CaptureView("dead");
                    Require(view.Play("idle_01", true) && !view.Animator.GetBool("die"), "native view revival stops original death parameters");
                    for (int frame = 0; frame < 80; frame++) { Tick(); yield return null; }
                    CaptureView("revived");
                    view.Dispose(); view.Dispose(); yield return null;
                    state.Hp = 0; view = Create();
                    Require(view.Animator.GetBool("immediately_die"), "restored dead native player uses the original on-start graph");
                    for (int frame = 0; frame < 12; frame++) { Tick(); yield return null; }
                    CaptureView("restored-dead");
                    Require(view.Play("idle_01", true) && !view.Animator.GetBool("immediately_die"), "restored native role can revive");
                    Require(JsonUtility.ToJson(profile) == saved, "native view preserves its supplied local profile");
                    Debug.Log("Original native player view passed: " + label + "; exact role hierarchy, shadow replacement/identity, body ordering, extracted movement, Unity-owned physics, refresh, death, revival, restored death and unchanged profile.");

                    OfficialBattlePlayerNativeModelView Create() => new OfficialBattlePlayerNativeModelView(state, host.transform,
                        scope, resources, 17, () => profile, _ => profile.InitialWeaponType);
                    void Tick() { view.Tick(Time.deltaTime); scope.Tick(Time.deltaTime); scope.LateTick(); }
                    void CaptureView(string suffix)
                    {
                        var renderers = view.Transform.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        Capture(bounds, label + "-" + suffix);
                    }
                }
                finally
                {
                    view?.Dispose();
                    if (cameraObject.TryGetComponent<Camera>(out var camera)) camera.targetTexture = null;
                    Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(host);
                }
                yield return null;
            }
        }
    }
}
