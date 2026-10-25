using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleTimelineTools
    {
        private const string Root = "Assets/Official/Combat/Timeline";
        private const string Source = Root + "/Resources/Combat/battle-timeline.json";
        private const string Receipt = "../assets/reference/battle-hud/timeline-native-source-manifest.json";
        private static BattleTimelineData Read() => JsonUtility.FromJson<BattleTimelineData>(File.ReadAllText(Source));

        [MenuItem("Zjcs/Combat/Restore Official Scene Timeline")]
        public static void Restore()
        {
            var data = Read();
            foreach (string folder in new[] { "/Native", "/Native/Sprites", "/Native/Materials", "/Native/Animations" })
                Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
            var textures = new Dictionary<string, Texture2D>();
            foreach (BattleTimelineTexture source in data.textures)
            {
                if (Sha(File.ReadAllBytes(source.path)) != source.sha256)
                    throw new InvalidDataException("Official timeline texture source changed: " + source.path);
                var importer = AssetImporter.GetAtPath(source.path) as TextureImporter;
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(source.width, source.height));
                importer.filterMode = (FilterMode)source.filter;
                importer.wrapModeU = (TextureWrapMode)source.wrapU;
                importer.wrapModeV = (TextureWrapMode)source.wrapV;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
                textures.Add(source.key, AssetDatabase.LoadAssetAtPath<Texture2D>(source.path));
            }
            var sprites = new Dictionary<string, Sprite>();
            foreach (BattleTimelineSprite source in data.sprites)
            {
                Sprite sprite = Sprite.Create(textures[source.texture],
                    new Rect(source.rect[0], source.rect[1], source.rect[2], source.rect[3]),
                    V2(source.pivot), source.ppu, 0, SpriteMeshType.FullRect, V4(source.border));
                sprite.name = source.name;
                sprites.Add(source.key, Save(sprite, Root + "/Native/Sprites/" + source.name + ".asset"));
            }
            var materials = new Dictionary<string, Material>();
            foreach (BattleTimelineMaterial source in data.materials)
            {
                string original = data.shaders.Single(s => s.key == source.shader).name;
                string native = original == "Sprites/DefaultZWrite" ? "TimelineSpriteZWrite"
                    : original == "Universal Render Pipeline/2D/Sprite-Unlit-Default" ? "TimelineSprite"
                    : original == "UI/EffectDistort" ? "TimelineEffectDistort" : throw new NotSupportedException(original);
                // The source URP shader stores this keyword on its material, but
                // neither original GLES program consumes an external alpha texture.
                if (source.keywords.Length != 0 && !(original == "Universal Render Pipeline/2D/Sprite-Unlit-Default"
                    && source.keywords.SequenceEqual(new[] { "ETC1_EXTERNAL_ALPHA" })))
                    throw new NotSupportedException("Restore the original timeline shader keyword variant: " + string.Join(",", source.keywords));
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Presentation/Combat/Shaders/" + native + ".shader");
                if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException("Timeline shader is not usable: " + native);
                var material = new Material(shader) { name = source.name };
                material.shaderKeywords = source.keywords;
                foreach (BattleTimelineFloat value in source.floats) material.SetFloat(value.name, value.value);
                foreach (BattleTimelineColor value in source.colors) material.SetColor(value.name, C(value.value));
                foreach (BattleTimelineMaterialTexture value in source.textures)
                {
                    material.SetTexture(value.name, string.IsNullOrEmpty(value.texture) ? null : textures[value.texture]);
                    material.SetTextureScale(value.name, V2(value.scale));
                    material.SetTextureOffset(value.name, V2(value.offset));
                }
                material.renderQueue = source.queue;
                materials.Add(source.key, Save(material, Root + "/Native/Materials/" + source.name + ".mat"));
            }
            var prefabs = new Dictionary<string, GameObject>();
            foreach (BattleTimelinePrefab source in data.prefabs)
            {
                AnimatorController controller = source.hasController ? RestoreController(source.controller) : null;
                var nodes = new GameObject[source.nodes.Length];
                try
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        BattleTimelineNode n = source.nodes[i];
                        var go = new GameObject(n.name);
                        nodes[i] = go;
                        go.SetActive(false);
                        go.layer = n.layer;
                        go.transform.SetParent(n.parent < 0 ? null : nodes[n.parent].transform, false);
                        go.transform.localPosition = V3(n.position);
                        go.transform.localRotation = new Quaternion(n.rotation[0], n.rotation[1], n.rotation[2], n.rotation[3]);
                        go.transform.localScale = V3(n.scale);
                        if (n.hasRenderer)
                        {
                            Renderer renderer;
                            if (n.renderer.kind == "SpriteRenderer")
                            {
                                var sr = go.AddComponent<SpriteRenderer>();
                                sr.sprite = string.IsNullOrEmpty(n.renderer.sprite) ? null : sprites[n.renderer.sprite];
                                sr.color = C(n.renderer.color);
                                sr.drawMode = (SpriteDrawMode)n.renderer.drawMode;
                                sr.size = V2(n.renderer.size);
                                sr.flipX = n.renderer.flipX; sr.flipY = n.renderer.flipY;
                                sr.maskInteraction = (SpriteMaskInteraction)n.renderer.maskInteraction;
                                sr.spriteSortPoint = (SpriteSortPoint)n.renderer.spriteSortPoint;
                                renderer = sr;
                            }
                            else { go.AddComponent<MeshFilter>(); renderer = go.AddComponent<MeshRenderer>(); }
                            renderer.sharedMaterials = n.renderer.materials.Select(m => materials[m]).ToArray();
                            renderer.enabled = n.renderer.enabled;
                            renderer.sortingOrder = n.renderer.order;
                        }
                        if (n.hasCircle)
                        {
                            var circle = go.AddComponent<WorldCircleSpriteRenderer>();
                            circle.Radius = n.circle.radius; circle.Segments = n.circle.segments;
                            circle.UVOffset = V2(n.circle.offset); circle.Color = C(n.circle.color);
                            circle.Sprite = string.IsNullOrEmpty(n.circle.sprite) ? null : sprites[n.circle.sprite];
                        }
                    }
                    if (controller != null)
                    {
                        var animator = nodes[0].AddComponent<Animator>();
                        animator.runtimeAnimatorController = controller;
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    }
                    for (int i = nodes.Length - 1; i >= 0; i--) nodes[i].SetActive(source.nodes[i].active);
                    if (!string.IsNullOrEmpty(source.particlePrefab))
                    {
                        GameObject effect = AssetDatabase.LoadAssetAtPath<GameObject>(source.particlePrefab);
                        if (effect == null) throw new InvalidDataException("Restore the official timeline particle prefab first.");
                        Transform placeholder = nodes[0].transform.Find(effect.name);
                        if (placeholder == null) throw new InvalidDataException("Original timeline particle parent changed.");
                        Transform parent = placeholder.parent;
                        UnityEngine.Object.DestroyImmediate(placeholder.gameObject);
                        PrefabUtility.InstantiatePrefab(effect, parent);
                    }
                    prefabs.Add(source.key, PrefabUtility.SaveAsPrefabAsset(nodes[0], Root + "/Native/" + source.key + ".prefab"));
                }
                finally { if (nodes[0] != null) UnityEngine.Object.DestroyImmediate(nodes[0]); }
            }
            var catalog = ScriptableObject.CreateInstance<OfficialBattleTimelineAssets>();
            catalog.ScenePrefab = prefabs["round_scene"]; catalog.CardPrefab = prefabs["round_character_scene"];
            catalog.SceneSize = data.sceneSize; catalog.Z = data.z; catalog.DeadTime = data.deadTime;
            catalog.MinDistance = data.minDistance; catalog.MoveDuration = data.moveDuration;
            catalog.Anchor = data.anchor; catalog.CardBindings = data.cardBindings; catalog.SceneBindings = data.sceneBindings;
            catalog.SourceSha256 = Sha(File.ReadAllBytes(Source));
            Save(catalog, Root + "/Resources/Combat/OfficialBattleTimelineAssets.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            var outputs = Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal).Select(p => new Output { path = "unity/" + p.Replace('\\', '/'), sha256 = Sha(File.ReadAllBytes(p)) }).ToArray();
            File.WriteAllText(Receipt, JsonUtility.ToJson(new NativeReceipt
            {
                manifest = data.manifest, input_sha256 = Sha(File.ReadAllBytes(Source)),
                importer_sha256 = Sha(File.ReadAllBytes("Assets/Game/Editor/Combat/OfficialBattleTimelineTools.cs")), outputs = outputs
            }, true) + "\n");
            Debug.Log("Official scene timeline native assets restored: original prefabs, ten sprites, three card materials, four Animator clips and two particle systems (original disabled parent preserved).");
        }

        private static AnimatorController RestoreController(BattleTimelineController source)
        {
            AnimationClip[] clips = source.clips.Select(RestoreClip).ToArray();
            string path = Root + "/Native/Animations/" + source.name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.parameters = source.parameters.Select(p => new AnimatorControllerParameter
                { name = p.name, type = (AnimatorControllerParameterType)p.type }).ToArray();
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition old in machine.anyStateTransitions) UnityEngine.Object.DestroyImmediate(old, true);
            foreach (ChildAnimatorState old in machine.states) { machine.RemoveState(old.state); UnityEngine.Object.DestroyImmediate(old.state, true); }
            var states = source.states.Select(s =>
            {
                AnimatorState state = machine.AddState(s.name);
                state.motion = s.clip < 0 ? null : clips[s.clip]; state.speed = s.speed; state.writeDefaultValues = s.writeDefaultValues;
                return state;
            }).ToArray();
            machine.defaultState = states[source.defaultState];
            foreach (BattleFunctionEntryTransition t in source.anyTransitions) Configure(machine.AddAnyStateTransition(states[t.destination]), t);
            for (int i = 0; i < states.Length; i++)
                foreach (BattleFunctionEntryTransition t in source.states[i].transitions) Configure(states[i].AddTransition(states[t.destination]), t);
            EditorUtility.SetDirty(controller); EditorUtility.SetDirty(machine);
            return controller;
        }

        private static void Configure(AnimatorStateTransition target, BattleFunctionEntryTransition source)
        {
            target.duration = source.duration; target.hasFixedDuration = source.fixedDuration;
            target.exitTime = source.exitTime; target.hasExitTime = source.hasExitTime; target.offset = source.offset;
            target.canTransitionToSelf = source.canTransitionToSelf;
            target.interruptionSource = (TransitionInterruptionSource)source.interruptionSource;
            target.orderedInterruption = source.orderedInterruption;
            foreach (BattleFunctionEntryCondition c in source.conditions) target.AddCondition((AnimatorConditionMode)c.mode, c.threshold, c.parameter);
        }

        private static AnimationClip RestoreClip(BattleFunctionEntryClip source)
        {
            var clip = new AnimationClip { name = source.name, frameRate = source.frameRate };
            foreach (BattleFunctionEntryTrack track in source.tracks)
            {
                string property = Property(track.property);
                Type type = property == "m_IsActive" ? typeof(GameObject) : property.StartsWith("m_Color.", StringComparison.Ordinal)
                    ? typeof(SpriteRenderer) : typeof(Transform);
                var curve = new AnimationCurve(track.keys.Select(k => new Keyframe(k.time, k.value,
                    type == typeof(GameObject) ? float.PositiveInfinity : k.inTangent,
                    type == typeof(GameObject) ? float.PositiveInfinity : k.outTangent)).ToArray());
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(track.path, type, property), curve);
            }
            AnimationUtility.SetAnimationEvents(clip, source.events.Select(e => new AnimationEvent
            { time = e.time, functionName = e.functionName, stringParameter = e.data, floatParameter = e.floatParameter,
                intParameter = e.intParameter, messageOptions = (SendMessageOptions)e.messageOptions }).ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = source.duration; settings.loopTime = source.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return Save(clip, Root + "/Native/Animations/" + source.name + ".anim");
        }

        public static void Validate()
        {
            var data = Read();
            OfficialBattleTimelineAssets assets = OfficialBattleTimelineAssets.Load();
            if (assets.SourceSha256 != Sha(File.ReadAllBytes(Source))) throw new InvalidDataException("Timeline native assets differ from their source payload.");
            foreach (BattleTimelineSprite source in data.sprites)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Native/Sprites/" + source.name + ".asset");
                if (sprite == null || sprite.rect != new Rect(source.rect[0], source.rect[1], source.rect[2], source.rect[3])
                    || sprite.pixelsPerUnit != source.ppu || sprite.border != V4(source.border)
                    || (sprite.pivot - Vector2.Scale(V2(source.pivot), sprite.rect.size)).sqrMagnitude > 1e-8f)
                    throw new InvalidDataException("Original timeline Sprite geometry changed: " + source.name);
            }
            foreach (BattleTimelinePrefab source in data.prefabs)
            {
                GameObject prefab = source.key == "round_scene" ? assets.ScenePrefab : assets.CardPrefab;
                foreach (BattleTimelineNode n in source.nodes)
                {
                    string relative = n.path.Substring(source.nodes[0].path.Length).TrimStart('/');
                    Transform node = relative.Length == 0 ? prefab.transform : prefab.transform.Find(relative);
                    if (node == null || node.gameObject.activeSelf != n.active || (node.localPosition - V3(n.position)).sqrMagnitude > 1e-10f
                        || (node.localScale - V3(n.scale)).sqrMagnitude > 1e-10f || node.gameObject.layer != n.layer
                        || Quaternion.Angle(node.localRotation, new Quaternion(n.rotation[0], n.rotation[1], n.rotation[2], n.rotation[3])) > .001f)
                        throw new InvalidDataException("Original scene timeline hierarchy changed: " + n.path);
                    if (n.hasRenderer)
                    {
                        Renderer renderer = node.GetComponent<Renderer>();
                        if (renderer == null || renderer.sortingOrder != n.renderer.order || renderer.enabled != n.renderer.enabled
                            || renderer.sharedMaterials.Any(m => m == null || ShaderUtil.ShaderHasError(m.shader)))
                            throw new InvalidDataException("Original timeline renderer is invalid: " + n.path);
                        if (renderer is SpriteRenderer sr && (sr.color != C(n.renderer.color) || sr.size != V2(n.renderer.size)
                            || sr.drawMode != (SpriteDrawMode)n.renderer.drawMode || sr.flipX != n.renderer.flipX || sr.flipY != n.renderer.flipY
                            || sr.maskInteraction != (SpriteMaskInteraction)n.renderer.maskInteraction || sr.spriteSortPoint != (SpriteSortPoint)n.renderer.spriteSortPoint))
                            throw new InvalidDataException("Original timeline SpriteRenderer properties changed: " + n.path);
                    }
                    if (n.hasCircle)
                    {
                        var circle = node.GetComponent<WorldCircleSpriteRenderer>();
                        if (circle == null || circle.Radius != n.circle.radius || circle.Segments != n.circle.segments
                            || circle.UVOffset != V2(n.circle.offset) || circle.Color != C(n.circle.color))
                            throw new InvalidDataException("Original timeline circular mesh parameters changed: " + n.path);
                    }
                }
                if (!source.hasController) continue;
                if (!string.IsNullOrEmpty(source.particlePrefab)) ValidateParticles(prefab, source.particlePrefab);
                var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                if (machine.states.Length != source.controller.states.Length || machine.anyStateTransitions.Length != source.controller.anyTransitions.Length)
                    throw new InvalidDataException("Original timeline Animator state coverage changed.");
                if (controller.parameters.Length != source.controller.parameters.Length || controller.parameters.Where((p, i) =>
                    p.name != source.controller.parameters[i].name || (int)p.type != source.controller.parameters[i].type).Any()
                    || machine.defaultState.name != source.controller.states[source.controller.defaultState].name)
                    throw new InvalidDataException("Original timeline Animator parameters or initial state changed.");
                foreach (BattleFunctionEntryState stateSource in source.controller.states)
                {
                    AnimatorState state = machine.states.Single(s => s.state.name == stateSource.name).state;
                    if (state.speed != stateSource.speed || state.writeDefaultValues != stateSource.writeDefaultValues
                        || (state.motion == null ? stateSource.clip >= 0 : state.motion.name != source.controller.clips[stateSource.clip].name)
                        || state.transitions.Length != stateSource.transitions.Length)
                        throw new InvalidDataException("Original timeline Animator state changed: " + state.name);
                    for (int i = 0; i < state.transitions.Length; i++) ValidateTransition(state.transitions[i], stateSource.transitions[i], source.controller);
                }
                for (int i = 0; i < machine.anyStateTransitions.Length; i++)
                    ValidateTransition(machine.anyStateTransitions[i], source.controller.anyTransitions[i], source.controller);
                foreach (BattleFunctionEntryClip clipSource in source.controller.clips)
                {
                    AnimationClip clip = controller.animationClips.Single(c => c.name == clipSource.name);
                    AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                    if (clip.frameRate != clipSource.frameRate || settings.loopTime != clipSource.loop || Mathf.Abs(settings.stopTime - clipSource.duration) > 1e-6f
                        || AnimationUtility.GetCurveBindings(clip).Length != clipSource.tracks.Length)
                        throw new InvalidDataException("Original timeline animation coverage or timing changed: " + clip.name);
                    foreach (BattleFunctionEntryTrack track in clipSource.tracks)
                    {
                        EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip).Single(b => b.path == track.path && b.propertyName == Property(track.property));
                        if (binding.isDiscreteCurve) throw new InvalidDataException("Original timeline float curve became discrete.");
                        Keyframe[] keys = AnimationUtility.GetEditorCurve(clip, binding).keys;
                        bool active = track.property == "m_IsActive";
                        if (keys.Length != track.keys.Length || keys.Where((k, i) => k.time != track.keys[i].time || k.value != track.keys[i].value
                            || !SameTangent(k.inTangent, active ? float.PositiveInfinity : track.keys[i].inTangent)
                            || !SameTangent(k.outTangent, active ? float.PositiveInfinity : track.keys[i].outTangent)).Any())
                            throw new InvalidDataException("Original timeline animation keys changed.");
                    }
                }
            }
            Debug.Log("Official scene timeline validation passed: hierarchy, Sprite geometry, renderer/circular mesh parameters and complete Animator transitions/curves.");
        }

        private static bool SameTangent(float a, float b) => a == b || Mathf.Abs(a - b) < 1e-6f;
        private static void ValidateParticles(GameObject card, string path)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Transform effect = card.transform.Find(source.name);
            if (effect == null || effect.Find("UIParticle").gameObject.activeSelf)
                throw new InvalidDataException("Original disabled scene particle branch changed.");
            ParticleSystem[] originals = source.GetComponentsInChildren<ParticleSystem>(true);
            if (originals.Length != 2 || effect.GetComponentsInChildren<ParticleSystem>(true).Length != 2)
                throw new InvalidDataException("Original timeline particle coverage changed.");
            foreach (ParticleSystem particle in originals)
            {
                string relative = AnimationUtility.CalculateTransformPath(particle.transform, source.transform);
                Transform actual = effect.Find(relative);
                foreach (Type type in new[] { typeof(ParticleSystem), typeof(ParticleSystemRenderer) })
                {
                    string Normalize(Component c) => Regex.Replace(EditorJsonUtility.ToJson(c), "\"instanceID\":-?[0-9]+", "\"instanceID\":0");
                    if (actual == null || actual.GetComponent(type) == null || Normalize(particle.GetComponent(type)) != Normalize(actual.GetComponent(type)))
                        throw new InvalidDataException("Original native particle serialization changed: " + relative + " / " + type.Name);
                }
                Renderer renderer = actual.GetComponent<ParticleSystemRenderer>();
                if (renderer.sharedMaterial == null || ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)
                    || renderer.sharedMaterial.shader.name != "Zjcs/Combat/TimelineParticleMainTex")
                    throw new InvalidDataException("Original timeline particle shader is not restored: " + relative);
            }
        }
        private static void ValidateTransition(AnimatorStateTransition t, BattleFunctionEntryTransition s, BattleTimelineController controller)
        {
            if (t.destinationState.name != controller.states[s.destination].name || t.duration != s.duration || t.hasFixedDuration != s.fixedDuration
                || t.exitTime != s.exitTime || t.hasExitTime != s.hasExitTime || t.offset != s.offset || t.canTransitionToSelf != s.canTransitionToSelf
                || (int)t.interruptionSource != s.interruptionSource || t.orderedInterruption != s.orderedInterruption
                || t.conditions.Length != s.conditions.Length || t.conditions.Where((c, i) => c.parameter != s.conditions[i].parameter
                    || c.threshold != s.conditions[i].threshold || (int)c.mode != s.conditions[i].mode).Any())
                throw new InvalidDataException("Original timeline Animator transition changed: " + t.destinationState.name);
        }

        private static string Property(string p) => p.StartsWith("position.", StringComparison.Ordinal) ? "m_LocalPosition." + p.Substring(9)
            : p.StartsWith("scale.", StringComparison.Ordinal) ? "m_LocalScale." + p.Substring(6) : p;
        private static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            T old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (old == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, old); EditorUtility.SetDirty(old); UnityEngine.Object.DestroyImmediate(value); return old;
        }
        private static Vector2 V2(float[] v) => new Vector2(v[0], v[1]);
        private static Vector3 V3(float[] v) => new Vector3(v[0], v[1], v[2]);
        private static Vector4 V4(float[] v) => new Vector4(v[0], v[1], v[2], v[3]);
        private static Color C(float[] v) => new Color(v[0], v[1], v[2], v[3]);
        private static string Sha(byte[] b) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
        [Serializable] private sealed class Output { public string path, sha256; }
        [Serializable] private sealed class NativeReceipt { public string manifest, input_sha256, importer_sha256; public Output[] outputs; }
    }
}
