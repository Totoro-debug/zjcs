using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.NativeSpine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.ProfessionUI;

public sealed partial class HomeFurnitureControllerChecks
{
    [Serializable] sealed class WeaponCatalogInput { public WeaponCatalogSelection[] selections; }
    [Serializable] sealed class WeaponCatalogSelection { public WeaponCatalogAsset asset; public int[] spine_skin_ids; }
    [Serializable] sealed class WeaponCatalogAsset { public string path; }
    [Serializable] sealed class CatalogMaterialInfo
    {
        public string name, shader, texture, asset;
        public int width, height;
    }
    [Serializable] sealed class CatalogRenderInfo
    {
        public string prefab;
        public CatalogMaterialInfo[] materials;
    }

    IEnumerator CheckAllNativeWeaponPrefabs()
    {
        const string folder = "Assets/Official/Home/WeaponParts/";
        string source = Environment.GetEnvironmentVariable("ZJCS_FURNITURE_WEAPON_MANIFEST");
        var inputs = JsonUtility.FromJson<WeaponCatalogInput>(File.ReadAllText(source));
        int expected = int.Parse(Environment.GetEnvironmentVariable("ZJCS_FURNITURE_EXPECTED_PREFABS"));
        CatalogNeed(inputs.selections.Length == expected && expected > 0, "Full official weapon selection must be installed");
        var catalog = AssetDatabase.LoadAssetAtPath<NativeSpineAppearanceCatalog>(folder + "HomeWeaponAppearanceCatalog.asset");
        var config = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
        var manager = new AppearanceAssetManager(config, callback => new Subscription(() => { }));
        var resources = new NativeSpineAppearanceBindings(catalog, manager);
        var animations = new NativeAnimationBindings();
        const int tileWidth = 90, tileHeight = 160, columns = 8, rows = 8;
        var render = new RenderTexture(tileWidth, tileHeight, 24);
        var tile = new Texture2D(tileWidth, tileHeight, TextureFormat.RGBA32, false);
        var sheet = new Texture2D(tileWidth * columns, tileHeight * rows, TextureFormat.RGBA32, false);
        int sheetStart = 0, sidesChecked = 0, rendered = 0;
        var sourceFailures = new List<string>();
        for (int index = 0; index < inputs.selections.Length; index++)
        {
            string original = inputs.selections[index].asset.path;
            string path = folder + original.Substring("Assets/".Length);
            string name = Path.GetFileNameWithoutExtension(original);
            Debug.Log("HOME_NATIVE_CATALOG_ITEM: " + index + " " + name);
            int priorErrors = errors;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CatalogNeed(prefab, "Missing exact native prefab: " + original);
            var authoredRenderer = prefab.GetComponentInChildren<SkeletonAnimation>(true);
            CatalogNeed(authoredRenderer && authoredRenderer.skeletonDataAsset, "Missing authored native skeleton: " + name);
            if (authoredRenderer.skeletonDataAsset.GetSkeletonData(false).FindSkin(authoredRenderer.initialSkinName) == null)
            {
                string failure = original + " -> missing authored skin " + authoredRenderer.initialSkinName;
                var references = inputs.selections[index].spine_skin_ids;
                CatalogNeed(references != null && references.Length == 0, "An official skin-table entry can select the invalid original prefab: " + failure);
                sourceFailures.Add(failure);
                Debug.LogWarning("HOME_NATIVE_CATALOG_UNREFERENCED_SOURCE_GAP: " + failure);
                continue;
            }
            var stage = New("CatalogStage_" + name); stage.SetActive(false);
            var model = Instantiate(prefab, stage.transform, false);
            foreach (var lite in model.GetComponentsInChildren<AppearanceRendererLite>(true)) lite.Configure(resources);
            foreach (var side in model.GetComponentsInChildren<AppearancePartSideSwitcher>(true)) side.AnimationBindings = animations;
            CatalogNeed(model.GetComponentsInChildren<Transform>(true).All(value => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(value.gameObject) == 0), "Missing native component: " + name);
            stage.SetActive(true); yield return null;
            CatalogNeed(errors == priorErrors, "Native prefab activation logged an error: " + name);
            var renderer = model.GetComponentInChildren<SkeletonAnimation>(true);
            var bridge = model.GetComponentInChildren<NativeSpineAppearanceAdapter>(true);
            CatalogNeed(renderer && renderer.valid && bridge && bridge.Source == renderer, "Native Spine renderer did not initialize: " + name);
            CatalogNeed(renderer.Skeleton.Skin != null && renderer.Skeleton.Skin.Name == renderer.initialSkinName, "Authored native skin did not resolve: " + name);
            foreach (var side in model.GetComponentsInChildren<AppearancePartSideSwitcher>(true))
            {
                var authored = side.Side;
                foreach (var state in side.Sides ?? Array.Empty<AppearancePartSide>())
                {
                    side.SetSide(state.SideType);
                    if (!string.IsNullOrEmpty(state.SkeletonAnimName))
                        CatalogNeed(renderer.AnimationState.GetCurrent(0)?.Animation.Name == state.SkeletonAnimName, "Authored side animation did not play: " + name + " / " + state.SkeletonAnimName);
                    CatalogNeed(state.GameObjects == null || state.GameObjects.All(value => value.activeSelf), "Authored side group did not activate: " + name);
                    sidesChecked++;
                }
                side.SetSide(authored);
            }
            // Exercise every real model even while the batch camera has not
            // reached it; the source prefab's visibility policy stays intact.
            var visibility = renderer.UpdateMode;
            renderer.UpdateMode = UpdateMode.FullUpdate;
            renderer.Update(.17f); renderer.LateUpdateMesh();
            renderer.UpdateMode = visibility;
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            CatalogNeed(mesh && mesh.vertexCount > 0 && mesh.triangles.Length > 0, "Native skin produced no render geometry: " + name);
            var main = renderer.GetComponent<MeshRenderer>();
            CatalogNeed(main.sharedMaterials.Length > 0 && main.sharedMaterials.All(value => value && value.shader && value.shader.isSupported && value.mainTexture), "Native material/texture is unbound or unsupported: " + name);
            var materialInfo = new CatalogRenderInfo { prefab = original, materials = main.sharedMaterials.Select(value => new CatalogMaterialInfo {
                name = value.name, shader = value.shader.name, texture = value.mainTexture.name,
                asset = AssetDatabase.GetAssetPath(value.mainTexture), width = value.mainTexture.width, height = value.mainTexture.height
            }).ToArray() };
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_FURNITURE_VALIDATION_DIR"), "native-materials.jsonl"), JsonUtility.ToJson(materialInfo) + "\n");
            foreach (var other in model.GetComponentsInChildren<Renderer>(true))
            {
                CatalogNeed(other.sharedMaterials.Where(value => value).All(value => value.shader && value.shader.isSupported), "Assigned native effect shader is unsupported: " + name + " / " + other.name);
                if (other.enabled && other.gameObject.activeInHierarchy)
                    CatalogNeed(other.sharedMaterials.All(value => value), "Visible native effect material is unbound: " + name + " / " + other.name);
            }
            var bounds = main.bounds;
            CatalogNeed(bounds.size.sqrMagnitude > 0 && !float.IsNaN(bounds.size.sqrMagnitude) && !float.IsInfinity(bounds.size.sqrMagnitude), "Native render bounds are invalid: " + name);
            viewCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 20);
            viewCamera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x * tileHeight / tileWidth) * .65f;
            viewCamera.clearFlags = CameraClearFlags.SolidColor; viewCamera.backgroundColor = new Color(.17f, .2f, .24f);
            viewCamera.targetTexture = render; viewCamera.Render();
            var previous = RenderTexture.active; RenderTexture.active = render;
            tile.ReadPixels(new Rect(0, 0, tileWidth, tileHeight), 0, 0); tile.Apply();
            int cell = index - sheetStart;
            sheet.SetPixels32((cell % columns) * tileWidth, (rows - 1 - cell / columns) * tileHeight, tileWidth, tileHeight, tile.GetPixels32());
            RenderTexture.active = previous; viewCamera.targetTexture = null;
            if (name == "pWeapon_100_10114_Home") CaptureNativeController(model, renderer, name);
            if (cell == columns * rows - 1 || index == inputs.selections.Length - 1)
            {
                sheet.Apply();
                string filename = "weapons-sheet-" + sheetStart.ToString("D4") + "-" + index.ToString("D4") + ".png";
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_FURNITURE_VALIDATION_DIR"), filename), sheet.EncodeToPNG());
                sheetStart = index + 1;
                sheet.SetPixels32(new Color32[tileWidth * tileHeight * columns * rows]);
            }
            CatalogNeed(errors == priorErrors, "Native pose/render logged an error: " + name);
            rendered++;
            Destroy(stage); yield return null;
            CatalogNeed(resources.OpenAssetHandles == 0, "Native prefab retained an appearance loader handle: " + name);
            if (index % 32 == 31) Debug.Log("HOME_NATIVE_CATALOG_PROGRESS: " + (index + 1) + "/" + expected);
        }
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
        CatalogNeed(resources.OpenTextureHandles == 0, "Full native catalog retained a texture request after asset-owner shutdown");
        resources.Dispose(); manager.Dispose(); Destroy(config); render.Release(); Destroy(render); Destroy(tile); Destroy(sheet);
        Debug.Log("HOME_NATIVE_WEAPON_CATALOG_CHECKED: " + rendered + " rendered, " + sourceFailures.Count + " unresolved authored skins, " + sidesChecked + " side states.");
        CatalogNeed(rendered + sourceFailures.Count == expected, "Every selected native prefab must have an explicit result");
        Debug.Log("HOME_NATIVE_WEAPON_CATALOG_PASSED: " + rendered + " exact native prefabs rendered, " + sourceFailures.Count + " preserved invalid inputs with no current official skin-table reference, " + sidesChecked + " authored side states and resource-owner shutdown. Original blank art remains a source limitation.");
    }

    static void CatalogNeed(bool value, string message) { if (!value) throw new Exception(message); }
}
