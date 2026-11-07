using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor
{
    // These translated shaders contain the original executable keyword combinations,
    // which are a sparse subset of the Cartesian product emitted by multi_compile.
    // Never compile an absent program, or substitute another program for it.
    public sealed class OfficialSparseShaderVariants : IPreprocessShaders
    {
        private static readonly Dictionary<string, int> originalPrograms = new Dictionary<string, int>
        {
            ["Assets/Game/Runtime/Official/ProfessionUI/SpineSkeletonGraphicHeightClip.shader"] = 16,
            ["Assets/Game/Runtime/Official/World/NativeWater/Shaders/EnvironmentNoise.shader"] = 260,
            ["Assets/Game/Runtime/Official/Combat/Shaders/OfficialSummonSkeletonEffect2D.shader"] = 768,
            ["Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicCustom_05649282.shader"] = 16,
            ["Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/UniversalRenderPipelineSpineSkeletonEffect2D_19e49995.shader"] = 768,
            ["Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/BoundEntity/UniversalRenderPipelineSpineSkeletonEffect2D_19e49995.shader"] = 768,
            ["Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/BoundEntity/EffectNoiseMaskPro_6d8d993d.shader"] = 260,
            ["Assets/Game/Runtime/Official/World/NativeMap14/Shaders/Butterfly1.shader"] = 260,
            ["Assets/Game/Runtime/Offline/World/Map12/BuildingPreview.shader"] = 6,
            ["Assets/Official/Partners/UI/Card/Converted/59de19d261370c02.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/Cast_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/GroundEffectNoiseMaskPro.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/GridEntity_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/MonsterBodyEffect_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/MovementEffect_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/PartnerHp_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/PetFollower_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/PetTransfer_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/PlayerEffect_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/SkillGraph_FullScreen-Polar.shader"] = 3,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/SkillGraph_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/SkillGraph_Spine-SkeletonEffect-URP-2D.shader"] = 768,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/StatusAnimation_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/StatusFrozen_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/StatusMaterial_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/StatusOrbit_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/StatusSpineFollower_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/Track_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Game/Runtime/Presentation/Combat/Shaders/WeaponPrefab_Noise_Mask_Pro_0.shader"] = 260,
            ["Assets/Official/Combat/NativeMonsters/OriginalSkeletonEffect.shader"] = 768,
            ["Assets/Official/Combat/NativePartnerAssets/OriginalSkeletonEffect.shader"] = 768,
            ["Assets/Official/Combat/DemonResultRtModels/OriginalSkeletonEffect.shader"] = 768,
            ["Assets/Official/Combat/DemonResultRtModels/shaders/0acd001ab2a50fe9138876feb10a6193dd286155a16607a22e1c5900ec55c414.shader"] = 3,
            ["Assets/Official/Combat/DemonResultRtPrefab/Resources/Combat/DemonResultRtPrefab/Payload/Converted/59de19d261370c02.shader"] = 260,
            ["Assets/Official/Combat/DemonResultRtPrefab/Resources/Combat/DemonResultRtPrefab/Payload/Converted/a6ffae7daff41fb6.shader"] = 3,
            ["Assets/Official/Combat/DemonResultRtPrefab/Resources/Combat/DemonResultRtPrefab/Payload/Converted/b29a473e62738eea.shader"] = 768,
            ["Assets/Official/Combat/NativeMonsters/shaders/0acd001ab2a50fe9138876feb10a6193dd286155a16607a22e1c5900ec55c414.shader"] = 3,
            ["Assets/Official/DailyChallenges/ActivityRtMonsters/OriginalSkeletonEffect.shader"] = 768,
            ["Assets/Official/DailyChallenges/ActivityRtPrefab/Resources/DailyChallenges/ActivityRtPrefab/Payload/Converted/59de19d261370c02.shader"] = 260,
            ["Assets/Official/DailyChallenges/ActivityRtPrefab/Resources/DailyChallenges/ActivityRtPrefab/Payload/Converted/b29a473e62738eea.shader"] = 768,
            ["Assets/Official/DailyChallenges/Resources/ActivityMonsterIcons/Converted/893dda98db004c56.shader"] = 16,
            ["Assets/Official/DailyChallenges/Resources/ActivityMonsterIcons/Converted/b29a473e62738eea.shader"] = 768,
            ["Assets/Official/DailyChallenges/UI/Activity/Converted/4e00a32c496e3e94.shader"] = 16,
            ["Assets/Official/DailyChallenges/UI/Activity/Converted/59de19d261370c02.shader"] = 260,
            ["Assets/Official/DailyChallenges/UI/Activity/Converted/6a51e3ded6b39a3c.shader"] = 12,
            ["Assets/Official/DailyChallenges/UI/Activity/Converted/b29a473e62738eea.shader"] = 768,
            ["Assets/Official/DailyChallenges/UI/Rank/Converted/4e00a32c496e3e94.shader"] = 16,
            ["Assets/Official/DailyChallenges/UI/Rank/Converted/b29a473e62738eea.shader"] = 768,
            ["Assets/Official/Partners/UI/DetailActors/Converted/5dfd400a7875c92f.shader"] = 6,
            ["Assets/Official/Partners/UI/DetailActors/Converted/b29a473e62738eea.shader"] = 768,
            ["Assets/Official/World/Shared/NativeMiniMapMarkersCurrent/Converted/59de19d261370c02.shader"] = 260,
            ["Assets/Official/World/Shared/NativeMiniMapPanelCurrent/Converted/59de19d261370c02.shader"] = 260,
            ["Assets/Official/World/Map/16/NativeStoryPlayables/Shaders/Noise_Mask_Pro.shader"] = 260
        };
        private static readonly Dictionary<string, Selection> selections = new Dictionary<string, Selection>();
        public int callbackOrder => -100;

        private sealed class Selection
        {
            public LocalKeyword[] Keywords;
            public string[] Names;
            public HashSet<int> Programs;
            public int PassCount;
        }

        public static void ValidateProgramDomains()
        {
            selections.Clear();
            int stages = 0;
            foreach (var original in originalPrograms)
            {
                AssetDatabase.ImportAsset(original.Key, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(original.Key);
                if (shader == null) throw new BuildFailedException("Original shader is missing: " + original.Key);
                Selection selection = Read(shader, original.Key, original.Value);
                selections.Add(original.Key, selection);
                stages += selection.PassCount * selection.Programs.Count * 2;
            }
            if (stages != 49816) throw new BuildFailedException("Original shader stage domain changed: " + stages);
            Debug.Log("Original sparse shader domains validated: " + originalPrograms.Count + " assets / " + stages + " GPU stages.");
        }

        [MenuItem("Zjcs/Assets/Validate Original Sparse Shader Programs")]
        public static void Validate()
        {
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            int stages = 0;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var original in originalPrograms)
                {
                    string path = original.Key;
                    // Clear messages left by a previous build of absent combinations.
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (shader == null) throw new BuildFailedException("Original shader is missing: " + path);
                    Selection selection = Read(shader, path, original.Value);
                    selections[path] = selection;
                    var source = ShaderUtil.GetShaderData(shader).GetSubshader(0);
                    for (int pass = 0; pass < selection.PassCount; pass++)
                        foreach (int program in selection.Programs)
                        {
                            string[] keywords = selection.Names.Where((name, index) => (program & 1 << index) != 0).ToArray();
                            foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                            {
                                var result = source.GetPass(pass).CompileVariant(stage, keywords,
                                    ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                                if (!result.Success) throw new BuildFailedException("Original shader program failed: "
                                    + path + " " + pass + " " + stage + " " + string.Join(",", keywords));
                                stages++;
                            }
                        }
                    if (ShaderUtil.ShaderHasError(shader))
                        throw new BuildFailedException("Original shader has compilation errors: " + path);
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previousAsync; }
            if (stages != 49816) throw new BuildFailedException("Original shader stage count changed: " + stages);
            Debug.Log("Original sparse shaders: all " + stages + " Windows GPU stages across "
                + originalPrograms.Count + " original shader assets compiled.");
            ProjectTools.Validate();
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            string path = AssetDatabase.GetAssetPath(shader);
            if (!originalPrograms.TryGetValue(path, out int expected)) return;
            if (!selections.TryGetValue(path, out Selection selection))
            {
                selection = Read(shader, path, expected);
                selections.Add(path, selection);
            }
            int before = data.Count;
            for (int i = data.Count - 1; i >= 0; i--)
            {
                int signature = 0;
                for (int k = 0; k < selection.Keywords.Length; k++)
                    if (data[i].shaderKeywordSet.IsEnabled(selection.Keywords[k])) signature |= 1 << k;
                if (!selection.Programs.Contains(signature)) data.RemoveAt(i);
            }
            Debug.Log("[Original shader programs] " + shader.name + " " + snippet.passType + " "
                + snippet.shaderType + ": " + before + " -> " + data.Count);
        }

        private static Selection Read(Shader shader, string path, int expected)
        {
            var blocks = Regex.Matches(File.ReadAllText(path), @"CGPROGRAM\s*\n(.*?)ENDCG", RegexOptions.Singleline);
            string[] names = null;
            HashSet<int> programs = null;
            foreach (Match block in blocks)
            {
                string source = block.Groups[1].Value;
                string[] currentNames = Regex.Matches(source,
                    @"(?m)^#pragma multi_compile(?:_local)? __ (\w+)\s*$")
                    .Cast<Match>().Select(value => value.Groups[1].Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (currentNames.Length == 0 || currentNames.Length > 30
                    || names != null && !names.SequenceEqual(currentNames))
                    throw new BuildFailedException("Original shader keyword domain changed: " + path);
                names = currentNames;
                var currentPrograms = new HashSet<int>();
                int depth = 0;
                foreach (Match branch in Regex.Matches(source,
                    @"(?m)^\s*#(if|ifdef|ifndef|elif|else|endif)\b([^\r\n]*)"))
                {
                    string directive = branch.Groups[1].Value;
                    bool selectsProgram = directive == "if" && depth == 0 || directive == "elif" && depth == 1;
                    if (directive == "else" && depth == 1)
                        throw new BuildFailedException("Original sparse shader gained an unbounded fallback: " + path);
                    if (directive == "if" || directive == "ifdef" || directive == "ifndef") depth++;
                    else if (directive == "endif") depth--;
                    // Original bodies may contain platform branches such as
                    // UNITY_UV_STARTS_AT_TOP. Only the outer chain selects programs.
                    if (!selectsProgram) continue;
                    string condition = branch.Groups[2].Value.Trim();
                    MatchCollection terms = Regex.Matches(condition, @"(!?)defined\((\w+)\)");
                    if (terms.Count != names.Length || string.Join(" && ", terms.Cast<Match>().Select(value => value.Value)) != condition
                        || !terms.Cast<Match>().Select(value => value.Groups[2].Value)
                            .OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(names))
                        throw new BuildFailedException("Original shader branch is not an exact keyword selection: " + path);
                    int signature = 0;
                    foreach (Match term in terms)
                        if (term.Groups[1].Value.Length == 0) signature |= 1 << Array.IndexOf(names, term.Groups[2].Value);
                    if (!currentPrograms.Add(signature))
                        throw new BuildFailedException("Original shader program selection is ambiguous: " + path);
                }
                if (depth != 0) throw new BuildFailedException("Original shader condition nesting changed: " + path);
                if (currentPrograms.Count != expected || programs != null && !programs.SetEquals(currentPrograms))
                    throw new BuildFailedException("Recheck original shader program domains for each pass: " + path);
                programs = currentPrograms;
            }
            if (programs == null) throw new BuildFailedException("Original shader programs are missing: " + path);
            return new Selection { Keywords = names.Select(name => new LocalKeyword(shader, name)).ToArray(),
                Names = names, Programs = programs, PassCount = blocks.Count };
        }
    }
}
