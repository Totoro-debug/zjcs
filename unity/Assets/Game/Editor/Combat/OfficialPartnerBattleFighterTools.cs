using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialPartnerBattleFighterTools
    {
        private const string Root = "Assets/Official/Combat/NpcFighters/";
        private const string CatalogPath = "Assets/Game/Resources/Combat/OfficialBattleModelCatalog.asset";

        [Serializable] private sealed class Payload { public string manifest; public Fighter[] fighters; }
        [Serializable] private sealed class Fighter
        {
            public int classId, entityClassId;
            public string stem;
            public float scale;
            public string[] skins;
            public Page[] pages;
            public OfficialBattleAnimationAlias[] animations;
        }
        [Serializable] private sealed class Page { public string name, file; }

        [MenuItem("Zjcs/Combat/Import Official Partner Fighters")]
        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Payload payload = JsonUtility.FromJson<Payload>(File.ReadAllText(Root + "fighters.json"));
            if (payload == null || payload.manifest != "884_156692" || payload.fighters == null
                || payload.fighters.Length != 48)
                throw new InvalidDataException("Official partner fighter source is not pinned or is incomplete.");

            OfficialBattleModelCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialBattleModelCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("Official battle model catalog is missing.");
            var bindings = new List<OfficialBattleModelBinding>();
            var classIds = new HashSet<int>();
            foreach (Fighter fighter in payload.fighters)
            {
                if (fighter == null || fighter.classId <= 0 || fighter.entityClassId <= 0
                    || !classIds.Add(fighter.classId) || fighter.pages == null || fighter.skins == null
                    || fighter.animations == null)
                    throw new InvalidDataException("Official partner fighter metadata is invalid.");
                var materials = new List<Material>();
                foreach (Page page in fighter.pages)
                {
                    string path = Root + page.file;
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new InvalidDataException("Official partner atlas page is missing: " + path);
                    importer.textureType = TextureImporterType.Default;
                    importer.textureShape = TextureImporterShape.Texture2D;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.sRGBTexture = true;
                    importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 4096;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null) throw new InvalidDataException("Official partner atlas failed to import: " + path);
                    string materialPath = Root + Path.GetFileNameWithoutExtension(page.file) + ".mat";
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(NativeShader());
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    material.shader = NativeShader();
                    material.mainTexture = texture;
                    material.SetFloat("_StraightAlphaInput", 1f);
                    material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                    EditorUtility.SetDirty(material);
                    materials.Add(material);
                }

                string atlasPath = Root + fighter.stem + "_Atlas.asset";
                SpineAtlasAsset atlas = AssetDatabase.LoadAssetAtPath<SpineAtlasAsset>(atlasPath);
                if (atlas == null)
                {
                    atlas = ScriptableObject.CreateInstance<SpineAtlasAsset>();
                    AssetDatabase.CreateAsset(atlas, atlasPath);
                }
                atlas.atlasFile = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + fighter.stem + ".atlas.txt");
                atlas.materials = materials.ToArray();
                atlas.Clear();

                string skeletonPath = Root + fighter.stem + "_SkeletonData.asset";
                SkeletonDataAsset skeleton = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(skeletonPath);
                if (skeleton == null)
                {
                    skeleton = ScriptableObject.CreateInstance<SkeletonDataAsset>();
                    AssetDatabase.CreateAsset(skeleton, skeletonPath);
                }
                skeleton.skeletonJSON = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + fighter.stem + ".skel.bytes");
                skeleton.atlasAssets = new AtlasAssetBase[] { atlas };
                skeleton.scale = fighter.scale;
                skeleton.Clear();
                var data = skeleton.GetSkeletonData(false);
                if (data == null) throw new InvalidDataException("Official partner Spine skeleton failed: " + fighter.classId);
                if (data.FindSkin(fighter.skins[0]) == null)
                    throw new InvalidDataException("Official partner body skin is missing: " + fighter.skins[0]);
                foreach (string skin in fighter.skins.Skip(1))
                    if (data.FindSkin(skin) == null)
                        Debug.LogWarning("Original partner fighter has no embedded optional skin " + skin
                            + " in entity " + fighter.entityClassId + ".");
                foreach (OfficialBattleAnimationAlias animation in fighter.animations)
                    if (data.FindAnimation(animation.spineName) == null)
                        throw new InvalidDataException("Official partner animation is missing: " + animation.spineName);
                EditorUtility.SetDirty(atlas);
                EditorUtility.SetDirty(skeleton);
                bindings.Add(new OfficialBattleModelBinding {
                    key = "body_" + fighter.classId,
                    skin = fighter.skins[0],
                    skins = fighter.skins,
                    scale = fighter.scale,
                    animations = fighter.animations,
                    skeleton = skeleton
                });
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty rows = serialized.FindProperty("partnerNpcBindings");
            rows.arraySize = bindings.Count;
            for (int i = 0; i < bindings.Count; i++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("key").stringValue = bindings[i].key;
                row.FindPropertyRelative("monsterId").intValue = 0;
                row.FindPropertyRelative("skin").stringValue = bindings[i].skin;
                SerializedProperty skins = row.FindPropertyRelative("skins");
                skins.arraySize = bindings[i].skins.Length;
                for (int j = 0; j < bindings[i].skins.Length; j++)
                    skins.GetArrayElementAtIndex(j).stringValue = bindings[i].skins[j];
                row.FindPropertyRelative("scale").floatValue = bindings[i].scale;
                row.FindPropertyRelative("skeleton").objectReferenceValue = bindings[i].skeleton;
                SerializedProperty animations = row.FindPropertyRelative("animations");
                animations.arraySize = bindings[i].animations.Length;
                for (int j = 0; j < bindings[i].animations.Length; j++)
                {
                    SerializedProperty animation = animations.GetArrayElementAtIndex(j);
                    animation.FindPropertyRelative("name").stringValue = bindings[i].animations[j].name;
                    animation.FindPropertyRelative("spineName").stringValue = bindings[i].animations[j].spineName;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            OfficialBattleModelCatalog loaded = OfficialBattleModelCatalog.LoadDefault();
            foreach (Fighter fighter in payload.fighters)
            {
                string key = "body_" + fighter.classId;
                OfficialBattleModelBinding binding = loaded.Find(key);
                if (binding == null || binding.skeleton == null
                    || !loaded.HasNpcBattleAnimations(fighter.classId))
                    throw new InvalidDataException("Imported partner fighter is not playable in the battle model catalog: "
                        + fighter.classId);
                var skeleton = new Spine.Skeleton(binding.skeleton.GetSkeletonData(false));
                if (binding.ApplySkinCombination(skeleton) == 0)
                    throw new InvalidDataException("Original partner skin combination has no available skins: "
                        + fighter.classId);
            }
            Debug.Log("Official partner battle Spine assets imported: " + bindings.Count + " fighters.");
        }

        private static Shader NativeShader() => AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Spine/Runtime/spine-unity/Shaders/Spine-Skeleton.shader")
            ?? throw new InvalidDataException("Approved native Spine shader is missing.");
    }
}
