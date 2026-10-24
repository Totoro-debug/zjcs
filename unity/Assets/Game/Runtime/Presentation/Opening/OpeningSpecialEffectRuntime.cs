using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    public sealed class OpeningSpecialEffectRuntime : MonoBehaviour
    {
        private sealed class CameraMove
        {
            public int Sequence;
            public int Group;
            public float Start;
            public float Duration;
            public float Speed;
            public Vector3 Offset;
            public AnimationCurve X;
            public AnimationCurve Y;
            public AnimationCurve Z;
            public Quaternion SkillRotation;
            public bool RotateBySkill;
            public bool Flip;
            public Vector3 Value;
        }

        private sealed class HitMove
        {
            public float Start;
            public float Duration;
            public float Speed;
            public bool Resume;
            public bool KeywordValue;
            public string Keyword;
            public string ColorProperty;
            public string FloatProperty;
            public Gradient Color;
            public AnimationCurve Float;
        }

        private sealed class HitTarget
        {
            public Renderer[] Renderers;
            public Material[][] Original;
            public Material[][] Adapted;
            public readonly List<HitMove> Moves = new List<HitMove>();
        }

        private sealed class FollowEffect
        {
            public GameObject Root;
            public Transform Content;
            public Spine.Unity.SkeletonAnimation Target;
            public Spine.Unity.BoneFollower BoneFollower;
            public Spine.Slot Slot;
            public Spine.Attachment Attachment;
            public string AttachmentName;
            public bool FollowEnable;
            public bool FollowShowHideNode;
            public bool FollowZSpacing;
            public bool ZFront;
            public float Start;
            public float Duration;
            public float FollowDuration;
            public float Speed;
            public bool Started;
        }

        private const string HitShaderName = "Zjcs/Opening/SpineHit";
        private readonly List<CameraMove> cameraMoves = new List<CameraMove>();
        private readonly Dictionary<Spine.Unity.SkeletonAnimation, HitTarget> hitTargets =
            new Dictionary<Spine.Unity.SkeletonAnimation, HitTarget>();
        private readonly List<FollowEffect> followEffects = new List<FollowEffect>();
        private Camera stageCamera;
        private Vector3 cameraBasePosition;
        private float cameraBaseSize;
        private int sequence;
        private Shader hitShader;

        public static OpeningSpecialEffectRuntime GetOrCreate(Transform parent, Camera camera)
        {
            if (parent == null) return null;
            OpeningSpecialEffectRuntime runtime = parent.GetComponent<OpeningSpecialEffectRuntime>();
            if (runtime == null) runtime = parent.gameObject.AddComponent<OpeningSpecialEffectRuntime>();
            runtime.BindCamera(camera);
            return runtime;
        }

        public bool Play(Dictionary<string, object> entry, Spine.Unity.SkeletonAnimation source,
            Spine.Unity.SkeletonAnimation target, Vector3 sourcePosition, Vector3 targetPosition,
            Quaternion skillRotation, OpeningSkillEffectPlayer assets, Material specialMaterial, float eventSpeed)
        {
            if (!entry.TryGetValue("source_event", out object rawSource)
                || !(rawSource is Dictionary<string, object> sourceEvent)
                || !sourceEvent.TryGetValue("fields", out object rawFields)
                || !(rawFields is Dictionary<string, object> fields)) return false;

            string playable = OpeningJson.String(sourceEvent, "playable");
            if (playable == "MyPlayableFollowSpinePart")
                return PlayFollowSpinePart(entry, sourceEvent, fields, source, assets, eventSpeed);
            if (playable == "MyPlayableGroundEffect")
                return OpeningGroundEffectRuntime.Play(transform, entry, fields, eventSpeed);

            if (fields.ContainsKey("CameraGroup"))
                return PlayCameraMove(entry, fields, source, sourcePosition, targetPosition, skillRotation, eventSpeed);
            if (fields.ContainsKey("Keyword"))
                return PlayMaterialProperty(entry, fields, target, eventSpeed);
            if (fields.ContainsKey("mStartAttachPos"))
                return PlayGrapplingLine(entry, sourceEvent, fields, source, target, sourcePosition, targetPosition,
                    assets, specialMaterial, eventSpeed);
            return false;
        }

        private void BindCamera(Camera camera)
        {
            if (camera == null || stageCamera == camera) return;
            stageCamera = camera;
            cameraBasePosition = camera.transform.position;
            cameraBaseSize = camera.orthographicSize;
        }

        private bool PlayCameraMove(Dictionary<string, object> entry, Dictionary<string, object> fields,
            Spine.Unity.SkeletonAnimation source, Vector3 sourcePosition, Vector3 targetPosition,
            Quaternion skillRotation, float eventSpeed)
        {
            if (stageCamera == null) return false;
            float speed = Mathf.Clamp(eventSpeed, 0.01f, 3f);
            Vector3 offset = Vector3Value(fields, "Offset");
            bool flip = OpeningJson.Bool(fields, "FlipBySourceX") && source != null
                && source.transform.localScale.x < 0f;
            if (OpeningJson.Bool(fields, "FlipByTargetY") && targetPosition.y < sourcePosition.y) flip = !flip;
            float duration = (float)OpeningJson.Number(fields, "Duration", 0f);
            var motion = new CameraMove
            {
                Sequence = ++sequence,
                Group = Mathf.Clamp(OpeningJson.Int(fields, "CameraGroup"), 0, 3),
                Start = Time.time + Mathf.Max(0f, (float)OpeningJson.Number(entry, "eventTime")) / speed,
                Duration = duration,
                Speed = speed,
                Offset = offset,
                X = ReadCurve(fields, "X", 0f),
                Y = ReadCurve(fields, "Y", 0f),
                Z = ReadCurve(fields, "Z", 0f),
                SkillRotation = skillRotation,
                RotateBySkill = OpeningJson.Bool(fields, "RotBySkill"),
                Flip = flip,
            };
            cameraMoves.Add(motion);
            return true;
        }

        private bool PlayMaterialProperty(Dictionary<string, object> entry, Dictionary<string, object> fields,
            Spine.Unity.SkeletonAnimation target, float eventSpeed)
        {
            if (target == null) return false;
            hitShader ??= Shader.Find(HitShaderName);
            if (hitShader == null) return false;
            if (!hitTargets.TryGetValue(target, out HitTarget state))
            {
                state = CreateHitTarget(target);
                if (state == null) return false;
                hitTargets.Add(target, state);
            }
            Dictionary<string, object> colorSource = OpeningJson.Object(fields["ColorCurve"], "hit color curve");
            Dictionary<string, object> floatSource = OpeningJson.Object(fields["FloatCurve"], "hit float curve");
            float duration = (float)OpeningJson.Number(fields, "Duration", 0f);
            state.Moves.Add(new HitMove
            {
                Start = Time.time + Mathf.Max(0f, (float)OpeningJson.Number(entry, "eventTime"))
                    / Mathf.Clamp(eventSpeed, 0.01f, 3f),
                Duration = duration,
                Speed = Mathf.Clamp(eventSpeed, 0.01f, 3f),
                Resume = OpeningJson.Bool(fields, "NeedResume", true),
                KeywordValue = OpeningJson.Bool(fields, "KeywordBoolValue"),
                Keyword = OpeningJson.String(fields, "Keyword"),
                ColorProperty = OpeningJson.String(fields, "ColorPropertyName"),
                FloatProperty = OpeningJson.String(fields, "FloatPropertyName"),
                Color = ReadGradient(colorSource),
                Float = ReadCurve(floatSource, 0f),
            });
            return true;
        }

        private HitTarget CreateHitTarget(Spine.Unity.SkeletonAnimation target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            var state = new HitTarget
            {
                Renderers = renderers,
                Original = new Material[renderers.Length][],
                Adapted = new Material[renderers.Length][],
            };
            bool found = false;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null) continue;
                Material[] originals = renderer.sharedMaterials;
                var adapted = new Material[originals.Length];
                state.Original[rendererIndex] = originals;
                state.Adapted[rendererIndex] = adapted;
                for (int materialIndex = 0; materialIndex < originals.Length; materialIndex++)
                {
                    Material original = originals[materialIndex];
                    if (original == null) continue;
                    var material = new Material(hitShader) { name = original.name + "_OpeningHit" };
                    if (original.HasProperty("_MainTex"))
                    {
                        material.SetTexture("_MainTex", original.GetTexture("_MainTex"));
                        material.SetTextureScale("_MainTex", original.GetTextureScale("_MainTex"));
                        material.SetTextureOffset("_MainTex", original.GetTextureOffset("_MainTex"));
                    }
                    if (original.HasProperty("_Color")) material.SetColor("_Color", original.GetColor("_Color"));
                    if (original.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", original.GetFloat("_SrcBlend"));
                    if (original.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", original.GetFloat("_DstBlend"));
                    if (original.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", original.GetFloat("_ZWrite"));
                    if (original.HasProperty("_ZTest")) material.SetFloat("_ZTest", original.GetFloat("_ZTest"));
                    if (original.HasProperty("_Cull")) material.SetFloat("_CullMode", original.GetFloat("_Cull"));
                    adapted[materialIndex] = material;
                    found = true;
                }
                renderer.sharedMaterials = adapted;
            }
            return found ? state : null;
        }

        private void Update()
        {
            UpdateCameraMoves();
            UpdateHitEffects();
            UpdateFollowEffects();
        }

        private bool PlayFollowSpinePart(Dictionary<string, object> entry, Dictionary<string, object> sourceEvent,
            Dictionary<string, object> fields, Spine.Unity.SkeletonAnimation target,
            OpeningSkillEffectPlayer assets, float eventSpeed)
        {
            if (target == null || target.Skeleton == null) return false;
            Dictionary<string, object> follower = OpeningJson.Object(sourceEvent["follower"], "attachment follower");
            string boneName = OpeningJson.String(follower, "BoneName");
            string slotName = OpeningJson.String(follower, "SlotName");
            string attachmentName = OpeningJson.String(follower, "AttachmentName");
            Spine.Slot slot = null;
            Spine.Attachment attachment = null;
            if (!string.IsNullOrEmpty(slotName) && !string.IsNullOrEmpty(attachmentName))
            {
                slot = target.Skeleton.FindSlot(slotName);
                attachment = target.Skeleton.GetAttachment(slotName, attachmentName);
                if (slot == null || attachment == null)
                {
                    Debug.LogWarning("开场骨骼挂件找不到槽位或附件: " + slotName + "/" + attachmentName);
                    return false;
                }
                if (string.IsNullOrEmpty(boneName)) boneName = slot.Bone.Data.Name;
            }
            if (string.IsNullOrEmpty(boneName) || target.Skeleton.FindBone(boneName) == null)
            {
                Debug.LogWarning("开场骨骼挂件找不到骨骼: " + boneName);
                return false;
            }

            float speed = Mathf.Clamp(eventSpeed, 0.01f, 3f);
            var root = new GameObject("OpeningFollowSpinePart_" + OpeningJson.Int(entry, "classId"));
            root.transform.SetParent(target.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.SetActive(false);

            var boneFollower = root.AddComponent<Spine.Unity.BoneFollower>();
            boneFollower.initializeOnAwake = false;
            boneFollower.skeletonRenderer = target;
            boneFollower.boneName = boneName;
            boneFollower.followXYPosition = true;
            boneFollower.followZPosition = true;
            boneFollower.followAttachmentZSpacing = false;
            boneFollower.followBoneRotation = OpeningJson.Bool(follower, "FollowRotation", true);
            boneFollower.followSkeletonFlip = OpeningJson.Bool(follower, "FollowSkeletonTransformFlipX", true);
            boneFollower.followLocalScale = OpeningJson.Bool(follower, "FollowLocalScale", true);
            boneFollower.Initialize();

            var content = new GameObject("FollowedPrefabRoots");
            content.transform.SetParent(root.transform, false);
            var effect = new FollowEffect
            {
                Root = root,
                Content = content.transform,
                Target = target,
                BoneFollower = boneFollower,
                Slot = slot,
                Attachment = attachment,
                AttachmentName = attachmentName,
                FollowEnable = OpeningJson.Bool(fields, "FollowEnable"),
                FollowShowHideNode = OpeningJson.Bool(fields, "IsFollowShowHideNode"),
                FollowZSpacing = OpeningJson.Bool(follower, "FollowZSpacing", true),
                ZFront = OpeningJson.Bool(follower, "ZFront", true),
                Start = Time.time + Mathf.Max(0f, (float)OpeningJson.Number(entry, "eventTime")) / speed,
                Duration = (float)OpeningJson.Number(fields, "Duration", -1f),
                FollowDuration = (float)OpeningJson.Number(fields, "FollowDuration", -1f),
                Speed = speed,
            };
            if (follower.TryGetValue("prefabs", out object rawPrefabs)
                && rawPrefabs is List<object> prefabs)
            {
                foreach (object rawPrefab in prefabs)
                    CreateFollowPrefab(OpeningJson.Object(rawPrefab, "follow-spine prefab"), effect.Content, assets,
                        speed);
            }
            followEffects.Add(effect);
            return true;
        }

        private static void CreateFollowPrefab(Dictionary<string, object> prefab, Transform parent,
            OpeningSkillEffectPlayer assets, float eventSpeed)
        {
            List<object> rawNodes = OpeningJson.Array(prefab["nodes"], "follow-spine prefab nodes");
            var nodes = new Transform[rawNodes.Count];
            for (int index = 0; index < rawNodes.Count; index++)
            {
                Dictionary<string, object> node = OpeningJson.Object(rawNodes[index], "follow-spine node");
                int parentIndex = OpeningJson.Int(node, "parent", -1);
                Transform nodeParent = parentIndex >= 0 && parentIndex < index ? nodes[parentIndex] : parent;
                var gameObject = new GameObject(OpeningJson.String(node, "name", "FollowSpineNode"));
                gameObject.transform.SetParent(nodeParent, false);
                gameObject.transform.localPosition = Vector3Value(node, "position");
                gameObject.transform.localRotation = QuaternionValue(node, "rotation");
                gameObject.transform.localScale = Vector3Value(node, "scale", Vector3.one);
                gameObject.SetActive(OpeningJson.Bool(node, "active", true));
                nodes[index] = gameObject.transform;
                foreach (object rawParticle in OpeningJson.Array(node["particles"], "follow-spine particles"))
                {
                    Dictionary<string, object> particle = OpeningJson.Object(rawParticle, "follow-spine particle");
                    GameObject instance = assets.CreateSpecialParticle(particle, gameObject.transform, eventSpeed);
                    if (instance != null)
                    {
                        ParticleSystemRenderer renderer = instance.GetComponent<ParticleSystemRenderer>();
                        if (renderer != null) renderer.enabled = OpeningJson.Bool(particle, "renderer_enabled", true);
                        if (!OpeningJson.Bool(particle, "active", true)) instance.SetActive(false);
                    }
                }
            }
        }

        private void UpdateFollowEffects()
        {
            for (int index = followEffects.Count - 1; index >= 0; index--)
            {
                FollowEffect effect = followEffects[index];
                if (effect.Root == null || effect.Target == null || effect.Target.Skeleton == null)
                {
                    if (effect.Root != null) UnityEngine.Object.Destroy(effect.Root);
                    followEffects.RemoveAt(index);
                    continue;
                }
                if (Time.time < effect.Start) continue;
                float elapsed = (Time.time - effect.Start) * effect.Speed;
                if (effect.Duration >= 0f && elapsed >= effect.Duration)
                {
                    UnityEngine.Object.Destroy(effect.Root);
                    followEffects.RemoveAt(index);
                    continue;
                }
                bool following = effect.FollowDuration < 0f || elapsed < effect.FollowDuration;
                effect.BoneFollower.enabled = following;
                if (effect.FollowZSpacing && following && effect.Slot != null)
                    ApplyAttachmentDepth(effect);
                bool active = !effect.FollowEnable || effect.Target.gameObject.activeInHierarchy;
                if (effect.Slot != null && effect.Attachment != null)
                    active &= effect.Slot.Attachment == effect.Attachment;
                bool wasActive = effect.Root.activeSelf;
                effect.Root.SetActive(active);
                if (active && !wasActive)
                {
                    foreach (ParticleSystem system in effect.Root.GetComponentsInChildren<ParticleSystem>(false))
                        system.Play(true);
                    effect.Started = true;
                }
            }
        }

        private static void ApplyAttachmentDepth(FollowEffect effect)
        {
            if (effect.Content == null || effect.Slot == null) return;
            Spine.ExposedList<Spine.Slot> drawOrder = effect.Target.Skeleton.DrawOrder;
            int slotIndex = -1;
            for (int index = 0; index < drawOrder.Count; index++)
                if (drawOrder.Items[index] == effect.Slot)
                {
                    slotIndex = index;
                    break;
                }
            if (slotIndex < 0) return;
            float spacing = effect.Target.zSpacing;
            effect.Content.localPosition = new Vector3(0f, 0f,
                spacing * slotIndex + (effect.ZFront ? spacing * 0.5f : -spacing * 0.5f));
        }

        private static Vector3 Vector3Value(Dictionary<string, object> source, string key, Vector3 fallback = default)
        {
            if (!source.TryGetValue(key, out object raw) || !(raw is Dictionary<string, object> value)) return fallback;
            return new Vector3((float)OpeningJson.Number(value, "x", fallback.x),
                (float)OpeningJson.Number(value, "y", fallback.y),
                (float)OpeningJson.Number(value, "z", fallback.z));
        }

        private static Quaternion QuaternionValue(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object raw) || !(raw is Dictionary<string, object> value))
                return Quaternion.identity;
            return new Quaternion((float)OpeningJson.Number(value, "x"), (float)OpeningJson.Number(value, "y"),
                (float)OpeningJson.Number(value, "z"), (float)OpeningJson.Number(value, "w", 1f));
        }

        private void UpdateCameraMoves()
        {
            float now = Time.time;
            for (int index = cameraMoves.Count - 1; index >= 0; index--)
            {
                CameraMove motion = cameraMoves[index];
                if (now < motion.Start) continue;
                float elapsed = (now - motion.Start) * motion.Speed;
                if (motion.Duration >= 0f && motion.Duration > 0f && elapsed > motion.Duration)
                {
                    cameraMoves.RemoveAt(index);
                    continue;
                }
                float normalized = motion.Duration > 0f ? Mathf.Clamp01(elapsed / motion.Duration) : 1f;
                Vector3 offset = new Vector3(motion.X.Evaluate(normalized) * motion.Offset.x,
                    motion.Y.Evaluate(normalized) * motion.Offset.y,
                    motion.Z.Evaluate(normalized) * motion.Offset.z);
                if (motion.RotateBySkill)
                {
                    float z = offset.z;
                    offset = motion.SkillRotation * offset;
                    offset.z = z;
                }
                if (motion.Flip) offset.x = -offset.x;
                motion.Value = offset;
            }

            Vector3 total = Vector3.zero;
            for (int group = 0; group < 3; group++)
            {
                CameraMove latest = null;
                foreach (CameraMove motion in cameraMoves)
                    if (motion.Group == group && now >= motion.Start
                        && (latest == null || motion.Sequence > latest.Sequence)) latest = motion;
                if (latest != null) total += latest.Value;
            }
            foreach (CameraMove motion in cameraMoves)
                if (motion.Group == 3 && now >= motion.Start) total += motion.Value;
            if (stageCamera == null) return;
            stageCamera.transform.position = cameraBasePosition + new Vector3(total.x, total.y, 0f);
            if (stageCamera.orthographic) stageCamera.orthographicSize = Mathf.Max(0.05f, cameraBaseSize * (1f + total.z));
        }

        private void UpdateHitEffects()
        {
            var completed = new List<Spine.Unity.SkeletonAnimation>();
            foreach (KeyValuePair<Spine.Unity.SkeletonAnimation, HitTarget> pair in hitTargets)
            {
                HitTarget state = pair.Value;
                HitMove active = null;
                for (int index = state.Moves.Count - 1; index >= 0; index--)
                {
                    HitMove move = state.Moves[index];
                    float elapsed = (Time.time - move.Start) * move.Speed;
                    if (Time.time < move.Start) continue;
                    if (move.Resume && move.Duration >= 0f && elapsed >= move.Duration)
                    {
                        state.Moves.RemoveAt(index);
                        continue;
                    }
                    if (active == null) active = move;
                }
                if (active == null)
                {
                    RestoreHitTarget(state);
                    completed.Add(pair.Key);
                    continue;
                }
                float time = active.Duration > 0f
                    ? Mathf.Clamp01((Time.time - active.Start) * active.Speed / active.Duration) : 1f;
                Color color = active.Color.Evaluate(time);
                float blend = active.Float.Evaluate(time);
                foreach (Material[] materials in state.Adapted)
                {
                    if (materials == null) continue;
                    foreach (Material material in materials)
                    {
                        if (material == null) continue;
                        if (!string.IsNullOrEmpty(active.ColorProperty) && material.HasProperty(active.ColorProperty))
                            material.SetColor(active.ColorProperty, color);
                        if (!string.IsNullOrEmpty(active.FloatProperty) && material.HasProperty(active.FloatProperty))
                            material.SetFloat(active.FloatProperty, blend);
                        if (!string.IsNullOrEmpty(active.Keyword) && material.HasProperty(active.Keyword))
                            material.SetFloat(active.Keyword, active.KeywordValue ? 1f : 0f);
                    }
                }
            }
            foreach (Spine.Unity.SkeletonAnimation target in completed) hitTargets.Remove(target);
        }

        private static void RestoreHitTarget(HitTarget state)
        {
            for (int index = 0; index < state.Renderers.Length; index++)
            {
                Renderer renderer = state.Renderers[index];
                if (renderer != null && state.Original[index] != null) renderer.sharedMaterials = state.Original[index];
                if (state.Adapted[index] == null) continue;
                foreach (Material material in state.Adapted[index])
                    if (material != null) UnityEngine.Object.Destroy(material);
            }
        }

        private bool PlayGrapplingLine(Dictionary<string, object> entry, Dictionary<string, object> sourceEvent,
            Dictionary<string, object> fields, Spine.Unity.SkeletonAnimation source,
            Spine.Unity.SkeletonAnimation target, Vector3 sourcePosition, Vector3 targetPosition,
            OpeningSkillEffectPlayer assets, Material specialMaterial, float eventSpeed)
        {
            return OpeningGrapplingLineEffect.Play(this, entry, sourceEvent, fields, source, target,
                sourcePosition, targetPosition, specialMaterial, eventSpeed);
        }

        private static AnimationCurve ReadCurve(Dictionary<string, object> source, string key, float fallback)
        {
            if (!source.TryGetValue(key, out object raw) || !(raw is Dictionary<string, object> data))
                return AnimationCurve.Constant(0f, 1f, fallback);
            if (!data.TryGetValue("m_Curve", out object rawKeys) || !(rawKeys is List<object> values)
                || values.Count == 0) return AnimationCurve.Constant(0f, 1f, fallback);
            var keys = new Keyframe[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                Dictionary<string, object> item = OpeningJson.Object(values[index], "animation curve key");
                var frame = new Keyframe((float)OpeningJson.Number(item, "time"),
                    (float)OpeningJson.Number(item, "value"), (float)OpeningJson.Number(item, "inSlope"),
                    (float)OpeningJson.Number(item, "outSlope"));
                frame.weightedMode = (WeightedMode)OpeningJson.Int(item, "weightedMode");
                frame.inWeight = (float)OpeningJson.Number(item, "inWeight", 1f / 3f);
                frame.outWeight = (float)OpeningJson.Number(item, "outWeight", 1f / 3f);
                keys[index] = frame;
            }
            var curve = new AnimationCurve(keys)
            {
                preWrapMode = (WrapMode)OpeningJson.Int(data, "m_PreInfinity", 2),
                postWrapMode = (WrapMode)OpeningJson.Int(data, "m_PostInfinity", 2),
            };
            return curve;
        }

        private static AnimationCurve ReadCurve(Dictionary<string, object> source, float fallback)
        {
            return ReadCurve(new Dictionary<string, object> { { "curve", source } }, "curve", fallback);
        }

        private static Gradient ReadGradient(Dictionary<string, object> source)
        {
            int colorCount = Mathf.Clamp(OpeningJson.Int(source, "m_NumColorKeys"), 1, 8);
            int alphaCount = Mathf.Clamp(OpeningJson.Int(source, "m_NumAlphaKeys"), 1, 8);
            var colors = new GradientColorKey[colorCount];
            var alphas = new GradientAlphaKey[alphaCount];
            for (int index = 0; index < colorCount; index++)
            {
                Dictionary<string, object> item = OpeningJson.Object(source["key" + index], "gradient color");
                colors[index] = new GradientColorKey(new Color((float)OpeningJson.Number(item, "r"),
                    (float)OpeningJson.Number(item, "g"), (float)OpeningJson.Number(item, "b")),
                    Mathf.Clamp01((float)OpeningJson.Number(source, "ctime" + index) / 65535f));
            }
            for (int index = 0; index < alphaCount; index++)
                alphas[index] = new GradientAlphaKey((float)OpeningJson.Number(
                    OpeningJson.Object(source["key" + index], "gradient alpha"), "a"),
                    Mathf.Clamp01((float)OpeningJson.Number(source, "atime" + index) / 65535f));
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            gradient.mode = OpeningJson.Int(source, "m_Mode") == 1 ? GradientMode.Fixed : GradientMode.Blend;
            return gradient;
        }

        private static Vector3 Vector3Value(Dictionary<string, object> source, string key)
        {
            Dictionary<string, object> value = OpeningJson.Object(source[key], key);
            return new Vector3((float)OpeningJson.Number(value, "x"), (float)OpeningJson.Number(value, "y"),
                (float)OpeningJson.Number(value, "z"));
        }

        private void OnDestroy()
        {
            foreach (HitTarget state in hitTargets.Values) RestoreHitTarget(state);
            hitTargets.Clear();
            cameraMoves.Clear();
            if (stageCamera != null)
            {
                stageCamera.transform.position = cameraBasePosition;
                if (stageCamera.orthographic) stageCamera.orthographicSize = cameraBaseSize;
            }
        }
    }
}
