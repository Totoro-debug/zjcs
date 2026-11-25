using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Spine.Unity;

public static class HomeFruitTreeNativeValidation
{
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    // Render the original prefab's actual Spine meshes without activating
    // gameplay components whose local owners are not yet installed.
    public static void Render()
    {
        GameObject stage = null;
        try
        {
            string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
            Require(File.Exists(Path.Combine(work, ".zjcs-validation.json")) &&
                Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar), "Independent render project required.");
            stage = new GameObject("Native fruit-tree render validation"); stage.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/FruitTree/Native/FruitTree.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab, stage.transform, false);
            var source = instance.GetComponentsInChildren<SkeletonAnimation>(true).Single();
            source.Initialize(true);
            foreach (string animation in new[] { "loop_1", "loop_2", "loop_3" })
            {
                Require(source.Skeleton.Data.FindAnimation(animation) != null, "Original tree animation missing: " + animation);
                source.Skeleton.SetToSetupPose();
                source.AnimationState.SetAnimation(0, animation, true);
                source.Update(0.5f); source.LateUpdateMesh();
                var mesh = source.GetComponent<MeshFilter>().sharedMesh;
                var materials = source.GetComponent<MeshRenderer>().sharedMaterials;
                Require(mesh && mesh.vertexCount > 0 && materials.Length == mesh.subMeshCount, "Original mesh/material bindings.");
                Capture(mesh, materials, Path.Combine(work, "native-fruit-tree-" + animation + ".png"));
            }
            Debug.Log("HOME_FRUIT_TREE_RENDER_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally { if (stage) UnityEngine.Object.DestroyImmediate(stage); }
    }

    private static void Capture(Mesh mesh, Material[] materials, string path)
    {
        var target = RenderTexture.GetTemporary(768, 768, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            Bounds bounds = mesh.bounds;
            float scale = 1.8f / Mathf.Max(bounds.size.x, bounds.size.y);
            Matrix4x4 fit = Matrix4x4.Scale(new Vector3(scale, scale, 0.01f)) * Matrix4x4.Translate(-bounds.center);
            using (var commands = new CommandBuffer())
            {
                commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    Require(materials[i] && materials[i].shader && materials[i].shader.isSupported, "Original tree shader must be supported.");
                    commands.DrawMesh(mesh, fit, materials[i], i, 0);
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            RenderTexture.active = target;
            texture = new Texture2D(768, 768, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); texture.Apply();
            Color32[] pixels = texture.GetPixels32();
            int visible = pixels.Count(color => color.a > 32);
            int magenta = pixels.Count(color => color.a > 32 && color.r > 240 && color.b > 240 && color.g < 16);
            Require(visible > 3000 && magenta < visible / 100, "Original mesh must render visible textured pixels without missing-shader color.");
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            if (texture) UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    public static void Run()
    {
        try
        {
            const string root = "Assets/Official/Home/FruitTree/Native/";
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(root + "FruitTree.prefab");
            var fruit = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Fruit.prefab");
            Require(tree && fruit, "Both original prefabs must import.");
            foreach (var prefab in new[] { tree, fruit })
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                        "Missing native script: " + prefab.name + "/" + transform.name);
            var scripts = tree.GetComponentsInChildren<MonoBehaviour>(true);
            Require(scripts.Count(value => value.GetType().Name == "HomeFruitTree") == 1, "Original parent controller.");
            Require(scripts.Count(value => value.GetType().Name == "HomeFruitTreePet") == 1, "Original pet subtree.");
            Require(scripts.Count(value => value.GetType().Name == "HomeFruitTreeProduceFruit") == 1, "Original production subtree.");
            var output = scripts.Single(value => value.GetType().Name == "HomeFruitTreeHangUpHandlerFruitItem");
            var serialized = new SerializedObject(output);
            var effect = serialized.FindProperty("mEffectItem").objectReferenceValue as Component;
            Require(effect && effect.gameObject == fruit, "The output handler must point to the original external Fruit component.");
            Require(serialized.FindProperty("mEffectItemSlotConfigs").arraySize == 10, "All ten original fruit slots.");
            var renderers = tree.GetComponentsInChildren<SkeletonAnimation>(true);
            Require(renderers.Length > 0, "Original Spine renderer must be retained.");
            foreach (var renderer in renderers)
            {
                Require(renderer.skeletonDataAsset, "Original skeleton data reference.");
                var data = renderer.skeletonDataAsset.GetSkeletonData(false);
                Require(data != null && data.Bones.Count > 0 && data.Animations.Count > 0, "Native skeleton and animation data must decode.");
            }
            Debug.Log("HOME_FRUIT_TREE_NATIVE_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
