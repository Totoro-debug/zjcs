using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleNativeModelView : IOfficialBattleNativeModel
    {
        private sealed class DrawView
        {
            public GameObject Object;
            public Mesh Mesh;
            public MeshRenderer Renderer;
        }

        private readonly OfficialBattleNativeAnimationLibrary library;
        private readonly OfficialBattleNativeModelRecord model;
        private readonly List<DrawView> draws = new List<DrawView>();
        private readonly Vector3 modelScale;
        private OfficialBattleNativeAnimationData animation;
        private float elapsed;
        private int currentFrame = -1;
        private bool loop;
        private bool disposed;

        public OfficialBattleNativeModelView(OfficialBattleNativeAnimationLibrary library,
            OfficialBattleNativeModelRecord model, Transform parent, string objectName)
        {
            this.library = library != null ? library : throw new ArgumentNullException(nameof(library));
            this.model = model != null ? model : throw new ArgumentNullException(nameof(model));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            OfficialBattleSceneBindings.Unit source = OfficialBattleSceneBindings.Find(model.monsterId);
            if (source?.model?.transforms == null || source.model.transforms.Length == 0)
                throw new InvalidOperationException("Official native model transform binding is missing: " + model.monsterId);
            var root = new GameObject(objectName);
            Transform = root.transform;
            Transform.SetParent(parent, false);
            Transform modelParent = Transform;
            foreach (OfficialBattleSceneBindings.TransformBinding binding in source.model.transforms)
            {
                var child = new GameObject(binding.name).transform;
                child.SetParent(modelParent, false);
                child.localPosition = new Vector3(binding.position[0], binding.position[1], binding.position[2]);
                child.localRotation = new Quaternion(binding.rotation[0], binding.rotation[1], binding.rotation[2], binding.rotation[3]);
                child.localScale = new Vector3(binding.scale[0], binding.scale[1], binding.scale[2]);
                modelParent = child;
            }
            ModelTransform = modelParent;
            modelScale = ModelTransform.localScale;
        }

        public Transform Transform { get; }
        public Transform ModelTransform { get; }
        public string Animation => animation == null ? string.Empty : animation.animation;
        public bool IsLooping => loop;
        public bool IsAnimationFinished { get; private set; }

        public bool HasAnimation(string name)
        {
            return library.HasAnimation(model.key, name);
        }

        public bool Play(string name, bool loopAnimation, bool restart = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleNativeModelView));
            if (string.IsNullOrEmpty(name) || !HasAnimation(name)) return false;
            if (!restart && animation != null && animation.animation == name && loop == loopAnimation
                && !IsAnimationFinished) return true;
            OfficialBattleNativeAnimationData next = library.LoadAnimation(model.key, name);
            animation = next;
            loop = loopAnimation;
            elapsed = 0f;
            IsAnimationFinished = false;
            currentFrame = -1;
            ModelTransform.localScale = modelScale * next.scale;
            ApplyFrame(0);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (disposed || animation == null || IsAnimationFinished || deltaTime <= 0f) return;
            elapsed += deltaTime;
            int frame = Mathf.FloorToInt(elapsed * animation.fps);
            if (loop)
                frame %= animation.frames.Length;
            else if (frame >= animation.frames.Length || elapsed >= animation.duration)
            {
                frame = animation.frames.Length - 1;
                IsAnimationFinished = true;
            }
            if (frame != currentFrame) ApplyFrame(frame);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (DrawView draw in draws)
            {
                OfficialBattleSceneBindings.DestroyCreatedObject(draw.Mesh);
                OfficialBattleSceneBindings.DestroyCreatedObject(draw.Object);
            }
            draws.Clear();
            if (Transform != null) OfficialBattleSceneBindings.DestroyCreatedObject(Transform.gameObject);
        }

        private void ApplyFrame(int frameIndex)
        {
            OfficialBattleNativeFrameData frame = animation.frames[frameIndex];
            int required = frame.batches.Length;
            while (draws.Count < required) draws.Add(CreateDraw(draws.Count));
            for (int i = 0; i < draws.Count; i++)
            {
                DrawView drawView = draws[i];
                if (i >= required)
                {
                    if (drawView.Object.activeSelf) drawView.Object.SetActive(false);
                    continue;
                }
                OfficialBattleNativeDrawData draw = frame.batches[i];
                if (!drawView.Object.activeSelf) drawView.Object.SetActive(true);
                int vertexCount = draw.vertices.Length / 2;
                var vertices = new Vector3[vertexCount];
                var uv = new Vector2[vertexCount];
                var colors = new Color[vertexCount];
                Color tint = new Color(draw.color[0], draw.color[1], draw.color[2], draw.color[3]);
                for (int vertex = 0; vertex < vertexCount; vertex++)
                {
                    vertices[vertex] = new Vector3(draw.vertices[vertex * 2], draw.vertices[vertex * 2 + 1], 0f);
                    // Atlas V in the sampled payload starts at the top of the PNG.
                    uv[vertex] = new Vector2(draw.uv[vertex * 2], 1f - draw.uv[vertex * 2 + 1]);
                    colors[vertex] = tint;
                }
                drawView.Mesh.Clear();
                if (vertexCount > ushort.MaxValue) drawView.Mesh.indexFormat = IndexFormat.UInt32;
                drawView.Mesh.vertices = vertices;
                drawView.Mesh.uv = uv;
                drawView.Mesh.colors = colors;
                drawView.Mesh.triangles = draw.triangles;
                drawView.Mesh.RecalculateBounds();
                drawView.Renderer.sharedMaterial = library.LoadMaterial(draw.page, draw.blend);
                drawView.Renderer.sortingOrder = draw.order;
            }
            currentFrame = frameIndex;
        }

        private DrawView CreateDraw(int index)
        {
            var instance = new GameObject("Native draw " + index.ToString("D2"));
            instance.transform.SetParent(ModelTransform, false);
            var mesh = new Mesh { name = "Official Battle Mesh " + model.key + " " + index.ToString("D2") };
            mesh.MarkDynamic();
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new DrawView { Object = instance, Mesh = mesh, Renderer = renderer };
        }
    }
}
