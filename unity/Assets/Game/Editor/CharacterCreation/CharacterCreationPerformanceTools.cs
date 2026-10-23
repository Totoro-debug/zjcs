using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Editor.CharacterCreation
{
    public static class CharacterCreationPerformanceTools
    {
        private const string ProjectAssetPath = "Assets/Game/Resources/CharacterCreation/OfficialCharacterCreationPerformanceAssets.asset";
        private const string PanelPrefabPath = CharacterCreationUiTools.Root + "/CreatePlayerPanel.prefab";
        private const string SourcePayloadPath = "Assets/Official/CharacterCreation/creation-performance.json";
        private const string SourceManifestRelativePath = "assets/reference/character-creation/creation-performance-source-manifest.json";
        private const string UiManifestRelativePath = "assets/reference/character-creation/create-player-ui-source-manifest.json";
        // This performance export retains its original source profile; the active
        // extraction directory follows the current cloud profile independently.
        private const string ShaderBundlePath = ".tools/source-packages/history/854_156383/extracted/defaultpackage_unityshaders.bundle";
        private const string ShaderBundleMd5 = "cac97355bb4799c415e3cb1ef38b6dc5";
        private const string ShaderBundleSha256 = "f6cc2ad49028110a87744a116bc2fd193af9d8e2f2e7e452b7d90c0bc934dabe";
        private const string ShaderProgramSha256 = "3ed165d5dace4e415cf23aaa1119f5e99935354348f1ad7f706d54f3a4fba559";
        private const string RuntimeShaderPath = "Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationEffectDissolve.shader";
        private const string RuntimeShaderName = "Zjcs/CharacterCreation/EffectDissolve";
        private const string PinnedApkSha256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9";
        private const string RootOwner = "/CreatePlayerPanel";
        private const string RoleOwner = RootOwner + "/Content/BgGo/RoleContent";
        private const string PayloadRoot = "Assets/Official/CharacterCreation/Recovered";

        [Serializable]
        private sealed class PerformanceSourceManifest
        {
            public string manifest;
            public string apk_sha256;
            public PerformanceSource source;
            public string[] animations;
            public string[] ui_meshes;
            public PerformanceOutput[] outputs;
        }

        [Serializable]
        private sealed class PerformanceSource
        {
            public string data_path;
            public string data_sha256;
            public string ui_source_manifest;
            public string ui_source_manifest_sha256;
            public string source_bundle;
            public string source_bundle_sha256;
            public string shader_bundle;
            public string shader_bundle_md5;
            public long shader_bundle_bytes;
            public string shader_bundle_sha256;
            public string shader_name;
            public string shader_program_sha256;
            public string runtime_shader;
            public string panel_prefab;
            public string[] official_code;
        }

        [Serializable]
        private sealed class PerformanceOutput
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        private sealed class AnimationDefinition
        {
            public string owner;
            public Dictionary<string, object> data;
        }

        [MenuItem("Zjcs/Character Creation/Build Performance Assets")]
        public static void BuildAndValidate()
        {
            CharacterCreationAssetCatalog catalog = CharacterCreationAssetCatalog.LoadDefault();
            BuildAndValidate(catalog.CreationPerformance);
        }

        public static void BuildAndValidate(TextAsset source)
        {
            if (source == null) throw new BuildFailedException("The official character creation performance data was not imported.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            if (prefab == null) throw new BuildFailedException("The official CreatePlayerPanel prefab was not imported.");

            Dictionary<string, object> document = AsDictionary(CharacterCreationJson.Parse(source.text), "performance document");
            if (String(document, "manifest") != "854_156383")
                throw new BuildFailedException("Character creation performance data is not pinned to manifest 854_156383.");

            string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            string shaderBundleAbsolute = Path.GetFullPath(Path.Combine(projectRoot,
                ShaderBundlePath.Replace('/', Path.DirectorySeparatorChar)));
            FileInfo shaderBundleFile = new FileInfo(shaderBundleAbsolute);
            if (!shaderBundleFile.Exists || shaderBundleFile.Length != 850402
                || !string.Equals(HashMd5File(shaderBundleAbsolute), ShaderBundleMd5, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(HashFile(shaderBundleAbsolute), ShaderBundleSha256, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("The pinned official shader bundle is missing or changed.");
            Shader runtimeShader = Shader.Find(RuntimeShaderName);
            if (runtimeShader == null || !runtimeShader.isSupported)
                throw new BuildFailedException("The Windows character creation dissolve shader did not compile: " + RuntimeShaderName);

            Dictionary<string, object> root = AsDictionary(document["root"], "performance root");
            List<AnimationDefinition> allAnimations = new List<AnimationDefinition>();
            var meshSources = new List<KeyValuePair<string, Dictionary<string, object>>>();
            CollectNodes(root, allAnimations, meshSources);
            List<AnimationSourceBinding> selectedAnimations = SelectAnimations(document, allAnimations);
            if (selectedAnimations.Count != 13 || meshSources.Count != 2)
                throw new BuildFailedException("The official CreatePlayerPanel animation or dissolve mesh count differs from its source.");

            CharacterCreationPerformanceAssets assets = AssetDatabase.LoadAssetAtPath<CharacterCreationPerformanceAssets>(ProjectAssetPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<CharacterCreationPerformanceAssets>();
                AssetDatabase.CreateAsset(assets, ProjectAssetPath);
            }
            RemoveGeneratedSubassets(assets);

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PanelPrefabPath);
            try
            {
                var animationBindings = new List<CharacterCreationAnimationAssetBinding>();
                foreach (AnimationSourceBinding sourceBinding in selectedAnimations)
                {
                    AnimationClip clip = BuildClip(prefabRoot, sourceBinding);
                    AssetDatabase.AddObjectToAsset(clip, assets);
                    animationBindings.Add(new CharacterCreationAnimationAssetBinding
                    {
                        owner = sourceBinding.owner,
                        alias = sourceBinding.alias,
                        clip = clip
                    });
                }

                var meshBindings = new List<CharacterCreationMeshAssetBinding>();
                foreach (KeyValuePair<string, Dictionary<string, object>> meshSource in meshSources)
                    meshBindings.Add(BuildMesh(assets, meshSource.Key, meshSource.Value));

                CharacterCreationTransformOrderBinding transformOrder = BuildTransformOrder(root);
                assets.SetGeneratedData(animationBindings.ToArray(), meshBindings.ToArray(), new[] { transformOrder });
                assets.ValidateBindings();
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            WriteSourceManifest(projectRoot, source, selectedAnimations, meshSources);
            Debug.Log("Built and validated 13 official CreatePlayerPanel clips, two dissolve meshes, and sex-specific draw order.");
        }

        private sealed class AnimationSourceBinding
        {
            public string owner;
            public string alias;
            public Dictionary<string, object> data;
        }

        private static void CollectNodes(Dictionary<string, object> node,
            List<AnimationDefinition> animations,
            List<KeyValuePair<string, Dictionary<string, object>>> meshes)
        {
            string path = String(node, "path");
            if (node.TryGetValue("animations", out object rawAnimations))
                foreach (object rawAnimation in AsList(rawAnimations, "node animations"))
                    animations.Add(new AnimationDefinition { owner = path, data = AsDictionary(rawAnimation, "animation") });

            if (node.TryGetValue("ui_mesh", out object rawMesh) && rawMesh != null)
                meshes.Add(new KeyValuePair<string, Dictionary<string, object>>(path,
                    AsDictionary(rawMesh, "UIMesh source")));

            if (node.TryGetValue("children", out object rawChildren))
                foreach (object rawChild in AsList(rawChildren, "node children"))
                    CollectNodes(AsDictionary(rawChild, "child node"), animations, meshes);
        }

        private static List<AnimationSourceBinding> SelectAnimations(Dictionary<string, object> document,
            List<AnimationDefinition> allAnimations)
        {
            var result = new List<AnimationSourceBinding>();
            Dictionary<string, object> simple = AsDictionary(document["simple"], "simple animation aliases");
            foreach (KeyValuePair<string, object> ownerGroup in simple)
            {
                Dictionary<string, object> aliases = AsDictionary(ownerGroup.Value, "animation aliases");
                foreach (KeyValuePair<string, object> alias in aliases)
                {
                    string clipName = AsString(alias.Value, "animation alias target");
                    AnimationDefinition source = allAnimations.FirstOrDefault(value => value.owner == ownerGroup.Key
                        && String(value.data, "name") == clipName);
                    if (source == null)
                        throw new BuildFailedException("The source animation alias is not bound: " + ownerGroup.Key + "/" + alias.Key);
                    result.Add(new AnimationSourceBinding { owner = ownerGroup.Key, alias = alias.Key, data = source.data });
                }
            }

            foreach (AnimationDefinition source in allAnimations.Where(value =>
                String(value.data, "name") == "CreatePlayerPanel_SelRole_Loop"
                && value.owner.EndsWith("/SelGlow", StringComparison.Ordinal)))
                result.Add(new AnimationSourceBinding { owner = source.owner, alias = "Loop", data = source.data });

            return result;
        }

        private static AnimationClip BuildClip(GameObject prefabRoot, AnimationSourceBinding source)
        {
            if (AsList(source.data["unconverted_bindings"], "unconverted bindings").Count != 0)
                throw new BuildFailedException("The official animation still has unconverted bindings: " + source.alias);

            Transform animationRoot = FindRelative(prefabRoot.transform, RelativePath(RootOwner, source.owner));
            if (animationRoot == null)
                throw new BuildFailedException("An official animation owner is missing: " + source.owner);

            var clip = new AnimationClip
            {
                name = source.alias,
                legacy = true,
                frameRate = 60f,
                wrapMode = Boolean(source.data, "loop") ? WrapMode.Loop : WrapMode.Once
            };

            foreach (object rawTrack in AsList(source.data["tracks"], "animation tracks"))
            {
                Dictionary<string, object> track = AsDictionary(rawTrack, "animation track");
                string fullPath = String(track, "path");
                string relativePath = RelativePath(source.owner, fullPath);
                string property = String(track, "property");
                GameObject target = FindRelative(animationRoot, relativePath)?.gameObject;
                if (target == null) throw new BuildFailedException("An official animation target is missing: " + fullPath);
                Type componentType = ResolveBindingType(target, property);
                var curve = new AnimationCurve();
                foreach (object rawKey in AsList(track["keys"], "animation keys"))
                {
                    List<object> key = AsList(rawKey, "animation key");
                    if (key.Count != 4) throw new BuildFailedException("An official animation key has an unexpected format: " + fullPath);
                    object rawIn = key[2];
                    object rawOut = key[3];
                    var frame = new Keyframe(Float(key[0]), Float(key[1]), rawIn == null ? 0f : Float(rawIn),
                        rawOut == null ? 0f : Float(rawOut));
                    int keyIndex = curve.AddKey(frame);
                    if (keyIndex < 0) throw new BuildFailedException("An official animation key could not be added: " + fullPath);
                    if (rawIn == null)
                        AnimationUtility.SetKeyLeftTangentMode(curve, keyIndex, AnimationUtility.TangentMode.Constant);
                    if (rawOut == null)
                        AnimationUtility.SetKeyRightTangentMode(curve, keyIndex, AnimationUtility.TangentMode.Constant);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(relativePath, componentType, property), curve);
            }

            var events = new List<AnimationEvent>();
            foreach (object rawEvent in AsList(source.data["events"], "animation events"))
            {
                Dictionary<string, object> value = AsDictionary(rawEvent, "animation event");
                events.Add(new AnimationEvent
                {
                    time = Float(value["time"]),
                    functionName = String(value, "function"),
                    intParameter = value.TryGetValue("int", out object integer) && integer != null ? (int)Float(integer) : 0,
                    floatParameter = value.TryGetValue("float", out object number) && number != null ? Float(number) : 0f,
                    stringParameter = value.TryGetValue("string", out object text) && text != null ? AsString(text, "animation event string") : string.Empty
                });
            }
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            return clip;
        }

        private static Type ResolveBindingType(GameObject target, string property)
        {
            if (property.StartsWith("m_AnchoredPosition.", StringComparison.Ordinal)
                || property.StartsWith("m_SizeDelta.", StringComparison.Ordinal)) return typeof(RectTransform);
            if (property.StartsWith("scale.", StringComparison.Ordinal)) return typeof(Transform);
            if (property == "m_Alpha") return typeof(CanvasGroup);
            if (property.StartsWith("m_fontColor.", StringComparison.Ordinal)) return typeof(TMP_Text);
            if (property.StartsWith("m_Color.", StringComparison.Ordinal)) return typeof(Graphic);
            if (property == "m_IsActive") return typeof(GameObject);
            if (property == "mCurOrderIndex") return typeof(CharacterCreationTransformReorderer);
            if (property.StartsWith("material.", StringComparison.Ordinal)) return typeof(MeshRenderer);
            if (property == "m_Enabled")
            {
                bool isUiMesh = target.GetComponent<CharacterCreationUIMesh>() != null
                    || target.name == "Mesh" || target.name == "Mesh2";
                return isUiMesh ? typeof(CharacterCreationUIMesh) : typeof(Graphic);
            }
            throw new BuildFailedException("An official character creation animation property is unsupported: " + property);
        }

        private static CharacterCreationMeshAssetBinding BuildMesh(CharacterCreationPerformanceAssets assets,
            string owner, Dictionary<string, object> source)
        {
            List<Vector3> vertices = ReadVector3List(source["vertices"]);
            List<Vector2> uv = ReadVector2List(source["uv"]);
            List<int> triangles = ReadIntList(source["triangles"]);
            var mesh = new Mesh { name = "CharacterCreationDissolve_" + (owner.Contains("FemaleGo") ? "Female" : "Male") };
            mesh.SetVertices(vertices);
            if (uv.Count == vertices.Count) mesh.SetUVs(0, uv);
            List<Color> colors = ReadColorList(source["colors"]);
            if (colors.Count == vertices.Count) mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, assets);

            Dictionary<string, object> materialData = AsDictionary(source["material"], "UIMesh material");
            string shaderName = String(materialData, "shader");
            Shader shader = shaderName == "UI/EffectDissolve"
                ? Shader.Find(RuntimeShaderName)
                : Shader.Find(shaderName);
            if (shader == null) throw new BuildFailedException("The official character creation dissolve shader is missing: " + shaderName);
            var material = new Material(shader) { name = String(materialData, "name") };
            foreach (KeyValuePair<string, object> texture in AsDictionary(materialData["textures"], "UIMesh textures"))
            {
                Dictionary<string, object> textureData = AsDictionary(texture.Value, "UIMesh texture binding");
                string path = FindTexture(String(textureData, "file"));
                Texture2D textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (textureAsset == null) throw new BuildFailedException("An official character creation dissolve texture is missing: " + path);
                if (material.HasProperty(texture.Key)) material.SetTexture(texture.Key, textureAsset);
                if (textureData.TryGetValue("scale", out object scale))
                {
                    List<object> values = AsList(scale, "texture scale");
                    if (values.Count == 2) material.SetTextureScale(texture.Key, new Vector2(Float(values[0]), Float(values[1])));
                }
                if (textureData.TryGetValue("offset", out object offset))
                {
                    List<object> values = AsList(offset, "texture offset");
                    if (values.Count == 2) material.SetTextureOffset(texture.Key, new Vector2(Float(values[0]), Float(values[1])));
                }
            }

            foreach (KeyValuePair<string, object> value in AsDictionary(materialData["floats"], "UIMesh material floats"))
                if (material.HasProperty(value.Key)) material.SetFloat(value.Key, Float(value.Value));
            foreach (KeyValuePair<string, object> value in AsDictionary(materialData["colors"], "UIMesh material colors"))
            {
                List<object> channels = AsList(value.Value, "UIMesh material color");
                if (channels.Count == 4 && material.HasProperty(value.Key))
                    material.SetColor(value.Key, new Color(Float(channels[0]), Float(channels[1]), Float(channels[2]), Float(channels[3])));
            }
            if (!material.HasProperty("_AnDissolve"))
                throw new BuildFailedException("The official character creation shader has no _AnDissolve property.");
            AssetDatabase.AddObjectToAsset(material, assets);

            List<object> tint = AsList(source["tint"], "UIMesh tint");
            if (tint.Count != 4) throw new BuildFailedException("The official character creation UIMesh tint is invalid.");
            return new CharacterCreationMeshAssetBinding
            {
                owner = owner,
                mesh = mesh,
                material = material,
                tint = new Color(Float(tint[0]), Float(tint[1]), Float(tint[2]), Float(tint[3]))
            };
        }

        private static CharacterCreationTransformOrderBinding BuildTransformOrder(Dictionary<string, object> root)
        {
            Dictionary<string, object> node = FindNode(root, RoleOwner);
            Dictionary<string, object> bindings = AsDictionary(node["original_bindings"], "role original bindings");
            Dictionary<string, object> reorderer = AsDictionary(bindings["TransformReorderer"], "TransformReorderer binding");
            var orders = new List<CharacterCreationTransformOrder>();
            foreach (object rawOrder in AsList(reorderer["mTransformOrderList"], "transform orders"))
            {
                Dictionary<string, object> order = AsDictionary(rawOrder, "transform order");
                orders.Add(new CharacterCreationTransformOrder
                {
                    paths = AsList(order["TransformOrderList"], "transform order paths")
                        .Select(value => AsString(value, "transform order path")).ToArray()
                });
            }
            return new CharacterCreationTransformOrderBinding
            {
                owner = RoleOwner,
                currentOrderIndex = (int)Float(reorderer["mCurOrderIndex"]),
                orders = orders.ToArray()
            };
        }

        private static string FindTexture(string uri)
        {
            string fileName = Path.GetFileNameWithoutExtension(uri);
            int suffixIndex = fileName.LastIndexOf('-');
            string baseName = suffixIndex > 0 && fileName.Length - suffixIndex == 11
                ? fileName.Substring(0, suffixIndex) : fileName;
            string[] matches = AssetDatabase.FindAssets(baseName + " t:Texture2D", new[] { PayloadRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(baseName, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
                throw new BuildFailedException("Could not resolve the unique official character creation texture: " + uri);
            return matches[0];
        }

        private static void WriteSourceManifest(string projectRoot, TextAsset source,
            List<AnimationSourceBinding> animations,
            List<KeyValuePair<string, Dictionary<string, object>>> meshes)
        {
            string uiManifestAbsolute = Path.Combine(projectRoot, UiManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string sourceManifestAbsolute = Path.Combine(projectRoot, SourceManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string uiManifest = File.ReadAllText(uiManifestAbsolute, Encoding.UTF8);
            Dictionary<string, object> uiSource = AsDictionary(CharacterCreationJson.Parse(uiManifest), "CreatePlayer UI source manifest");
            Dictionary<string, object> sourceEntry = AsDictionary(uiSource["source"], "CreatePlayer source entry");

            string[] officialCodePaths =
            {
                ".tools/source-packages/official-code/client-source/CreatePlayerPanel.cs",
                ".tools/source-packages/official-code/client-source/CreaterPlayerRunAnimation.cs",
                ".tools/source-packages/official-code/launch-source/UIMesh.cs",
                ".tools/source-packages/official-code/launch-source/UIMeshUtils.cs",
                ".tools/source-packages/official-code/framework-source/Framework.UI/TransformReorderer.cs",
                "scripts/official_shader_programs.py"
            };
            var codeInputs = officialCodePaths.Select(path => new PerformanceOutput
            {
                path = path,
                bytes = new FileInfo(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar))).Length,
                sha256 = HashFile(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar)))
            }).ToArray();

            var manifest = new PerformanceSourceManifest
            {
                manifest = "854_156383",
                apk_sha256 = PinnedApkSha256,
                source = new PerformanceSource
                {
                    data_path = SourcePayloadPath,
                    data_sha256 = HashBytes(Encoding.UTF8.GetBytes(source.text)),
                    ui_source_manifest = UiManifestRelativePath,
                    ui_source_manifest_sha256 = HashFile(uiManifestAbsolute),
                    source_bundle = String(sourceEntry, "bundle"),
                    source_bundle_sha256 = String(sourceEntry, "bundle_sha256"),
                    shader_bundle = "defaultpackage_unityshaders.bundle",
                    shader_bundle_md5 = ShaderBundleMd5,
                    shader_bundle_bytes = new FileInfo(Path.Combine(projectRoot, ShaderBundlePath.Replace('/', Path.DirectorySeparatorChar))).Length,
                    shader_bundle_sha256 = HashFile(Path.Combine(projectRoot, ShaderBundlePath.Replace('/', Path.DirectorySeparatorChar))),
                    shader_name = "UI/EffectDissolve",
                    shader_program_sha256 = ShaderProgramSha256,
                    runtime_shader = RuntimeShaderPath,
                    panel_prefab = PanelPrefabPath,
                    official_code = codeInputs.Select(value => value.path + " " + value.sha256).ToArray()
                },
                animations = animations.Select(value => value.owner + " :: " + value.alias).ToArray(),
                ui_meshes = meshes.Select(value => value.Key).ToArray(),
                outputs = BuildOutputHashes(projectRoot)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(sourceManifestAbsolute));
            string json = JsonUtility.ToJson(manifest, true) + "\n";
            if (!File.Exists(sourceManifestAbsolute) || !string.Equals(File.ReadAllText(sourceManifestAbsolute), json, StringComparison.Ordinal))
                File.WriteAllText(sourceManifestAbsolute, json, new UTF8Encoding(false));
        }

        private static PerformanceOutput[] BuildOutputHashes(string projectRoot)
        {
            var paths = new[]
            {
                ProjectAssetPath,
                ProjectAssetPath + ".meta",
                RuntimeShaderPath,
                RuntimeShaderPath + ".meta"
            };
            return paths.Select(path =>
            {
                string absolute = Path.GetFullPath(Path.Combine(projectRoot, "unity",
                    path.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(absolute)) throw new BuildFailedException("A generated character performance asset is missing: " + path);
                var file = new FileInfo(absolute);
                return new PerformanceOutput { path = path, bytes = file.Length, sha256 = HashFile(absolute) };
            }).ToArray();
        }

        private static void RemoveGeneratedSubassets(CharacterCreationPerformanceAssets assets)
        {
            string path = AssetDatabase.GetAssetPath(assets);
            foreach (UnityEngine.Object subasset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (subasset == assets) continue;
                AssetDatabase.RemoveObjectFromAsset(subasset);
                UnityEngine.Object.DestroyImmediate(subasset, true);
            }
        }

        private static string RelativePath(string owner, string path)
        {
            if (path == owner) return string.Empty;
            if (path.StartsWith(owner + "/", StringComparison.Ordinal)) return path.Substring(owner.Length + 1);
            if (!path.StartsWith("/", StringComparison.Ordinal)) return path;
            throw new BuildFailedException("Animation curve path is outside its owner: " + owner + " -> " + path);
        }

        private static Transform FindRelative(Transform root, string relativePath)
        {
            return string.IsNullOrEmpty(relativePath) ? root : root.Find(relativePath);
        }

        private static Dictionary<string, object> FindNode(Dictionary<string, object> node, string path)
        {
            if (String(node, "path") == path) return node;
            if (!node.TryGetValue("children", out object children)) return null;
            foreach (object child in AsList(children, "node children"))
            {
                Dictionary<string, object> found = FindNode(AsDictionary(child, "child node"), path);
                if (found != null) return found;
            }
            return null;
        }

        private static List<Vector3> ReadVector3List(object source)
        {
            return AsList(source, "vertices").Select(value =>
            {
                List<object> vector = AsList(value, "vertex");
                if (vector.Count != 3) throw new BuildFailedException("An official UIMesh vertex is invalid.");
                return new Vector3(Float(vector[0]), Float(vector[1]), Float(vector[2]));
            }).ToList();
        }

        private static List<Vector2> ReadVector2List(object source)
        {
            return AsList(source, "UV coordinates").Select(value =>
            {
                List<object> vector = AsList(value, "UV coordinate");
                if (vector.Count != 2) throw new BuildFailedException("An official UIMesh UV coordinate is invalid.");
                return new Vector2(Float(vector[0]), Float(vector[1]));
            }).ToList();
        }

        private static List<Color> ReadColorList(object source)
        {
            return AsList(source, "vertex colors").Select(value =>
            {
                List<object> color = AsList(value, "vertex color");
                if (color.Count != 4) throw new BuildFailedException("An official UIMesh vertex color is invalid.");
                return new Color(Float(color[0]), Float(color[1]), Float(color[2]), Float(color[3]));
            }).ToList();
        }

        private static List<int> ReadIntList(object source)
        {
            var result = new List<int>();
            foreach (object triangle in AsList(source, "triangles"))
                foreach (object index in AsList(triangle, "triangle indices"))
                    result.Add((int)Float(index));
            return result;
        }

        private static Dictionary<string, object> AsDictionary(object value, string label)
        {
            return value as Dictionary<string, object>
                ?? throw new BuildFailedException("Invalid character creation performance data: " + label);
        }

        private static List<object> AsList(object value, string label)
        {
            return value as List<object>
                ?? throw new BuildFailedException("Invalid character creation performance data: " + label);
        }

        private static string String(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object found) ? AsString(found, key) : string.Empty;
        }

        private static string AsString(object value, string label)
        {
            return value as string ?? throw new BuildFailedException("Invalid character creation performance text: " + label);
        }

        private static bool Boolean(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object found) && found is bool result && result;
        }

        private static float Float(object value)
        {
            if (value is double number) return (float)number;
            if (value is int integer) return integer;
            throw new BuildFailedException("Invalid numeric value in official character creation performance data.");
        }

        private static string HashFile(string path)
        {
            return HashBytes(File.ReadAllBytes(path));
        }

        private static string HashMd5File(string path)
        {
            using (MD5 md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
