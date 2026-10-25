using System;
using System.Collections.Generic;
using System.IO;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Opening;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.CharacterCreation
{
    public static class OpeningFighterAssetTools
    {
        public static void ImportAndRun()
        {
            Import();
            CharacterOpeningVisualPlayModeAcceptance.Run();
        }
        private const string Root = "Assets/Official/Opening/Fighters/";
        [Serializable] private sealed class Payload { public string manifest; public Fighter[] fighters; }
        [Serializable] private sealed class Fighter
        {
            public int classId;
            public string stem;
            public float scale;
            public string[] skins;
            public Page[] pages;
            public OfficialBattleAnimationAlias[] animations;
        }
        [Serializable] private sealed class Page { public string name, file; }

        [MenuItem("Zjcs/Opening/Import Official Fight NPCs")]
        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportUnitBarBorders();
            Payload payload = JsonUtility.FromJson<Payload>(File.ReadAllText(Root + "fighters.json"));
            if (payload == null || payload.manifest != "854_156383" || payload.fighters.Length != 2)
                throw new InvalidDataException("Opening fighter source is not pinned.");
            var catalog = OpeningNpcModelCatalog.LoadDefault();
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = payload.fighters.Length;
            for (int index = 0; index < payload.fighters.Length; index++)
            {
                Fighter fighter = payload.fighters[index];
                var materials = new List<Material>();
                foreach (Page page in fighter.pages)
                {
                    string path = Root + page.file;
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Default;
                    importer.textureShape = TextureImporterShape.Texture2D;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.sRGBTexture = true;
                    importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 4096;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                    Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if(texture==null) throw new InvalidDataException("Official NPC atlas page failed to import: "+path);
                    string matPath = Root + Path.GetFileNameWithoutExtension(page.file) + ".mat";
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (material == null)
                    {
                        material = new Material(NativeShader());
                        AssetDatabase.CreateAsset(material, matPath);
                    }
                    material.shader=NativeShader();
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
                EditorUtility.SetDirty(atlas);
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
                if (data == null) throw new InvalidDataException("Official NPC binary failed to import: " + fighter.classId);
                foreach (string skin in fighter.skins)
                    if (data.FindSkin(skin) == null) throw new InvalidDataException("Official NPC skin missing: " + skin);
                foreach (OfficialBattleAnimationAlias animation in fighter.animations)
                    if (data.FindAnimation(animation.spineName) == null)
                        throw new InvalidDataException("Official NPC timeline missing: " + animation.spineName);
                EditorUtility.SetDirty(skeleton);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("classId").intValue = fighter.classId;
                entry.FindPropertyRelative("skeleton").objectReferenceValue = skeleton;
                entry.FindPropertyRelative("skin").stringValue = fighter.skins[0];
                entry.FindPropertyRelative("scale").floatValue = fighter.scale;
                SerializedProperty skins = entry.FindPropertyRelative("skins");
                skins.arraySize = fighter.skins.Length;
                for (int i = 0; i < fighter.skins.Length; i++) skins.GetArrayElementAtIndex(i).stringValue = fighter.skins[i];
                SerializedProperty aliases = entry.FindPropertyRelative("animations");
                aliases.arraySize = fighter.animations.Length;
                for (int i = 0; i < fighter.animations.Length; i++)
                {
                    aliases.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = fighter.animations[i].name;
                    aliases.GetArrayElementAtIndex(i).FindPropertyRelative("spineName").stringValue = fighter.animations[i].spineName;
                }
                Debug.Log("Official opening NPC " + fighter.classId + ": " + data.Animations.Count + " animations, "
                    + fighter.skins.Length + " combined skins.");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            OpeningDamageAssetTools.Import();
        }
        private static void ImportUnitBarBorders()
        {
            var root=(Dictionary<string,object>)Spine.Json.Deserialize(new StringReader(
                File.ReadAllText("Assets/Official/Combat/HUD/unit-bars.json")));
            var entities=(Dictionary<string,object>)root["entities"];
            var imported=new HashSet<string>();
            foreach(object value in entities.Values)
            foreach(object part in (List<object>)((Dictionary<string,object>)value)["parts"])
            {
                var data=(Dictionary<string,object>)part;
                if(!data.TryGetValue("sprite",out object raw))continue;
                var sprite=(Dictionary<string,object>)raw;
                string file=Path.GetFileName((string)sprite["file"]);
                if(!imported.Add(file))continue;
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Official/Combat/HUD/Sprites/"+file);
                var border=(List<object>)sprite["border"];
                var native=new Vector4(Convert.ToSingle(border[0]),Convert.ToSingle(border[1]),
                    Convert.ToSingle(border[2]),Convert.ToSingle(border[3]));
                if(importer.spriteBorder==native)continue;
                importer.spriteBorder=native;
                importer.SaveAndReimport();
            }
        }
        private static Shader NativeShader() => AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Spine/Runtime/spine-unity/Shaders/Spine-Skeleton.shader")
            ?? throw new InvalidDataException("The approved native Spine shader is absent.");
    }
}
