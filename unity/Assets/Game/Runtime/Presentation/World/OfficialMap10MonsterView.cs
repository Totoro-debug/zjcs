using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class OfficialMap10MonsterView : MonoBehaviour
    {
        [SerializeField] private int monsterId;
        [SerializeField] private int sortingOrderOffset;

        private SequencePayload sequence;
        private SequencePayload idleSequence;
        private Mesh generatedMesh;
        private Material generatedMaterial;
        private MeshRenderer meshRenderer;
        private int currentFrame = -1;
        private float elapsed;
        private bool loopAnimation = true;
        private bool restoreIdleWhenFinished;
        private bool animationFinished;
        private bool hasBaseLocalPosition;
        private Vector3 baseLocalPosition;

        public int MonsterId => monsterId;
        public string Animation => sequence != null ? sequence.animation : string.Empty;

        private void Awake()
        {
            if (monsterId > 0) Initialize(monsterId);
        }

        private void Update()
        {
            if (sequence == null || sequence.frames.Length == 0 || animationFinished) return;

            elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(elapsed * sequence.fps);
            if (loopAnimation) frame %= sequence.frames.Length;
            else if (frame >= sequence.frames.Length)
            {
                if (restoreIdleWhenFinished && idleSequence != null && sequence != idleSequence)
                {
                    SetSequence(idleSequence, true, false);
                    return;
                }
                frame = sequence.frames.Length - 1;
                animationFinished = true;
            }
            if (frame != currentFrame) ApplyFrame(frame);
        }

        private void OnDestroy()
        {
            if (generatedMesh != null) Destroy(generatedMesh);
            if (generatedMaterial != null) Destroy(generatedMaterial);
        }

        public void Initialize(int id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            TextAsset source = Resources.Load<TextAsset>("Map10/Monsters/" + id);
            if (source == null) throw new InvalidOperationException("Official Map10 monster payload is missing: " + id);
            Initialize(source, id);
        }

        public void Initialize(TextAsset source, int id, int expectedMapClassId = 10)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (source == null) throw new ArgumentNullException(nameof(source));
            SequencePayload loaded = JsonUtility.FromJson<SequencePayload>(source.text);
            ValidatePayload(loaded, id, expectedMapClassId);

            InitializeSequence(loaded, id, null, Color.white, 0, 1f, 0f, -1000000f);
        }

        public void InitializeAnimation(TextAsset source, Shader shader, Color tint, int sortingOrderOffset = 0,
            float colorIntensity = 4f, float alphaIntensity = 0f, float clipHeight = -1000000f)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            SequencePayload loaded = JsonUtility.FromJson<SequencePayload>(source.text);
            ValidatePayload(loaded, 0, 10);
            if (shader == null) throw new ArgumentNullException(nameof(shader));

            InitializeSequence(loaded, 0, shader, tint, sortingOrderOffset, colorIntensity, alphaIntensity, clipHeight);
        }

        private void InitializeSequence(SequencePayload loaded, int id, Shader shader, Color tint, int orderOffset,
            float colorIntensity, float alphaIntensity, float clipHeight)
        {
            Texture2D atlas = Resources.Load<Texture2D>(loaded.textureResource);
            if (atlas == null) throw new InvalidOperationException("Official Map10 monster atlas is missing: " + loaded.textureResource);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Unity's Sprites/Default shader is unavailable.");

            if (generatedMesh != null) Destroy(generatedMesh);
            if (generatedMaterial != null) Destroy(generatedMaterial);
            sequence = loaded;
            monsterId = id;
            elapsed = 0f;
            currentFrame = -1;

            generatedMesh = new Mesh { name = "Map10 monster " + id + " idle" };
            generatedMesh.MarkDynamic();
            generatedMaterial = new Material(shader) { name = "Map10 monster " + id + " material" };
            generatedMaterial.mainTexture = atlas;
            generatedMaterial.renderQueue = (int)RenderQueue.Transparent;
            if (generatedMaterial.HasProperty("_MainColor")) generatedMaterial.SetColor("_MainColor", tint);
            if (generatedMaterial.HasProperty("_ColorIntensity")) generatedMaterial.SetFloat("_ColorIntensity", colorIntensity);
            if (generatedMaterial.HasProperty("_IntensityAffectAlpha"))
                generatedMaterial.SetFloat("_IntensityAffectAlpha", alphaIntensity);
            if (generatedMaterial.HasProperty("_WorldClipHeight")) generatedMaterial.SetFloat("_WorldClipHeight", clipHeight);
            if (shader.name == "Zjcs/World/Map10ButterflyNoiseMask") generatedMaterial.EnableKeyword("USE_MAINTEX");
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = generatedMaterial;
            meshRenderer.sortingOrder = sequence.sortingLayer * 1000 + sequence.sortingOrder + orderOffset;
            if (loaded.mapClassId == 12)
            {
                meshRenderer.sortingLayerID = SortingLayer.layers[loaded.sortingLayer].id;
                meshRenderer.sortingOrder = loaded.sortingOrder + orderOffset;
            }
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            GetComponent<MeshFilter>().sharedMesh = generatedMesh;

            if (!hasBaseLocalPosition)
            {
                baseLocalPosition = transform.localPosition;
                hasBaseLocalPosition = true;
            }
            idleSequence = loaded;
            SetSequence(loaded, true, false);
        }

        public void PlayAnimation(TextAsset source, bool loop, bool restoreIdleOnComplete)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (monsterId <= 0 || generatedMesh == null || generatedMaterial == null)
                throw new InvalidOperationException("Official Map10 entity Spine view is not initialized.");
            SequencePayload loaded = JsonUtility.FromJson<SequencePayload>(source.text);
            ValidatePayload(loaded, monsterId, sequence.mapClassId);
            Texture2D atlas = Resources.Load<Texture2D>(loaded.textureResource);
            if (atlas == null)
                throw new InvalidOperationException("Official Map10 story actor atlas is missing: " + loaded.textureResource);
            generatedMaterial.mainTexture = atlas;
            meshRenderer.sortingOrder = loaded.sortingLayer * 1000 + loaded.sortingOrder + sortingOrderOffset;
            if (loaded.mapClassId == 12)
            {
                meshRenderer.sortingLayerID = SortingLayer.layers[loaded.sortingLayer].id;
                meshRenderer.sortingOrder = loaded.sortingOrder + sortingOrderOffset;
            }
            SetSequence(loaded, loop, restoreIdleOnComplete);
        }

        private void SetSequence(SequencePayload value, bool loop, bool restoreIdle)
        {
            sequence = value;
            loopAnimation = loop;
            restoreIdleWhenFinished = restoreIdle;
            animationFinished = false;
            elapsed = 0f;
            currentFrame = -1;
            transform.localPosition = baseLocalPosition + new Vector3(value.offsetX, value.offsetY, 0f);
            ApplyFrame(0);
        }

        public static int ValidatePayload(TextAsset source, int expectedMonsterId, out string textureResource,
            int expectedMapClassId = 10)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            SequencePayload parsed = JsonUtility.FromJson<SequencePayload>(source.text);
            ValidatePayload(parsed, expectedMonsterId, expectedMapClassId);
            textureResource = parsed.textureResource;
            return parsed.frames.Length;
        }

        private static void ValidatePayload(SequencePayload value, int expectedMonsterId, int expectedMapClassId)
        {
            if (value == null || value.format != 1 || value.mapClassId != expectedMapClassId
                || (expectedMonsterId > 0 && value.monsterId != expectedMonsterId) || value.monsterId < 0
                || value.fps <= 0 || value.frames == null || value.frames.Length == 0
                || string.IsNullOrEmpty(value.textureResource))
                throw new InvalidOperationException("Official Map10 monster animation payload is invalid: " + expectedMonsterId);

            foreach (Frame frame in value.frames)
            {
                if (frame == null || frame.attachments == null)
                    throw new InvalidOperationException("Official Map10 monster frame is empty: " + expectedMonsterId);
                foreach (Attachment attachment in frame.attachments)
                {
                    if (attachment == null || attachment.vertices == null || attachment.vertices.Length == 0
                        || attachment.vertices.Length % 2 != 0 || attachment.uv == null
                        || attachment.uv.Length != attachment.vertices.Length || attachment.triangles == null
                        || attachment.triangles.Length == 0 || attachment.triangles.Length % 3 != 0
                        || attachment.color == null || attachment.color.Length != 4)
                        throw new InvalidOperationException("Official Map10 monster attachment is malformed: " + expectedMonsterId);
                    int vertexCount = attachment.vertices.Length / 2;
                    foreach (int index in attachment.triangles)
                        if (index < 0 || index >= vertexCount)
                            throw new InvalidOperationException("Official Map10 monster triangle index is invalid: " + expectedMonsterId);
                }
            }
        }

        private void ApplyFrame(int frameIndex)
        {
            Frame frame = sequence.frames[frameIndex];
            int vertexCount = 0;
            int indexCount = 0;
            foreach (Attachment attachment in frame.attachments)
            {
                vertexCount += attachment.vertices.Length / 2;
                indexCount += attachment.triangles.Length;
            }

            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var colors = new Color[vertexCount];
            var triangles = new int[indexCount];
            int vertexOffset = 0;
            int indexOffset = 0;
            foreach (Attachment attachment in frame.attachments)
            {
                int count = attachment.vertices.Length / 2;
                float depth = attachment.order * 0.0001f;
                var color = new Color(attachment.color[0], attachment.color[1], attachment.color[2], attachment.color[3]);
                for (int i = 0; i < count; i++)
                {
                    vertices[vertexOffset + i] = new Vector3(attachment.vertices[i * 2] * sequence.scaleX,
                        attachment.vertices[i * 2 + 1] * sequence.scaleY, depth);
                    uvs[vertexOffset + i] = new Vector2(attachment.uv[i * 2], attachment.uv[i * 2 + 1]);
                    colors[vertexOffset + i] = color;
                }
                for (int i = 0; i < attachment.triangles.Length; i++)
                    triangles[indexOffset + i] = vertexOffset + attachment.triangles[i];
                vertexOffset += count;
                indexOffset += attachment.triangles.Length;
            }

            generatedMesh.Clear();
            if (vertices.Length > ushort.MaxValue) generatedMesh.indexFormat = IndexFormat.UInt32;
            generatedMesh.vertices = vertices;
            generatedMesh.uv = uvs;
            generatedMesh.colors = colors;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();
            currentFrame = frameIndex;
        }

        [Serializable]
        private sealed class SequencePayload
        {
            public int format;
            public int mapClassId;
            public int monsterId;
            public string modelPath;
            public string animation;
            public float duration;
            public int fps;
            public float scaleX;
            public float scaleY;
            public float offsetX;
            public float offsetY;
            public int sortingLayer;
            public int sortingOrder;
            public string textureResource;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class Frame
        {
            public Attachment[] attachments;
        }

        [Serializable]
        private sealed class Attachment
        {
            public int order;
            public string slot;
            public string page;
            public float[] vertices;
            public float[] uv;
            public int[] triangles;
            public float[] color;
        }
    }
}
