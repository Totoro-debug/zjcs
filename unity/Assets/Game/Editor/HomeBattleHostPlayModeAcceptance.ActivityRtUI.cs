using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseActivityRtUI(string output)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_ACTIVITY_RT_PREFAB");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab, "Import the verified original TempRTUI component fixture.");
            var pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
            Require(pipeline, "Bind the existing original Universal render pipeline.");
            using var pipelineScope = pipeline.Enter();
            var fixture = new GameObject("Original activity RT component fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var main = new GameObject("RT component fixture clear camera", typeof(Camera));
            main.transform.SetParent(fixture.transform, false);
            main.tag = "MainCamera";
            var camera = main.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            var staging = new GameObject("Inactive original RT component staging", typeof(RectTransform));
            staging.transform.SetParent(fixture.transform, false);
            staging.SetActive(false);
            var assets = BattleNativeMonsterAssets.Load();
            // This fixture exercises the shared RT transport using the existing
            // verified 3709 world mesh. It does not claim to bind the 3706 model.
            var template = BattleNativeMonsterRenderer.Create(assets, 3709, staging.transform, "Original native test model");
            var seats = new SpineRTUIManager();
            var bindings = new ActivityRtBindings(assets, template.gameObject, seats);
            GameObject first = null, second = null;
            RTHandle firstHandle = null, secondHandle = null;
            Camera firstCamera = null, secondCamera = null;
            try
            {
                first = CreateActivityRtIcon(prefab, fixture.transform, staging.transform, bindings, template.gameObject);
                var a = first.GetComponent<TempRTUI>();
                first.SetActive(true);
                yield return null;
                firstHandle = a.RTColorTextureHandle;
                firstCamera = bindings.Cameras[0];
                Require(firstHandle.rt && firstHandle.rt.width == 640 && firstHandle.rt.height == 512
                    && a.RawImage.texture == firstHandle.rt && firstCamera.gameObject.activeSelf,
                    "The original RT size, texture assignment or initial seat differs.");
                Require(firstCamera.orthographic && firstCamera.orthographicSize == 3.5f
                    && firstCamera.aspect == 1.25f && !firstCamera.enabled
                    && firstCamera.transform.position == new Vector3(-5000, -5000, -100),
                    "The original disabled offscreen camera projection differs.");
                Require(a.LocalPos == new Vector3(0, -2.7f, 10) && a.LocalScale == new Vector3(.3f, .3f, 1)
                    && bindings.Models[0].transform.localPosition == a.LocalPos
                    && bindings.Models[0].transform.localScale == a.LocalScale,
                    "Original RT target transforms differ.");
                for (int frame = 0; frame < 6; frame++)
                { bindings.Advance(.04f); yield return null; }
                yield return new WaitForEndOfFrame();
                byte[] before = ReadActivityRt(firstHandle.rt, out int visible);
                Require(visible > 500, "The original Universal2D render pass produced no native model pixels.");
                for (int frame = 0; frame < 8; frame++)
                { bindings.Advance(.04f); yield return null; }
                yield return new WaitForEndOfFrame();
                Require(!before.SequenceEqual(ReadActivityRt(firstHandle.rt, out _)),
                    "The original offscreen pass did not render the advanced native Animator pose.");
                var screen = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(output, "activity-rt-native.png"), screen.EncodeToPNG()); }
                finally { UnityEngine.Object.Destroy(screen); }

                second = CreateActivityRtIcon(prefab, fixture.transform, staging.transform, bindings, template.gameObject);
                var b = second.GetComponent<TempRTUI>();
                second.SetActive(true); yield return null;
                secondHandle = b.RTColorTextureHandle;
                secondCamera = bindings.Cameras[1];
                Require(b.RawImage.texture == firstHandle.rt && !secondCamera.gameObject.activeSelf,
                    "A matching target must reuse the occupied seat's texture without rendering twice.");
                seats.ReturnRenderSeat(int.MaxValue);
                Require(!secondCamera.gameObject.activeSelf, "A foreign instance returned the original render seat.");
                first.SetActive(false); yield return null;
                Require(!firstCamera.gameObject.activeSelf && secondCamera.gameObject.activeSelf
                    && b.RawImage.texture == secondHandle.rt && bindings.BloomOwners.Count == 1,
                    "Disabling the owner did not transfer its render seat and unregister its bloom requirement.");
                first.SetActive(true); yield return null;
                Require(!firstCamera.gameObject.activeSelf && a.RawImage.texture == secondHandle.rt,
                    "Reopening the first icon must reuse the current owner's texture.");
                UnityEngine.Object.Destroy(second); second = null;
                yield return null; yield return null;
                Require(!secondCamera && !secondHandle.rt && firstCamera.gameObject.activeSelf
                    && a.RawImage.texture == firstHandle.rt && bindings.BloomOwners.Count == 1,
                    "Destroying the owner did not release its camera/RT and hand back the seat.");
                UnityEngine.Object.Destroy(first); first = null;
                yield return null; yield return null;
                Require(!firstCamera && !firstHandle.rt && bindings.BloomOwners.Count == 0
                    && bindings.Registers == 3 && bindings.Unregisters == 3,
                    "Original RT destruction leaked a camera, texture, or bloom registration.");
                Debug.Log("[Battle hosts] Original TempRTUI passed: native 3709 test mesh pixels and Animator changes, original 3706 icon geometry/camera/RT settings, same-target sharing, foreign-seat guard, disable/reopen handover and camera/RT disposal. Production 3706 model, particles and bloom output are not covered.");
            }
            finally
            {
                if (second) UnityEngine.Object.DestroyImmediate(second);
                if (first) UnityEngine.Object.DestroyImmediate(first);
                seats.Dispose();
                UnityEngine.Object.Destroy(fixture);
            }
        }

        private static GameObject CreateActivityRtIcon(GameObject prefab, Transform parent, Transform staging,
            ActivityRtBindings bindings, GameObject template)
        {
            var icon = UnityEngine.Object.Instantiate(prefab, staging, false);
            icon.SetActive(false);
            icon.transform.SetParent(parent, false);
            var view = icon.GetComponent<TempRTUI>();
            view.Bindings = bindings;
            view.RTTarget = template;
            return icon;
        }

        private static byte[] ReadActivityRt(RenderTexture source, out int visible)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = source;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                image.Apply();
                visible = image.GetPixels32().Count(pixel => pixel.a > 0 && (pixel.r > 0 || pixel.g > 0 || pixel.b > 0));
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
        }

        private sealed class ActivityRtBindings : IActivityRtUIBindings
        {
            private readonly BattleNativeMonsterAssets assets;
            private readonly GameObject template;
            public ISpineRTUIManager RenderSeats { get; }
            public readonly List<BattleNativeMonsterRenderer> Models = new List<BattleNativeMonsterRenderer>();
            public readonly List<Camera> Cameras = new List<Camera>();
            public readonly HashSet<IUIBloomRequirer> BloomOwners = new HashSet<IUIBloomRequirer>();
            public int Registers, Unregisters;
            public ActivityRtBindings(BattleNativeMonsterAssets value, GameObject original, ISpineRTUIManager seats)
            { assets = value; template = original; RenderSeats = seats; }
            public GameObject InstantiateTarget(GameObject source, Transform parent)
            {
                Require(source == template, "The RT target identity differs.");
                var model = BattleNativeMonsterRenderer.Create(assets, 3709, parent, "Original native 3709 RT test mesh");
                model.BindOriginalAnimator(assets, 3709);
                // Select the actual original model node from the battle entity
                // carrier; TempRTUI then applies its own serialized transforms.
                model.transform.SetParent(parent, false);
                UnityEngine.Object.Destroy(model.EntityRoot.gameObject);
                Models.Add(model); Cameras.Add(parent.GetComponent<Camera>());
                return model.gameObject;
            }
            public Material[] GetIndependentMaterials(GameObject instance)
            { return instance.GetComponent<MeshRenderer>().sharedMaterials; }
            public void RegisterBloom(IUIBloomRequirer requirer)
            { Require(BloomOwners.Add(requirer), "Duplicate original bloom registration."); Registers++; }
            public void UnregisterBloom(IUIBloomRequirer requirer)
            { Require(BloomOwners.Remove(requirer), "Unbalanced original bloom unregistration."); Unregisters++; }
            public void Advance(float delta)
            { foreach (var model in Models) if (model) model.Advance(delta); }
        }
    }
}
