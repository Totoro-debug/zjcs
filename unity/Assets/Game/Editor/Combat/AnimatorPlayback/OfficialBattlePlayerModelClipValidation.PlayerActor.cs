using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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
        public static void RunPlayerActors()
        {
            Run();
            SessionState.SetBool(Key + ".player-actor", true);
        }

        private static IEnumerator CheckPlayerActors(GameObject prefab,
            ProfessionNativeSceneAppearanceResources catalog, ProfessionNativeSceneWeaponCatalog weapons,
            IAppearanceAssetManager manager, IProfessionAppearanceUILiteBindings materials,
            AppearanceConfig config, ProfessionAppearanceResolver resolver)
        {
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
            var rules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
            var defaults = new ProfessionAppearanceSkinDefaults(tables,
                Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text,
                message => { throw new InvalidOperationException(message); });
            var deathBinding = OfficialBattleCharacterDeathCatalog.LoadDefault().Find(3100);
            foreach (Sex sex in new[] { Sex.Male, Sex.Female })
            foreach (int profession in new[] { 1, 10 })
            {
                var data = resolver.BuildDefaultAppearanceData(config, sex, (Profession)profession);
                var profile = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
                profile.Wardrobe.Sex = (int)sex; profile.Wardrobe.Profession = profession;
                foreach (var pair in data.SkinMap)
                    profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                string saved = JsonUtility.ToJson(profile);
                var playerBindings = new OfflineProfessionAppearanceBindings(17, () => profile, _ => profile.InitialWeaponType,
                    tables, config, rules, defaults);
                var host = new GameObject("Current original 3100 player model/controller probe");
                host.AddComponent<SortingGroup>().sortingLayerID = 1182434123;
                var parts = new PartBindings(config);
                var poolBindings = new OfflineProfessionScenePartPoolBindings(weapons, materials, parts, () => true);
                using var pool = new CharacterPartEntryProvider(poolBindings);
                var binding = new OfflineProfessionScenePlayerBindings(catalog, manager, parts, pool, playerBindings);
                var owned = new List<RuntimeAnimatorController>();
                ProfessionNativeBattlePlayerActor actor = null;
                GameObject cameraObject = null; RenderTexture target = null;
                bool dead = false;
                try
                {
                    actor = new ProfessionNativeBattlePlayerActor(prefab, host.transform, binding, 17);
                    var animator = actor.Animator; var native = actor.Renderer;
                    owned.Add(animator.runtimeAnimatorController);
                    Require(actor.Root.name == "3100" && actor.Root.transform.Find("3100_view/body/PlayerModel_Game") == animator.transform,
                        "current original 3100 root/controller path");
                    Require(animator.transform.localPosition == new Vector3(0, -.5f, 0)
                        && animator.transform.localScale == new Vector3(.3f, .3f, 1), "current embedded model transform");
                    Require(actor.Controller.gameObject == actor.Root && actor.Controller.myAnimator == animator
                        && actor.Controller.CreateOverrideController && actor.Controller.NotCreateGraph && !actor.Controller.InitOnAwake,
                        "original root controller fields and actual model cross-reference");
                    Require(actor.Appearance.AnimController == actor.Controller && !actor.Appearance.Animator
                        && actor.Controller.myInited && native.PlayerPhysicsTime == 0,
                        "original embedded appearance selects independent controller at zero physical time");
                    Require(animator.runtimeAnimatorController is AnimatorOverrideController
                        && animator.runtimeAnimatorController != prefab.GetComponent<Animator>().runtimeAnimatorController,
                        "actor owns its original requested override controller");
                    cameraObject = new GameObject("Current original 3100 visibility camera");
                    var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.aspect = .75f;
                    var bounds = native.GetComponent<MeshRenderer>().bounds;
                    camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / .75f) * 1.5f;
                    camera.transform.position = bounds.center + Vector3.back * 100;
                    target = new RenderTexture(384, 512, 24); camera.targetTexture = target;
                    using var scope = new OfflineBattleNativePlayableScope(host.transform, null);
                    using var death = new OfficialBattleCharacterDeathPlayback(actor.Root, () => dead, scope, deathBinding);
                    actor.Appearance.OnAnimatorChanged += death.OnAnimatorInited;
                    death.OnAnimatorInited();
                    for (int frame = 0; frame < 10; frame++) { scope.Tick(Time.deltaTime); scope.LateTick(); yield return null; }
                    Require(native.PlayerPhysicsTime > 0, "root-bound model advances only through actual Unity frames");
                    foreach (var renderer in actor.Root.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
                    string label = sex.ToString().ToLowerInvariant() + "-" + profession + "-3100-actor";
                    CaptureActor(label + "-idle");
                    var state = native.State; float before = native.PlayerPhysicsTime;
                    playerBindings.Refresh(AppearanceOperateSource.Normal);
                    owned.Add(animator.runtimeAnimatorController);
                    Require(native.State == state && native.PlayerPhysicsTime == before && actor.Controller.myInited,
                        "original profile refresh preserves physical state while replacing the owned controller");
                    // Real original death graphs resolve the controller on entity
                    // 3100, not on the nested model. No direct Animator.Play.
                    dead = true; death.OnDie(true);
                    Require(animator.GetBool("die"), "normal death graph resolves original entity root controller");
                    for (int frame = 0; frame < 80; frame++) { scope.Tick(Time.deltaTime); scope.LateTick(); yield return null; }
                    Require(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name.Contains("die")), "actual original death animation becomes active");
                    CaptureActor(label + "-dead");
                    dead = false; scope.Dispatch(() => death.OnDie(false));
                    Require(!animator.GetBool("die"), "revival stops graph and restores original death boolean");
                    for (int frame = 0; frame < 80; frame++) { scope.Tick(Time.deltaTime); scope.LateTick(); yield return null; }
                    Require(animator.GetCurrentAnimatorClipInfo(0).All(c => !c.clip.name.Contains("die")), "actual original controller exits death on revival");
                    CaptureActor(label + "-revived");
                    dead = true; scope.Dispatch(death.OnAnimatorInited);
                    Require(animator.GetBool("immediately_die") && !animator.GetBool("die"), "restored dead role uses original on-start death graph");
                    for (int frame = 0; frame < 10; frame++) { scope.Tick(Time.deltaTime); scope.LateTick(); yield return null; }
                    CaptureActor(label + "-on-start-dead");
                    dead = false; scope.Dispatch(() => death.OnDie(false));
                    Require(!animator.GetBool("immediately_die"), "on-start death graph also restores on revival");
                    Require(JsonUtility.ToJson(profile) == saved, "battle model leaves supplied profile unchanged");
                    actor.Appearance.OnAnimatorChanged -= death.OnAnimatorInited;
                    Debug.Log("Current original 3100 player actor passed: " + label + "; four-node source chain, independent root controller, actual LateUpdate, profile refresh, normal/on-start death graphs, revival and unchanged profile.");

                    void CaptureActor(string name)
                    {
                        var current = native.GetComponent<MeshRenderer>().bounds;
                        foreach (var renderer in actor.Root.GetComponentsInChildren<MeshRenderer>())
                            if (renderer.enabled) current.Encapsulate(renderer.bounds);
                        Capture(current, name);
                    }
                }
                finally
                {
                    actor?.Dispose(); pool.ClearPool();
                    if (cameraObject) { cameraObject.GetComponent<Camera>().targetTexture = null; Object.Destroy(cameraObject); }
                    if (target) Object.Destroy(target);
                    Object.Destroy(host);
                }
                yield return null;
                Require(poolBindings.LiveInstanceCount == 0 && owned.All(value => !value), "actor releases all owned weapons and override controllers");
                Require(prefab.GetComponent<Animator>().runtimeAnimatorController, "shared prefab controller survives actor disposal");
            }
        }
    }
}
