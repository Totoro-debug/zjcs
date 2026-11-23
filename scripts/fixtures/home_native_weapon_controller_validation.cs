using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.NativeSpine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.ProfessionUI;

public sealed partial class HomeFurnitureControllerChecks
{
    IEnumerator CheckNativeWeaponControllers()
    {
        const string folder = "Assets/Official/Home/WeaponParts/";
        var catalog = AssetDatabase.LoadAssetAtPath<NativeSpineAppearanceCatalog>(folder + "HomeWeaponAppearanceCatalog.asset");
        var config = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
        var manager = new AppearanceAssetManager(config, callback => new Subscription(() => { }));
        var resources = new NativeSpineAppearanceBindings(catalog, manager);
        var animations = new NativeAnimationBindings();
        foreach (string name in new[] { "sWeapon_11_015", "pWeapon_100_G_136", "pWeapon_100_G_073" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Prefabs/Appearance/PlayerPart/" + name + "/" + name + "_Home.prefab");
            Require(prefab, "Actual native controller witness is installed: " + name);
            var stage = New("NativeControllerStage_" + name); stage.SetActive(false);
            var model = Instantiate(prefab, stage.transform, false);
            foreach (var lite in model.GetComponentsInChildren<AppearanceRendererLite>(true)) lite.Configure(resources);
            foreach (var side in model.GetComponentsInChildren<AppearancePartSideSwitcher>(true)) side.AnimationBindings = animations;
            stage.SetActive(true); yield return null; yield return null;
            var renderer = model.GetComponentInChildren<SkeletonAnimation>(true);
            var bridge = model.GetComponentInChildren<NativeSpineAppearanceAdapter>(true);
            Require(renderer && renderer.valid && renderer.Skeleton.Skin.Name == name && bridge.Source == renderer,
                "Original native controller model initializes its authored skeleton and skin: " + name);
            int local = 0, complete = 0;
            Action<IProfessionBoneOverrideAnimation> onLocal = value => { Require(ReferenceEquals(value, bridge), "SDK local callback retains renderer identity"); local++; };
            Action<IProfessionBoneOverrideAnimation> onComplete = value => { Require(local == complete + 1, "SDK complete callback follows the local callback"); complete++; };
            bridge.UpdateLocal += onLocal; bridge.UpdateComplete += onComplete;
            renderer.ApplyAnimation();
            bridge.UpdateLocal -= onLocal; bridge.UpdateComplete -= onComplete;
            Require(local == 1 && complete == 1, "Native Spine animation forwards each original bone-controller phase once; local=" + local + ", complete=" + complete);
            if (name == "sWeapon_11_015")
            {
                var side = model.GetComponentInChildren<AppearancePartSideSwitcher>(true);
                Require(side && side.Sides.Length == 2 && side.SkeletonAnimation == renderer, "Original secondary weapon side references remain native");
                foreach (var value in side.Sides)
                {
                    side.SetSide(value.SideType);
                    Require(renderer.AnimationState.GetCurrent(0).Animation.Name == value.SkeletonAnimName &&
                        value.GameObjects.All(item => item.activeSelf), "Native secondary side selects its authored groups and animation: " + value.SideType);
                }
            }
            if (name == "pWeapon_100_G_136")
            {
                var controller = model.GetComponentInChildren<SkeletonBoneController>(true);
                var rotation = model.GetComponentInChildren<SkeletonBoneRotOverride>(true);
                var locker = model.GetComponentInChildren<TransformEulerZLocker>(true);
                Require(controller && rotation && locker && rotation.Controller == controller, "Native rotation controller, override and angle locker retain original references");
                stage.transform.eulerAngles = new Vector3(0, 0, 37);
                locker.gameObject.SendMessage("Update", SendMessageOptions.RequireReceiver);
                Require(Mathf.Abs(Mathf.DeltaAngle(locker.transform.eulerAngles.z, locker.WorldEulerZ)) < .001f, "Original world-angle locker compensates the rotated model parent");
                var bone = renderer.Skeleton.FindBone(rotation.BoneName);
                Require(bone != null, "Native rotation override finds the authored Spine bone");
                int checkedLocal = 0;
                Action<IProfessionBoneOverrideAnimation> inspect = value => {
                    float angle = rotation.transform.localEulerAngles.z; if (angle > 180) angle -= 360;
                    Require(Mathf.Abs(bone.Rotation - angle) < .001f && Mathf.Abs(bone.AppliedRotation - angle) < .001f,
                        "Original Override branch writes real local/applied bone rotation before world constraints");
                    checkedLocal++;
                };
                bridge.UpdateLocal += inspect; renderer.ApplyAnimation(); bridge.UpdateLocal -= inspect;
                Require(checkedLocal == 1, "Actual native bone controller received the SDK local phase");
                var proxy = bridge.FindOverrideBone(rotation.BoneName);
                rotation.RotMode = OverrideRotMode.Offset; rotation.UseDelta = false;
                bone.Rotation = 5; bone.AppliedRotation = 7;
                float offset = rotation.transform.localEulerAngles.z; if (offset > 180) offset -= 360;
                rotation.OverrideUpdate(bridge);
                Require(Mathf.Abs(bone.Rotation - (5 + offset)) < .001f && Mathf.Abs(bone.AppliedRotation - (7 + offset)) < .001f,
                    "Original Offset branch changes both real SDK rotation inputs");
                Require(proxy.Rotation == bone.Rotation && proxy.AppliedRotation == bone.AppliedRotation, "Bone facade reads and writes the same SDK bone used by native animation");
                rotation.UseDelta = true;
                rotation.transform.localEulerAngles += new Vector3(0, 0, 13);
                float next = rotation.transform.localEulerAngles.z; if (next > 180) next -= 360;
                bone.Rotation = 5; bone.AppliedRotation = 7;
                rotation.OverrideUpdate(bridge);
                Require(Mathf.Abs(bone.Rotation - (5 + next - offset)) < .001f && Mathf.Abs(bone.AppliedRotation - (7 + next - offset)) < .001f,
                    "Original UseDelta branch applies the change from the prior angle to both SDK inputs");
                rotation.UseDelta = false;
                rotation.RotMode = OverrideRotMode.Override;
                stage.transform.eulerAngles = Vector3.zero; locker.gameObject.SendMessage("Update");
            }
            renderer.Update(.15f); bridge.UpdateAnimationAndMesh();
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh && mesh.vertexCount > 0, "Native controller model produces an editable runtime mesh: " + name);
            Require(renderer.GetComponent<MeshRenderer>().sharedMaterials.All(value => value && value.shader.isSupported && value.mainTexture), "Native controller model uses supported original materials and textures: " + name);
            if (name == "pWeapon_100_G_073")
            {
                var preview = model.GetComponentInChildren<SkeletonRendererHighTexturePreview>(true);
                var loader = (YooAssetTextureLoader)typeof(SkeletonRendererHighTexturePreview).GetField("mTextureLoader", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
                Require(preview && loader && loader.LevelOfResolution == LevelOfResolution.Low && bridge.CustomMaterialOverride.Count > 0,
                    "Original preview retains its explicitly serialized Low loader independently of the normal High renderer path");
                foreach (var entry in bridge.CustomMaterialOverride)
                {
                    Require(loader.PlaceholderTextureIndexDict.TryGetValue(entry.Key.mainTexture, out int index), "Original preview material resolves its exact placeholder index");
                    var proxy = loader.placeholderMap[index].targetTextureReference.AssetReference;
                    var handle = (IProfessionUnsafeAssetHandle<Texture>)typeof(UnsafeAssetProxy<Texture>).GetField("mHandle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(proxy);
                    Require(handle != null && handle.IsValid && entry.Value.mainTexture == handle.Result, "Actual preview material displays the texture requested by its original shared proxy");
                }
            }
            CaptureNativeController(model, renderer, name);
            Destroy(stage); yield return null; yield return null;
            Require(resources.OpenAssetHandles == 0, "Native controller model releases its appearance loader handles: " + name);
        }
        // The explicit preview component uses the original shared texture
        // proxies rather than per-renderer requests. Close that asset-owner
        // scope only after every native model has been destroyed.
        foreach (var entry in catalog.Assets)
        {
            var loader = (YooAssetTextureLoader)entry.Asset;
            loader.TryUnloadUnusedTextures(true);
            foreach (var mapping in loader.placeholderMap)
            {
                mapping.targetTextureReference.AssetReference?.Unload();
                mapping.dyeTextureReference.AssetReference?.Unload();
            }
        }
        Require(resources.OpenTextureHandles == 0, "Shared preview texture proxies release when their asset-owner scope closes");
        resources.Dispose(); manager.Dispose(); Destroy(config);
        Debug.Log("HOME_NATIVE_WEAPON_CONTROLLERS_PASSED: original secondary sides, real SDK bone event order/rotation, native angle locker, explicit preview texture path and asset-owner closure.");
    }

    void CaptureNativeController(GameObject model, SkeletonAnimation renderer, string name)
    {
        Bounds bounds = renderer.GetComponent<MeshRenderer>().bounds;
        viewCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -20);
        viewCamera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x * 1080f / 608f) * .65f;
        viewCamera.clearFlags = CameraClearFlags.SolidColor; viewCamera.backgroundColor = new Color(.17f, .2f, .24f);
        var render = new RenderTexture(608, 1080, 24); viewCamera.targetTexture = render; viewCamera.Render();
        var previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(608, 1080, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 608, 1080), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_FURNITURE_VALIDATION_DIR"), "native-" + name + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous; viewCamera.targetTexture = null; render.Release(); Destroy(render); Destroy(image);
    }
}
