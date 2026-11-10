using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor.Home
{
    public static class DailyDungeonDistortShaderAcceptance
    {
        private const string ShaderPath = "Assets/Game/Runtime/Official/WorldUI/UIEffectDistort_884_156692.shader";
        private const string AdditiveShaderPath = "Assets/Game/Runtime/Official/Dungeons/UI/EffectAdditive.shader";
        private const string SpineDefaultShaderPath = "Assets/Game/Runtime/Official/Dungeons/Spine/SpineSkeletonGraphic_884_156692.shader";
        private const string SpineDyeShaderPath = "Assets/Game/Runtime/Official/Dungeons/Spine/SpineSkeletonGraphicDye_884_156692.shader";
        private const string MaterialRoot = "Assets/Game/Resources/Dungeons/UI/884_156692/Materials/";
        private static readonly string[] MaterialNames = { "GameMatch_Circle2", "GameMatch_Circle3" };
        private const string AdditiveMaterialPath = MaterialRoot + "UIImgAdd.mat";
        private const string AdditiveRuntimeMaterialPath = MaterialRoot + "UIImgAdd_Official.mat";
        private const string SpineMaterialRoot = MaterialRoot + "Spine/";
        private const string SpineDefaultMaterialPath = SpineMaterialRoot + "SkeletonGraphicDefault.mat";
        private const string SpineDyeMaterialPath = SpineMaterialRoot + "SkeletonGraphicDye.mat";
        private const string EntryPrefabPath = "Assets/Game/Resources/Dungeons/UI/884_156692/DailyDungeonEntryPanel.prefab";
        private const string DailyDungeonPrefabRoot = "Assets/Game/Resources/Dungeons/UI/884_156692/";

        [MenuItem("Zjcs/Dungeons/Validate Daily Dungeon Distort Shader")]
        public static void Run()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Validate()
        {
            ValidateWindowsShader(AdditiveShaderPath, AdditiveMaterialPath, "UI/EffectAdditive");
            ValidateWindowsShader(AdditiveShaderPath, AdditiveRuntimeMaterialPath, "UI/EffectAdditive runtime material");
            ValidateWindowsShader(SpineDefaultShaderPath, SpineDefaultMaterialPath,
                "Zjcs/Official/Dungeons/SpineSkeletonGraphic_884_156692");
            ValidateWindowsShader(SpineDyeShaderPath, SpineDyeMaterialPath,
                "Zjcs/Official/Dungeons/SpineSkeletonGraphicDye_884_156692");

            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null, "Daily dungeon Windows shader did not import");
            Require(shader.isSupported, "Daily dungeon Windows shader is unsupported");
            Require(!ShaderUtil.ShaderHasError(shader), "Daily dungeon Windows shader has import errors: "
                + string.Join(" | ", ShaderUtil.GetShaderMessages(shader).Select(message => message.message)));
            Material[] materials = MaterialNames.Select(name =>
            {
                string path = MaterialRoot + name + ".mat";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Require(material != null && material.shader == shader,
                    name + " does not bind the current Windows shader");
                return material;
            }).ToArray();

            GameObject entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabPath);
            Require(entryPrefab != null, "Official daily dungeon entry prefab did not load");
            Material[] entryMaterials = entryPrefab.GetComponentsInChildren<UIMesh>(true)
                .Select(mesh => mesh.materialForRendering).Where(material => material != null).ToArray();
            foreach (Material material in materials)
                Require(entryMaterials.Contains(material), "Daily dungeon entry UIMesh does not bind " + material.name);

            int stages = CompileWindowsUiVariants(shader, "UI/EffectDistort");
            int additivePrefabReferences = ValidateRuntimePrefabMaterialBinding(
                "DailyDungeonDetailContent.prefab", AdditiveRuntimeMaterialPath, "0197be4263e9bae4db9439953f71f474");
            int defaultSpineReferences = ValidateRuntimePrefabMaterialBinding(
                "DailyDungeonEndPanelItem.prefab", SpineDefaultMaterialPath, "0627795339756134ebcea556c4cec2d2");
            int dyeSpineReferences = ValidateRuntimePrefabMaterialBinding(
                "DailyDungeonEndPanelItem.prefab", SpineDyeMaterialPath, "bc4aa6bca656c6941a7fbade26616b84");

            Debug.Log("Daily dungeon UI/EffectDistort Windows shader passed: " + stages
                + " D3D stages; entry materials " + string.Join(", ", materials.Select(material => material.name))
                + "; runtime prefab material references Additive=" + additivePrefabReferences
                + ", Spine default=" + defaultSpineReferences + ", Spine dye=" + dyeSpineReferences + ".");
        }

        private static int ValidateRuntimePrefabMaterialBinding(string prefabName, string materialPath, string sourceGuid)
        {
            string prefabPath = DailyDungeonPrefabRoot + prefabName;
            string absolutePrefabPath = Path.Combine(Application.dataPath,
                prefabPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
            string prefabText = File.ReadAllText(absolutePrefabPath);
            string runtimeGuid = AssetDatabase.AssetPathToGUID(materialPath);
            Require(!string.IsNullOrEmpty(runtimeGuid), "Runtime dungeon material has no Unity GUID: " + materialPath);
            Require(!prefabText.Contains("guid: " + sourceGuid),
                prefabName + " still references the original mobile material " + sourceGuid);
            int references = prefabText.Split(new[] { "guid: " + runtimeGuid }, StringSplitOptions.None).Length - 1;
            Require(references > 0, prefabName + " does not bind runtime material " + materialPath);
            return references;
        }

        private static void ValidateWindowsShader(string shaderPath, string materialPath, string expectedName)
        {
            AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            Require(shader != null, expectedName + " Windows shader did not import");
            Require(!ShaderUtil.ShaderHasError(shader), expectedName + " Windows shader has import errors: "
                + string.Join(" | ", ShaderUtil.GetShaderMessages(shader).Select(message => message.message)));
            Require(shader.isSupported, expectedName + " Windows shader is unsupported");

            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceSynchronousImport);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Require(material != null && material.shader == shader,
                expectedName + " runtime material does not bind its verified Windows shader");
            int stages = CompileWindowsUiVariants(shader, expectedName);
            Debug.Log("Daily dungeon " + expectedName + " passed: " + stages + " D3D stages.");
        }

        private static int CompileWindowsUiVariants(Shader shader, string expectedName)
        {
            var data = ShaderUtil.GetShaderData(shader);
            var pass = data.GetSubshader(0).GetPass(0);
            var variants = new[] { Array.Empty<string>(), new[] { "UNITY_UI_ALPHACLIP" },
                new[] { "UNITY_UI_CLIP_RECT" }, new[] { "UNITY_UI_ALPHACLIP", "UNITY_UI_CLIP_RECT" } };
            int stages = 0;
            foreach (string[] keywords in variants)
                foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                {
                    var result = pass.CompileVariant(stage, keywords, ShaderCompilerPlatform.D3D,
                        BuildTarget.StandaloneWindows64);
                    Require(result.Success, expectedName + " failed Windows compilation: "
                        + stage + " " + string.Join(",", keywords) + " "
                        + string.Join(" | ", result.Messages.Select(message => message.message)));
                    stages++;
                }
            return stages;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
