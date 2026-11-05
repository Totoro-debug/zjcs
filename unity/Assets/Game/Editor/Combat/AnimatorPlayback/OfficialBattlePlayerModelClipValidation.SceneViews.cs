using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        private static IEnumerator CheckPlayerSceneViews(AppearanceConfig config, ProfessionAppearanceResolver resolver)
        {
            foreach (Sex sex in new[] { Sex.Male, Sex.Female })
            foreach (int profession in new[] { 1, 10 })
            {
                var profile = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
                profile.Wardrobe.Sex = (int)sex; profile.Wardrobe.Profession = profession;
                foreach (var pair in resolver.BuildDefaultAppearanceData(config, sex, (Profession)profession).SkinMap)
                    profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                string saved = JsonUtility.ToJson(profile), label = sex.ToString().ToLowerInvariant() + "-" + profession + "-shared-player";
                var state = new FightBattleUnitState { Id = 1, CharacterType = 1, RoleEntityId = 3100, Hp = 100, MaxHp = 100 };
                var checkpoint = new FightBattleCheckpoint { Grid = new FightBattleGrid { Width = 5, Height = 5 } };
                checkpoint.Units.Add(state);
                var host = new GameObject("Shared scene native player probe"); host.transform.position = new Vector3(3, -2, .4f);
                var source = new OfficialBattlePlayerAppearanceSource(1, 17, () => profile, _ => profile.InitialWeaponType);
                Require(source.Matches(state) && !source.Matches(new FightBattleUnitState { Id = 2, CharacterType = 1 })
                    && !source.Matches(new FightBattleUnitState { Id = 1, CharacterType = 7 }), "profile binding is restricted to its supplied player unit");
                using var scope = new OfflineBattleNativePlayableScope(host.transform, null);
                scope.BindNativePrefabAdapter(PartnerNativeSkillSkeleton.BindOriginalPrefab);
                var cameraObject = new GameObject("Shared player scene visibility camera");
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
                camera.aspect = .75f; camera.orthographicSize = 3;
                camera.transform.position = host.transform.position + new Vector3(.5f, .7f, -100);
                var target = new RenderTexture(384, 512, 24); camera.targetTexture = target;
                OfficialBattleSceneView scene = null;
                try
                {
                    scene = Create(); scene.Refresh(checkpoint);
                    var actor = host.GetComponentInChildren<ProfessionNativeSceneRenderer>();
                    var animator = actor.GetComponent<Animator>(); var entity = host.transform.Find("3100");
                    Require(actor && animator && entity, "shared scene creates the original player without an attached map avatar");
                    for (int frame = 0; frame < 12; frame++) { Tick(); yield return null; }
                    CaptureScene("idle");
                    var move = new FightBattleMovementEvent { UnitId = 1, MoveSkillEntityId = 15300,
                        Path = new List<FightGridPoint> { new FightGridPoint(0, 0), new FightGridPoint(1, 0) } };
                    var step = new FightBattleStepEvent { ActorId = 1, Movements = new List<FightBattleMovementEvent> { move } };
                    state.X = 1; scene.Refresh(checkpoint, step);
                    bool ran = false;
                    for (int frame = 0; frame < 80; frame++)
                    {
                        Tick(); yield return null;
                        if (!ran && animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name.Contains("run")))
                        { ran = true; CaptureScene("run"); }
                    }
                    Require(ran && Vector3.Distance(entity.position, host.transform.TransformPoint(Vector3.right)) < .001f,
                        "shared movement dispatch runs original Animator cues and reaches the checkpoint grid position");
                    state.Hp = 0; scene.Refresh(checkpoint);
                    Require(animator.GetBool("die"), "shared HP refresh begins the original death graph");
                    for (int frame = 0; frame < 80; frame++) { Tick(); yield return null; }
                    Require(entity && !entity.GetComponentInChildren<Animator>().GetBool("immediately_die"), "normal death retains the original entity");
                    CaptureScene("dead");
                    state.Hp = 100; scene.Refresh(checkpoint);
                    Require(!animator.GetBool("die"), "shared revival clears original death parameters");
                    for (int frame = 0; frame < 80; frame++) { Tick(); yield return null; }
                    CaptureScene("revived");
                    scene.Dispose(); scene = null; yield return null;
                    Require(!entity && !actor, "shared scene disposal releases its native player and resource owner");
                    state.Hp = 0; scene = Create(); scene.Refresh(checkpoint);
                    actor = host.GetComponentInChildren<ProfessionNativeSceneRenderer>(); animator = actor.GetComponent<Animator>();
                    entity = host.transform.Find("3100");
                    Require(entity && animator.GetBool("immediately_die"), "shared dead-checkpoint refresh creates its native player and original on-start graph");
                    for (int frame = 0; frame < 12; frame++) { Tick(); yield return null; }
                    CaptureScene("restored-dead");
                    state.Hp = 100; scene.Refresh(checkpoint);
                    Require(!animator.GetBool("immediately_die") && JsonUtility.ToJson(profile) == saved,
                        "shared restored player revives without changing the source profile");
                    Debug.Log("Shared original player scene passed: " + label + "; creation, extracted movement/grid track, HP death, revival, restored death and resource disposal.");

                    OfficialBattleSceneView Create() => new OfficialBattleSceneView(null, host.transform, _ => null,
                        new OfficialBattleGridMapper(host.transform, checkpoint.Grid), null, (_, __) => { },
                        nativePlayableScope: scope, playerAppearance: source);
                    void Tick() { scene.Tick(.01f); scope.Tick(.01f); scope.LateTick(); }
                    void CaptureScene(string suffix)
                    {
                        var renderers = entity.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        Capture(bounds, label + "-" + suffix);
                    }
                }
                finally
                {
                    scene?.Dispose(); camera.targetTexture = null;
                    Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(host);
                }
                yield return null;
            }
        }
    }
}
