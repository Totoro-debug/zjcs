using System;
using System.IO;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14
{
    // Independently evaluate the original model's affine transforms and cubic
    // tracks. Original prefab transforms, renderers and materials remain native.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class NativeMap14ButterflyMesh : MonoBehaviour
    {
        public NativeMap14ButterflyData skeletonDataAsset;
        public string initialSkinName;
        public bool initialFlipX, initialFlipY;
        public int updateWhenInvisible, updateTiming;
        public bool unscaledTime, loop;
        [SerializeField] private string _animationName;
        public float timeScale = 1;
        private NativeMap14ButterflyData.Model model;
        private NativeMap14ButterflyData.Skin skin;
        private NativeMap14ButterflyData.Clip clip;
        private NativeMap14ButterflyData.Bone[] pose;
        private Matrix4x4[] matrices;
        private Vector3[] positions;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private float elapsed;

        public Mesh DeformedMesh => mesh;
        public float Elapsed => elapsed;
        public string AnimationName => _animationName;
        public Matrix4x4[] BoneMatrices => matrices;

        private void Awake()
        {
            if (!skeletonDataAsset || initialFlipX || initialFlipY || unscaledTime || !loop || timeScale != 1 ||
                updateTiming != 1 || (updateWhenInvisible != 0 && updateWhenInvisible != 3))
                throw new InvalidDataException("Original Map14 butterfly instance settings differ.");
            model = skeletonDataAsset.Load();
            string selectedSkin = string.IsNullOrEmpty(initialSkinName) ? "default" : initialSkinName;
            foreach (var value in model.skins) if (value.name == selectedSkin) skin = value;
            foreach (var value in model.animations) if (value.name == _animationName) clip = value;
            if (skin == null || clip == null || clip.duration <= 0 || skin.vertices.Length != skin.uv.Length ||
                skin.vertices.Length != skin.vertexBones.Length)
                throw new InvalidDataException("Original Map14 butterfly skin/animation/geometry is unbound.");
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer.sharedMaterials.Length != 1 || meshRenderer.sharedMaterial != skeletonDataAsset.Material)
                throw new InvalidDataException("Original Map14 butterfly renderer material differs.");
            pose = new NativeMap14ButterflyData.Bone[model.bones.Length];
            matrices = new Matrix4x4[pose.Length]; positions = new Vector3[skin.vertices.Length];
            mesh = new Mesh { name = model.name + " native animated mesh" }; mesh.MarkDynamic();
            mesh.vertices = positions; mesh.uv = skin.uv; mesh.triangles = skin.triangles;
            var colors = new Color32[positions.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = colors; GetComponent<MeshFilter>().sharedMesh = mesh;
            Sample(0);
        }

        private void Update()
        {
            if (updateWhenInvisible == 0 && !meshRenderer.isVisible) return;
            elapsed += Time.deltaTime * timeScale;
            Sample(elapsed);
        }

        public void Sample(float time)
        {
            if (!mesh) throw new InvalidOperationException("Original Map14 butterfly has not initialized.");
            time = Mathf.Repeat(time, clip.duration);
            Array.Copy(model.bones, pose, pose.Length);
            foreach (var track in clip.tracks)
            {
                if (track.bone < 0 || track.bone >= pose.Length || track.keys == null || track.keys.Length == 0)
                    throw new InvalidDataException("Original Map14 butterfly track binding differs.");
                if (time < track.keys[0].time) continue;
                Vector2 value = Evaluate(track.keys, time);
                var bone = pose[track.bone];
                switch (track.channel)
                {
                    case 0: bone.rotation += value.x; break;
                    case 1: bone.x += value.x; bone.y += value.y; break;
                    case 2: bone.scaleX *= value.x; bone.scaleY *= value.y; break;
                    case 3: bone.shearX += value.x; bone.shearY += value.y; break;
                    default: throw new InvalidDataException("Unconverted original butterfly channel.");
                }
                pose[track.bone] = bone;
            }
            for (int i = 0; i < pose.Length; i++)
            {
                var bone = pose[i];
                if (bone.parent >= i) throw new InvalidDataException("Original butterfly parent order differs.");
                float xAngle = (bone.rotation + bone.shearX) * Mathf.Deg2Rad;
                float yAngle = (bone.rotation + 90 + bone.shearY) * Mathf.Deg2Rad;
                var local = Matrix4x4.identity;
                local.m00 = Mathf.Cos(xAngle) * bone.scaleX; local.m10 = Mathf.Sin(xAngle) * bone.scaleX;
                local.m01 = Mathf.Cos(yAngle) * bone.scaleY; local.m11 = Mathf.Sin(yAngle) * bone.scaleY;
                local.m03 = bone.x; local.m13 = bone.y;
                if (bone.parent < 0) matrices[i] = local;
                else if (bone.onlyTranslation)
                {
                    Vector3 origin = matrices[bone.parent].MultiplyPoint3x4(new Vector3(bone.x, bone.y));
                    local.m03 = origin.x; local.m13 = origin.y; matrices[i] = local;
                }
                else matrices[i] = matrices[bone.parent] * local;
            }
            for (int i = 0; i < positions.Length; i++)
                positions[i] = matrices[skin.vertexBones[i]].MultiplyPoint3x4(skin.vertices[i]) * model.scale;
            mesh.vertices = positions; mesh.RecalculateBounds();
        }

        private static Vector2 Evaluate(NativeMap14ButterflyData.Key[] keys, float time)
        {
            int left = 0;
            while (left + 1 < keys.Length && keys[left + 1].time <= time) left++;
            var a = keys[left];
            if (left + 1 == keys.Length || a.stepped) return new Vector2(a.x, a.y);
            var b = keys[left + 1];
            if (a.curve.Length == 0)
                return Vector2.LerpUnclamped(new Vector2(a.x, a.y), new Vector2(b.x, b.y), (time - a.time) / (b.time - a.time));
            float x = CubicAtTime(time, a.time, a.x, b.time, b.x, a.curve, 0);
            float y = a.curve.Length == 8 ? CubicAtTime(time, a.time, a.y, b.time, b.y, a.curve, 4) : a.y;
            return new Vector2(x, y);
        }

        private static float CubicAtTime(float time, float t0, float v0, float t1, float v1, float[] control, int offset)
        {
            // Solve the original absolute time/value control polygon directly.
            float low = 0, high = 1;
            for (int i = 0; i < 24; i++)
            {
                float u = (low + high) * .5f;
                if (Cubic(t0, control[offset], control[offset + 2], t1, u) < time) low = u;
                else high = u;
            }
            return Cubic(v0, control[offset + 1], control[offset + 3], v1, (low + high) * .5f);
        }

        private static float Cubic(float a, float b, float c, float d, float t)
        {
            float s = 1 - t;
            return s*s*s*a + 3*s*s*t*b + 3*s*t*t*c + t*t*t*d;
        }

        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
