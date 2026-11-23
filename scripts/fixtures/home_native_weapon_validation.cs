using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.NativeSpine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

public sealed partial class HomeFurnitureControllerChecks
{
    IEnumerator CheckNativeWeapon()
    {
        const string folder = "Assets/Official/Home/WeaponParts/";
        var catalog = AssetDatabase.LoadAssetAtPath<NativeSpineAppearanceCatalog>(folder + "HomeWeaponAppearanceCatalog.asset");
        Require(catalog && catalog.Config && catalog.Textures.Length != 0, "Selected original native Spine catalog and configuration are packaged");
        var textureGuids = new HashSet<string>(catalog.Textures.Select(value => value.Guid), StringComparer.Ordinal);
        foreach (var asset in catalog.Assets)
        {
            var loader = asset.Asset as YooAssetTextureLoader;
            Require(loader && loader.placeholderMap.All(value =>
                textureGuids.Contains(value.targetTextureReference.AssetReference.Guid) &&
                (ReferenceEquals(value.dyeTextureReference.AssetReference, null) || !value.dyeTextureReference.AssetReference.IsValid || textureGuids.Contains(value.dyeTextureReference.AssetReference.Guid))),
                "Original required/optional texture references retain valid catalog GUIDs or empty optional GUIDs");
        }
        // Only renderer pooling is used here; the manager's UI shader/material
        // branch is outside this world-renderer component validation.
        var managerConfig = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
        var manager = new AppearanceAssetManager(managerConfig, callback => new Subscription(() => { }));
        var bindings = new NativeSpineAppearanceBindings(catalog, manager);
        var animationBindings = new NativeAnimationBindings();
        var owner = New("ActualNativeRackAndWeapon"); owner.SetActive(false);
        var originalRack = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/StorageBox/WeaponRack/StorageWeaponRack.prefab");
        var instance = Instantiate(originalRack, owner.transform, false);
        var rack = instance.GetComponent<HomeWeaponRack>();
        var modes = instance.GetComponent<HomeViewModeControl>();
        var actions = instance.GetComponent<HomeSwitcherActionEventHandler>();
        var data = new RackServices(New("NativeWeaponInstantiationStage"), path =>
            AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Prefabs/" + path + ".prefab"), weapon => {
                foreach (var lite in weapon.GetComponentsInChildren<AppearanceRendererLite>(true)) lite.Configure(bindings);
                foreach (var side in weapon.GetComponentsInChildren<AppearancePartSideSwitcher>(true)) side.AnimationBindings = animationBindings;
            });
        rack.Services = data; modes.ViewModes = data; actions.ActionEvents = data;
        int skin = data.Tables.SpineSkinInfoParser.GetAll().Single(value => value.SkinType == SkinType.pWeapon && value.SkinName == "pWeapon_100_G_007").Id;
        data.Home.Data.SkinMap[SkinType.pWeapon] = new SkinData { Type = SkinType.pWeapon, SkinId = skin };
        owner.SetActive(true); yield return null; yield return null;
        Require(data.Loaded.Count == 1 && rack.PSlot.childCount == 1, "Original rack controller instantiates the actual Home weapon prefab in the authored slot");
        var weaponRoot = rack.PSlot.GetChild(0);
        var renderer = weaponRoot.GetComponentInChildren<SkeletonAnimation>();
        var bridge = weaponRoot.GetComponentInChildren<NativeSpineAppearanceAdapter>();
        var liteRenderer = weaponRoot.GetComponentInChildren<AppearanceRendererLite>();
        Require(renderer && renderer.valid && bridge && bridge.Source == renderer && liteRenderer.SkeletonRenderer == bridge,
            "Recovered native components retain real Spine renderer and explicit application bridge identities");
        Require(renderer.Skeleton.Skin.Name == "pWeapon_100_G_007" && bridge.SkeletonDataName == "primaryWeapon",
            "Original skin, skeleton data name and texture-loader address selection agree");
        Require(renderer.AnimationState.GetCurrent(0).Animation.Name == "100/100_loop" && renderer.AnimationState.GetCurrent(0).Loop,
            "Native weapon starts its authored looping animation");
        var materials = liteRenderer.GetIndependentMaterials();
        float refreshTime = renderer.AnimationState.GetCurrent(0).TrackTime;
        var updateMode = renderer.UpdateMode;
        bridge.UpdateAnimationAndMesh();
        Require(renderer.AnimationState.GetCurrent(0).TrackTime == refreshTime && renderer.UpdateMode == updateMode,
            "Original immediate mesh refresh does not advance animation time or change visibility update policy");
        Require(materials.Length > 0 && bindings.OpenTextureHandles > 0 && materials.All(value => value && value.mainTexture && value.mainTexture.width > 32 && value.shader.isSupported),
            "Original independent-material path requests actual high-resolution textures through the native catalog");
        Require(materials.All(value => Mathf.Approximately(value.GetFloat(AppearanceMaterialUtils.sLightAtten), 0)),
            "Original HomeWeapon applies its authored light attenuation to independent materials");
        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        Require(mesh && mesh.vertexCount > 0 && mesh.triangles.Length > 0, "Real Spine SDK produces the weapon's native attachment mesh");
        CaptureNativeWeapon(instance, renderer); yield return null;
        var follower = weaponRoot.GetComponentInChildren<SkeletonRendererFollower>(true);
        var bone = renderer.Skeleton.FindBone("bone3");
        Require(follower && follower.SkeletonRenderer == bridge && bone != null, "Original effect follower remains bound to the authored bone through the SDK bridge");
        bool effectActive = follower.gameObject.activeSelf;
        follower.Initialize(); follower.ForceUpdate();
        float before = renderer.AnimationState.GetCurrent(0).TrackTime;
        renderer.Update(.23f); bridge.UpdateAnimationAndMesh(); yield return null;
        follower.ForceUpdate();
        Require(renderer.AnimationState.GetCurrent(0).TrackTime > before, "Native animation state advances continuously");
        Vector3 expected = renderer.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0));
        Require(Vector2.Distance(follower.transform.position, expected) < .0001f, "Original follower evaluates the real animated bone position");
        Require(follower.gameObject.activeSelf == effectActive, "Explicit follower component validation preserves the authored effect activation state");
        CaptureNativeWeapon(instance, renderer);

        HomeWeaponAnimation.PlayAnimation(animationBindings, renderer, "200/200_loop", false, .25f, true);
        var entry = renderer.AnimationState.GetCurrent(0);
        Require(entry.Animation.Name == "200/200_loop" && !entry.Loop && Mathf.Approximately(entry.MixDuration, .25f) && Mathf.Approximately(entry.MixTime, .001f) &&
                entry.Next != null && entry.Next.Animation.Name == "100/100_loop" && entry.Next.Loop,
            "Original animation helper preserves mix timing and queues the previous animation for resume");
        HomeWeaponAnimation.PlayAnimation(animationBindings, renderer, "100/100_loop", true);
        Require(renderer.AnimationState.GetCurrent(0).Next == null && renderer.AnimationState.GetCurrent(0).MixTime == 0,
            "Original looping branch clears resume queue and zero-duration mix time");
        CheckNativeSides(renderer, animationBindings);
        GameObject retained = weaponRoot.gameObject;
        data.Main.Raise(); yield return null;
        Require(data.Loaded.Count == 1 && rack.PSlot.GetChild(0).gameObject == retained, "Unchanged actual weapon does not allocate another resource handle");
        Destroy(owner); yield return null; yield return null;
        Require(data.Released == 1 && bindings.OpenAssetHandles == 0 && bindings.OpenTextureHandles == 0,
            "Destroy releases original prefab, texture-loader handles and high-resolution texture requests");
        data.Dispose(); bindings.Dispose(); manager.Dispose(); Destroy(managerConfig);
        Debug.Log("HOME_NATIVE_WEAPON_PASSED: authored rack load, real Spine skin/animation/mesh/follower, high textures, light attenuation, animation/side branches and owner teardown. Selected prefab component coverage only.");
        yield return CheckNativeWeaponControllers();
    }

    void CheckNativeSides(SkeletonAnimation renderer, IHomeWeaponAnimationBindings bindings)
    {
        var root = New("OriginalSideSwitchBranches");
        var side = root.AddComponent<AppearancePartSideSwitcher>(); side.AnimationBindings = bindings; side.SkeletonAnimation = renderer;
        var first = Child(root, "FirstMatchingFront"); first.SetActive(false);
        var last = Child(root, "LastMatchingFront"); last.SetActive(false);
        var back = Child(root, "Back");
        side.Sides = new[] {
            new AppearancePartSide { SideType = AppearancePartSideType.Front, SkeletonAnimName = "100/100_loop", GameObjects = new[] { first } },
            new AppearancePartSide { SideType = AppearancePartSideType.Front, SkeletonAnimName = "200/200_loop", GameObjects = new[] { last } },
            new AppearancePartSide { SideType = AppearancePartSideType.Back, SkeletonAnimName = "100/100_loop", GameObjects = new[] { back } }
        };
        int callbacks = 0;
        side.OnSideChanged = () => { callbacks++; Require(side.Side == AppearancePartSideType.Front ? last.activeSelf && !back.activeSelf : back.activeSelf && !last.activeSelf, "Original side callback runs after GameObject state changes"); };
        side.SetSide(AppearancePartSideType.Front);
        Require(!first.activeSelf && last.activeSelf && !back.activeSelf && callbacks == 1 && renderer.AnimationState.GetCurrent(0).Animation.Name == "200/200_loop", "Original side switch selects the last match and its native animation");
        side.SetSide(AppearancePartSideType.Back);
        Require(!first.activeSelf && !last.activeSelf && back.activeSelf && callbacks == 2 && renderer.AnimationState.GetCurrent(0).Animation.Name == "100/100_loop", "Original back side disables both front groups and changes animation");
        HomeWeaponAnimation.PlayAnimation(bindings, renderer, "__fixture_absent_animation__", true);
        Require(renderer.AnimationState.GetCurrent(0).Animation.Name == "100/100_loop", "Missing original animation warns and retains the current track");
        side.SetSide((AppearancePartSideType)17);
        Require(!first.activeSelf && !last.activeSelf && !back.activeSelf && callbacks == 2, "Unmatched original side only deactivates groups and returns");
        Destroy(root);
    }

    void CaptureNativeWeapon(GameObject rack, SkeletonAnimation weapon)
    {
        Bounds bounds = rack.GetComponentInChildren<SpriteRenderer>().bounds;
        bounds.Encapsulate(weapon.GetComponent<MeshRenderer>().bounds);
        viewCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
        viewCamera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x * 1080f / 608f) * .65f;
        viewCamera.backgroundColor = new Color(.17f, .2f, .24f); viewCamera.clearFlags = CameraClearFlags.SolidColor;
        var render = new RenderTexture(608, 1080, 24); viewCamera.targetTexture = render; viewCamera.Render();
        var previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(608, 1080, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 608, 1080), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_FURNITURE_VALIDATION_DIR"), "native-rack-weapon.png"), image.EncodeToPNG());
        RenderTexture.active = previous; viewCamera.targetTexture = null; render.Release(); Destroy(render); Destroy(image);
    }

    sealed class NativeAnimationBindings : IHomeWeaponAnimationBindings
    {
        public Spine.Animation GetOrLoadSpineAnimation(ISkeletonComponent component, string name) => component.SkeletonDataAsset.GetSkeletonData(true).FindAnimation(name);
        public void UpdateAnimation(SkeletonRenderer renderer) => renderer.GetComponent<NativeSpineAppearanceAdapter>().UpdateAnimationAndMesh();
        public void UpdateAnimation(SkeletonGraphic graphic) { graphic.ApplyAnimation(); graphic.UpdateMesh(); }
    }
}
