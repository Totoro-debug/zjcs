using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineHudModule
        {
            public Texture2D playerHeadTexture;

            // CharacterPortraitPhotographer.Capture and HeadCharacterPortraitBuilder.
            private IEnumerator CapturePlayerHead(OfficialMap11UiData data)
            {
                var binding = data.components.Single(value => value.panel == "WorldCanvas" && value.type == "HeadCharacterPortrait").fields.mRequestor;
                var parameters = data.headPortraitSettings;
                var appearance = host.progression.ResolveAppearance();
                foreach (int type in parameters.mExcludeSkinTypes) appearance.SkinByType.Remove(type);
                GameObject sourceModel = CharacterCreationNativeModelFactory.CreatePreviewModel(host.slotId, host.persistentDataPath, appearance);
                // Reuse the validated native frame player used by the home and
                // creation screens. The old baked prefab contains zero-size face
                // and hair variants; those meshes cannot produce a full portrait.
                string[] skins = sourceModel.GetComponentsInChildren<Renderer>()
                    .Where(value => value.name.StartsWith("__skin__", StringComparison.Ordinal))
                    .Select(value => value.name.Substring("__skin__".Length)).Distinct().ToArray();
                var propertiesByType = new Dictionary<int, MaterialPropertyBlock>();
                foreach (var bindingNode in sourceModel.GetComponentsInChildren<CharacterCreationDyeMapBinding>())
                {
                    var block = new MaterialPropertyBlock();
                    bindingNode.GetComponent<Renderer>().GetPropertyBlock(block);
                    propertiesByType[bindingNode.SkinType] = block;
                }
                GameObject model = new GameObject("OfficialHeadPortraitFrames");
                var frames = model.AddComponent<CharacterCreationMeshFramePlayer>();
                frames.Initialize(host.progression.Player.Wardrobe.Sex == 1 ? "female" : "male");
                frames.ApplySkins(skins);
                frames.Play(binding.mAnimationName);
                foreach (var bindingNode in model.GetComponentsInChildren<CharacterCreationDyeMapBinding>())
                    if (propertiesByType.TryGetValue(bindingNode.SkinType, out var block))
                        bindingNode.GetComponent<Renderer>().SetPropertyBlock(block);
                frames.enabled = false;
                Destroy(sourceModel);
                // Keep the capture model outside the world cameras. Draw its renderers
                // explicitly, as the official photographer does.
                // extract_story_character_actions.mjs (and the wardrobe samples)
                // stores pixels as 2 * (160 + x, 388 - y). Undo that conversion
                // before applying the source photographer's Skeleton scale .01.
                Vector3 captureOrigin = new Vector3(10000, 10000, 0);
                model.transform.localScale = Vector3.one * .5f;
                model.transform.position = captureOrigin + new Vector3(-1.6f, 3.88f, 0);
                yield return null;
                var cameraObject = new GameObject("OfficialPlayerHeadCamera", typeof(Camera));
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = parameters.Size;
                camera.aspect = (float)parameters.TextureWidth / parameters.TextureHeight;
                camera.transform.SetPositionAndRotation(captureOrigin + parameters.mOffset + new Vector3(0, 0, -5), Quaternion.identity);
                var target = RenderTexture.GetTemporary(parameters.TextureWidth, parameters.TextureHeight, parameters.mDepthBuffer);
                var commands = new CommandBuffer { name = "Official head portrait" };
                var bakedMeshes = new List<Mesh>();
                try
                {
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>().Where(value => value.enabled)
                        // The native mesh conversion retains Spine draw order in
                        // Renderer.sortingOrder instead of separated renderer Z.
                        .OrderBy(value => value.sortingOrder).ThenByDescending(value => value.transform.position.z))
                    {
                        // The Windows conversion uses skinned meshes. Evaluate them
                        // explicitly because the source photographer draws a Spine
                        // MeshRenderer, which does not require camera skinning.
                        Mesh mesh = null;
                        if (renderer is SkinnedMeshRenderer skin)
                        {
                            mesh = new Mesh();
                            skin.BakeMesh(mesh);
                            bakedMeshes.Add(mesh);
                        }
                        var properties = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(properties);
                        for (int index = 0; index < renderer.sharedMaterials.Length; index++)
                            if (mesh != null) commands.DrawMesh(mesh, renderer.localToWorldMatrix,
                                renderer.sharedMaterials[index], index, -1, properties);
                            else commands.DrawRenderer(renderer, renderer.sharedMaterials[index], index);
                    }
                    Graphics.ExecuteCommandBuffer(commands);
                    RenderTexture previous = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = target;
                        playerHeadTexture = new Texture2D(parameters.TextureWidth, parameters.TextureHeight, TextureFormat.RGBA32, false);
                        playerHeadTexture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        playerHeadTexture.Apply();
                    }
                    finally { RenderTexture.active = previous; }
                    GameObject head = ui?.Find("WorldCanvas", binding.mRawImage);
                    if (head != null) head.GetComponent<RawImage>().texture = playerHeadTexture;
                }
                finally
                {
                    commands.Dispose();
                    foreach (Mesh mesh in bakedMeshes) Destroy(mesh);
                    RenderTexture.ReleaseTemporary(target);
                    Destroy(cameraObject);
                    Destroy(model);
                }
            }
        }
    }
}
