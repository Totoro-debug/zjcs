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
            ["Assets/Official/Partners/UI/Card/Converted/59de19d261370c02.shader"] = 260
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
            if (stages != 8844) throw new BuildFailedException("Original shader stage count changed: " + stages);
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
                if (Regex.IsMatch(source, @"(?m)^\s*#else\b"))
                    throw new BuildFailedException("Original sparse shader gained an unbounded fallback: " + path);
                string[] currentNames = Regex.Matches(source,
                    @"(?m)^#pragma multi_compile(?:_local)? __ (\w+)\s*$")
                    .Cast<Match>().Select(value => value.Groups[1].Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (currentNames.Length == 0 || currentNames.Length > 30
                    || names != null && !names.SequenceEqual(currentNames))
                    throw new BuildFailedException("Original shader keyword domain changed: " + path);
                names = currentNames;
                var currentPrograms = new HashSet<int>();
                foreach (Match branch in Regex.Matches(source, @"(?m)^#(?:if|elif) (.+)\r?$"))
                {
                    string condition = branch.Groups[1].Value.Trim();
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
