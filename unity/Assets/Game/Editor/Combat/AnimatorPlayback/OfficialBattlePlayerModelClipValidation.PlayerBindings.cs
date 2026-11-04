using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Player = Zjcs.Official.CombatAppearance.PlayerAnimatorAppearance;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        public static void RunPlayerAppearanceBindings()
        {
            RunLivePlayerPhysics();
            SessionState.SetBool(Key + ".player-appearance-bindings", true);
        }

        private static IEnumerator CheckPlayerAppearanceBindings(GameObject prefab,
            ProfessionNativeSceneAppearanceResources catalog, ProfessionNativeSceneWeaponCatalog weapons,
            IAppearanceAssetManager manager, IProfessionAppearanceUILiteBindings materials,
            AppearanceConfig config, ProfessionAppearanceResolver resolver)
        {
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
            var rules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
            var defaults = new ProfessionAppearanceSkinDefaults(tables, Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text,
                message => { throw new InvalidOperationException(message); });
            var previousClip = ProfessionAppearanceSkinClipBindings.Current;
            ProfessionAppearanceSkinClipBindings.Current = new OfflineProfessionSceneSkinClipBindings();
            try
            {
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
                    var staging = new GameObject("Original Game player binding staging"); staging.SetActive(false);
                    var host = new GameObject("Game player lifecycle rendering probe"); host.transform.SetParent(staging.transform, false);
                    host.AddComponent<SortingGroup>().sortingLayerID = 1182434123;
                    var root = Object.Instantiate(prefab, host.transform, false);
                    var native = root.GetComponent<ProfessionNativeSceneRenderer>();
                    var player = root.GetComponent<Player>();
                    var parts = new PartBindings(config);
                    var poolBindings = new OfflineProfessionScenePartPoolBindings(weapons, materials, parts, () => true);
                    using var pool = new CharacterPartEntryProvider(poolBindings);
                    var binding = new OfflineProfessionScenePlayerBindings(catalog, manager, parts, pool, playerBindings);
                    GameObject cameraObject = null; RenderTexture target = null;
                    int notices = 0, completions = 0;
                    bool configured = false;
                    player.OnAppearanceRefreshed += () => notices++;
                    try
                    {
                        int bound = binding.BindTree(root); configured = true;
                        Require(bound == 1 && binding.BindTree(root) == 0,
                            "bind original inactive Game graph once without initializing a sampled pose");
                        Require(root.transform.localScale == Vector3.one && native.State == null,
                            "binding retains original model transform and deferred skeleton initialization");
                        host.transform.SetParent(null, false);
                        player.Init(17, onComplete: () => completions++);
                        Require(player.Inited && notices == 1 && completions == 1 && native.PlayerPhysicsTime == 0,
                            "original saved-player Init and zero-time skin setup complete before first real frame");
                        var animator = root.GetComponent<Animator>();
                        Require(animator.runtimeAnimatorController.name == sex + "_" + (profession == 1 ? "Zhanshi" : "Fashi") + "_Controller",
                            "original Game address selects the exact sex/profession controller");
                        native.UpdateAnimationAndMesh();
                        var bounds = native.GetComponent<MeshRenderer>().bounds;
                        cameraObject = new GameObject("Original player binding visibility camera");
                        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.aspect = .75f;
                        camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / .75f) * 1.4f;
                        camera.transform.position = bounds.center + Vector3.back * 100;
                        target = new RenderTexture(384, 512, 24); camera.targetTexture = target;
                        for (int frame = 0; frame < 8; frame++) yield return null;
                        Require(native.PlayerPhysicsTime > 0, "fresh bound model automatically advances real physical time");
                        // Include the now-following weapon; a body-only frame
                        // cuts off the original long staff during image review.
                        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                            if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
                        string label = sex.ToString().ToLowerInvariant() + "-" + profession + "-player-bindings";
                        Capture(bounds, label + "-initial");
                        var state = native.State;
                        float before = native.PlayerPhysicsTime;
                        int refreshes = notices;
                        playerBindings.Refresh(AppearanceOperateSource.Normal);
                        Require(notices == refreshes + 1 && ReferenceEquals(state, native.State) && native.PlayerPhysicsTime == before,
                            "real profile event refresh retains skeleton and physical history with zero elapsed time");
                        Require(binding.BindTree(root) == 0, "repeated binding preserves the active player");
                        host.SetActive(false); before = native.PlayerPhysicsTime;
                        playerBindings.Refresh(AppearanceOperateSource.Normal);
                        Require(notices == refreshes + 1, "disabled original player unsubscribes profile events");
                        for (int frame = 0; frame < 3; frame++) yield return null;
                        Require(native.PlayerPhysicsTime == before, "pooled original player retains frozen physics");
                        host.SetActive(true);
                        Require(notices == refreshes + 2 && native.PlayerPhysicsTime == before,
                            "original OnEnable refreshes once and retains physical history");
                        for (int frame = 0; frame < 8; frame++) yield return null;
                        Require(native.PlayerPhysicsTime > before, "pooled player resumes actual LateUpdate");
                        Capture(bounds, label + "-resumed");
                        Require(JsonUtility.ToJson(profile) == saved, "appearance binding never changes the supplied saved profile");
                        Debug.Log("Original Game player binding passed: " + label + "; profile Init/refresh, exact controller, zero-time setup, automatic body physics, idempotent binding, original disable/pool events and unchanged profile.");
                    }
                    finally
                    {
                        host.SetActive(false);
                        if (configured) { player.Clear(); player.AppearanceCharacter.Release(true); }
                        pool.ClearPool(); native.Release();
                        if (cameraObject) { cameraObject.GetComponent<Camera>().targetTexture = null; Object.Destroy(cameraObject); }
                        if (target) Object.Destroy(target);
                        Object.Destroy(host); Object.Destroy(staging);
                    }
                    yield return null;
                    Require(poolBindings.LiveInstanceCount == 0, "bound Game player releases all owned weapon parts");
                }
            }
            finally { ProfessionAppearanceSkinClipBindings.Current = previousClip; }
        }
    }
}
