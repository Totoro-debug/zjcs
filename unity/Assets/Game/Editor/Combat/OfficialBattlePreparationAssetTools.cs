using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattlePreparationAssetTools
    {
        private const string SourcePath = "Assets/Official/Combat/Preparation/preparation.json";
        private const string DirectoryPath = "Assets/Game/Resources/Combat/Preparation";

        [Serializable] private sealed class Data { public string manifest; public Drag drag; public Area area; public Pointer pointer; }
        [Serializable] private sealed class Pointer { public ColliderData collider; public PointerTransform[] transforms; }
        [Serializable] private sealed class ColliderData { public Vector3 m_Size, m_Center; public bool m_IsTrigger, m_Enabled; }
        [Serializable] private sealed class PointerTransform { public string name; public TransformData transform; }
        [Serializable] private sealed class Drag { public float DownKeepTime, DragThreshold; }
        [Serializable] private sealed class Area
        {
            public string name;
            public TransformData transform;
            public RendererData renderer;
            public SpriteData sprite;
            public ClipData clip;
        }
        [Serializable] private sealed class TransformData
        {
            public Vector3 m_LocalPosition, m_LocalScale;
            public Quaternion m_LocalRotation;
        }
        [Serializable] private sealed class RendererData
        {
            public Color m_Color;
            public int m_SortingLayerID, m_SortingOrder, m_DrawMode, m_SpriteTileMode, m_MaskInteraction, m_SpriteSortPoint;
            public bool m_FlipX, m_FlipY;
            public Vector2 m_Size;
            public float m_AdaptiveModeThreshold;
        }
        [Serializable] private sealed class SpriteData
        {
            public string path;
            public float[] pivot, border;
            public float ppu;
            public int filter, wrapU, wrapV;
        }
        [Serializable] private sealed class ClipData
        {
            public string m_Name;
            public float m_SampleRate;
            public int m_WrapMode;
            public FloatCurve[] m_FloatCurves;
        }
        [Serializable] private sealed class FloatCurve { public CurveData curve; public string attribute, path; }
        [Serializable] private sealed class CurveData { public KeyData[] m_Curve; }
        [Serializable] private sealed class KeyData
        {
            public float time, value, inSlope, outSlope, inWeight, outWeight;
            public int weightedMode;
        }

        public static void ImportAndValidate()
        {
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath);
            if (source == null) throw new InvalidDataException("Extract official preparation assets first.");
            Data data = JsonUtility.FromJson<Data>(source.text);
            if (data.manifest != "871_156598") throw new InvalidDataException("Preparation version differs.");
            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(data.area.sprite.path);
            // This project restores native Sprite assets explicitly; it does not install the
            // 2D Sprite importer package. Retain the original RGBA pixels and sprite geometry.
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = (FilterMode)data.area.sprite.filter;
            importer.wrapModeU = (TextureWrapMode)data.area.sprite.wrapU;
            importer.wrapModeV = (TextureWrapMode)data.area.sprite.wrapV;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(data.area.sprite.path, ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);
            string spritePath = "Assets/Official/Combat/Preparation/ImgChangeBattlePosArea.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(data.area.sprite.path);
            if (texture == null)
                throw new InvalidDataException("Original preparation texture did not import: " + data.area.sprite.path
                    + "; type=" + AssetDatabase.GetMainAssetTypeAtPath(data.area.sprite.path)
                    + "; assets=" + string.Join(",", AssetDatabase.LoadAllAssetsAtPath(data.area.sprite.path)
                        .Select(value => value.GetType().FullName + ":" + value.name)));
            var nativeSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(data.area.sprite.pivot[0], data.area.sprite.pivot[1]),
                data.area.sprite.ppu, 0, SpriteMeshType.FullRect,
                new Vector4(data.area.sprite.border[0], data.area.sprite.border[1],
                    data.area.sprite.border[2], data.area.sprite.border[3]));
            nativeSprite.name = "ImgChangeBattlePosArea";
            var savedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (savedSprite == null) AssetDatabase.CreateAsset(nativeSprite, spritePath);
            else
            {
                EditorUtility.CopySerialized(nativeSprite, savedSprite);
                UnityEngine.Object.DestroyImmediate(nativeSprite);
                EditorUtility.SetDirty(savedSprite);
            }
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DirectoryPath + "/ChangeBattlePosArea_Loop.anim");
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, DirectoryPath + "/ChangeBattlePosArea_Loop.anim");
            }
            clip.ClearCurves();
            clip.name = data.area.clip.m_Name;
            clip.legacy = true;
            clip.frameRate = data.area.clip.m_SampleRate;
            clip.wrapMode = (WrapMode)data.area.clip.m_WrapMode;
            foreach (FloatCurve binding in data.area.clip.m_FloatCurves)
            {
                var keys = binding.curve.m_Curve.Select(key => new Keyframe(key.time, key.value,
                    key.inSlope, key.outSlope, key.inWeight, key.outWeight)
                    { weightedMode = (WeightedMode)key.weightedMode }).ToArray();
                clip.SetCurve(binding.path, typeof(SpriteRenderer), binding.attribute, new AnimationCurve(keys));
            }
            EditorUtility.SetDirty(clip);
            var root = new GameObject(data.area.name);
            try
            {
                root.transform.localPosition = data.area.transform.m_LocalPosition;
                root.transform.localRotation = data.area.transform.m_LocalRotation;
                root.transform.localScale = data.area.transform.m_LocalScale;
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                string materialPath = DirectoryPath + "/Sprites-Default.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidDataException("Unity native Sprites/Default shader is missing.");
                if (material == null)
                {
                    material = new Material(shader) { name = "Sprites-Default" };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.shader = shader;
                material.SetFloat("PixelSnap", 0);
                material.SetFloat("_EnableExternalAlpha", 0);
                material.SetColor("_Color", Color.white);
                material.SetVector("_Flip", Vector4.one);
                material.SetColor("_RendererColor", Color.white);
                material.renderQueue = -1;
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                EditorUtility.SetDirty(material);
                renderer.sharedMaterial = material;
                if (renderer.sprite == null || renderer.sharedMaterial == null
                    || renderer.sharedMaterial.shader.name != "Sprites/Default")
                    throw new InvalidDataException("Native official preparation sprite/material binding failed: sprite="
                        + renderer.sprite + ", material=" + renderer.sharedMaterial + ", shader="
                        + renderer.sharedMaterial?.shader?.name + ", source=" + data.area.sprite.path);
                renderer.color = data.area.renderer.m_Color;
                renderer.sortingLayerID = data.area.renderer.m_SortingLayerID;
                renderer.sortingOrder = data.area.renderer.m_SortingOrder;
                renderer.flipX = data.area.renderer.m_FlipX;
                renderer.flipY = data.area.renderer.m_FlipY;
                renderer.drawMode = (SpriteDrawMode)data.area.renderer.m_DrawMode;
                renderer.size = data.area.renderer.m_Size;
                renderer.tileMode = (SpriteTileMode)data.area.renderer.m_SpriteTileMode;
                renderer.adaptiveModeThreshold = data.area.renderer.m_AdaptiveModeThreshold;
                renderer.maskInteraction = (SpriteMaskInteraction)data.area.renderer.m_MaskInteraction;
                renderer.spriteSortPoint = (SpriteSortPoint)data.area.renderer.m_SpriteSortPoint;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var animation = root.AddComponent<Animation>();
                animation.clip = clip;
                animation.AddClip(clip, clip.name);
                animation.playAutomatically = true;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, DirectoryPath + "/ChangeBattlePosArea.prefab");
                string path = DirectoryPath + "/OfficialBattlePreparationCatalog.asset";
                var catalog = AssetDatabase.LoadAssetAtPath<OfficialBattlePreparationCatalog>(path);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<OfficialBattlePreparationCatalog>();
                    AssetDatabase.CreateAsset(catalog, path);
                }
                catalog.Source = source;
                catalog.AreaPrefab = prefab;
                var pointerRoot = new GameObject("PrepareBattlePlayerPointer");
                try
                {
                    Transform target = pointerRoot.transform;
                    foreach (var item in data.pointer.transforms)
                    {
                        var node = new GameObject(item.name).transform;
                        node.SetParent(target, false);
                        node.localPosition = item.transform.m_LocalPosition;
                        node.localRotation = item.transform.m_LocalRotation;
                        node.localScale = item.transform.m_LocalScale;
                        target = node;
                    }
                    var collider = target.gameObject.AddComponent<BoxCollider>();
                    collider.size = data.pointer.collider.m_Size;
                    collider.center = data.pointer.collider.m_Center;
                    collider.isTrigger = data.pointer.collider.m_IsTrigger;
                    collider.enabled = data.pointer.collider.m_Enabled;
                    catalog.PlayerPointerPrefab = PrefabUtility.SaveAsPrefabAsset(pointerRoot,
                        DirectoryPath + "/PrepareBattlePlayerPointer.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(pointerRoot); }
                catalog.DownKeepTime = data.drag.DownKeepTime;
                catalog.DragThreshold = data.drag.DragThreshold;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                if (Mathf.Abs(clip.length - 2.5f) > .0001f || clip.wrapMode != WrapMode.Loop
                    || renderer.sprite.border != new Vector4(15, 15, 15, 15))
                    throw new InvalidDataException("Official preparation native conversion differs.");
                Debug.Log("Official preparation imported: native sprite, border, layer, alpha and 2.5-second legacy curve; current stage ranges and drag thresholds.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
