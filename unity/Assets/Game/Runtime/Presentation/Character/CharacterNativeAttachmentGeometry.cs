using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Presentation.CharacterCreation
{
    /// <summary>Evaluates official bone-local attachment geometry on the existing native Animator bones.</summary>
    public sealed class CharacterNativeAttachmentGeometry : MonoBehaviour
    {
        [Serializable] private sealed class Payload { public string manifest; public Body[] sexes; }
        [Serializable] private sealed class Receipt { public string manifest, output_sha256; }
        [Serializable] private sealed class Body { public string sex; public string[] bones; public Attachment[] attachments; public Region[] regions; }
        [Serializable] private sealed class Attachment
        {
            public string skin, slot, attachment, path, kind;
            public int bone;
            public float x, y, rotation, scaleX, scaleY, width, height;
            public float[] vertices, uvs;
            public int[] triangles;
        }
        [Serializable] private sealed class Region
        {
            public string page, name;
            public int[] size, bounds, offsets;
            public int rotate;
        }
        private struct Influence { public int Bone; public Vector3 Position; public float Weight; }
        private sealed class Draw
        {
            public Renderer Source;
            public Mesh Mesh;
            public string Name;
            public Vector2[] Uvs;
            public Color[] Colors;
            public MeshRenderer Renderer;
            public Influence[][] Vertices;
            public Vector3[] Positions;
            public int[] Triangles;
            public bool PreviousForceOff;
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        }
        private static Payload payload;
        private readonly List<Draw> draws = new List<Draw>();
        private Transform[] bones;
        private bool initialized;

        public void Initialize(string sex)
        {
            if (initialized) throw new InvalidOperationException("Native geometry is already initialized.");
            Load();
            Body body = Array.Find(payload.sexes, value => value.sex == sex);
            if (body == null) throw new InvalidOperationException("Native attachment source is missing: " + sex);
            var wantedBones = new HashSet<string>(body.bones, StringComparer.Ordinal);
            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (wantedBones.Contains(child.name))
                {
                    if (byName.ContainsKey(child.name)) throw new InvalidOperationException("Ambiguous native bone: " + child.name);
                    byName.Add(child.name, child);
                }
            bones = new Transform[body.bones.Length];
            for (int i = 0; i < bones.Length; i++)
                if (!byName.TryGetValue(body.bones[i], out bones[i]))
                    throw new InvalidOperationException("Native character bone is missing: " + body.bones[i]);
            var attachments = new Dictionary<string, Attachment>(StringComparer.Ordinal);
            foreach (var attachment in body.attachments)
                attachments.Add(attachment.skin + "\n" + attachment.slot + "\n" + attachment.attachment, attachment);
            var regions = new Dictionary<string, Region>(StringComparer.Ordinal);
            foreach (Region region in body.regions) regions.Add(region.name, region);
            foreach (Renderer source in GetComponentsInChildren<Renderer>(true))
            {
                if (!source.name.StartsWith("__skin__", StringComparison.Ordinal)) continue;
                Mesh original;
                if (source is SkinnedMeshRenderer skinned) original = skinned.sharedMesh;
                else
                {
                    MeshFilter filter = source.GetComponent<MeshFilter>();
                    original = filter == null ? null : filter.sharedMesh;
                }
                Transform attachmentRoot = source.transform.parent;
                Transform slotRoot = attachmentRoot == null ? null : attachmentRoot.parent;
                if (slotRoot == null) throw new InvalidOperationException("Native attachment has no official slot hierarchy.");
                string key = source.name.Substring(8) + "\n" + slotRoot.name + "\n" + attachmentRoot.name;
                if (!attachments.TryGetValue(key, out Attachment attachment))
                    throw new InvalidOperationException("Official native attachment is missing: " + key);
                if (!regions.TryGetValue(attachment.path, out Region region))
                    throw new InvalidOperationException("Official attachment atlas path is missing: " + attachment.path);
                Texture texture = source.sharedMaterial == null ? null : source.sharedMaterial.mainTexture;
                if (texture == null || texture.name != region.page)
                    throw new InvalidOperationException("Native material differs from the official atlas page: " + key);
                Vector2[] uv = AttachmentUvs(attachment, region);
                if (original != null)
                {
                    Vector2[] previousUvs = original.uv;
                    if (previousUvs.Length != uv.Length)
                        throw new InvalidOperationException("Native attachment vertex count differs from the official source: " + key);
                    for (int i = 0; i < uv.Length; i++)
                    {
                        float distance = attachment.kind == "mesh" ? (uv[i] - previousUvs[i]).sqrMagnitude : float.MaxValue;
                        if (attachment.kind != "mesh")
                            foreach (Vector2 point in previousUvs) distance = Mathf.Min(distance, (uv[i] - point).sqrMagnitude);
                        if (distance > 1e-8f)
                            throw new InvalidOperationException("Native attachment UV differs from the official atlas: " + key);
                    }
                }
                Influence[][] vertices = attachment.kind == "mesh"
                    ? MeshVertices(attachment, uv.Length)
                    : RegionVertices(attachment, uv, region);
                var draw = new Draw { Source = source, Vertices = vertices,
                    Name = "[" + attachment.slot + "] " + attachment.attachment,
                    Uvs = uv, Colors = original != null ? original.colors : null,
                    Positions = new Vector3[vertices.Length], PreviousForceOff = source.forceRenderingOff,
                    Triangles = attachment.kind == "mesh" ? attachment.triangles : new[] { 0, 1, 2, 2, 3, 0 } };
                draws.Add(draw);
                source.forceRenderingOff = true;
            }
            initialized = true;
            LateUpdate();
        }

        private static void Load()
        {
            if (payload != null) return;
            TextAsset asset = Resources.Load<TextAsset>("CharacterCreation/native-character-geometry");
            TextAsset source = Resources.Load<TextAsset>("CharacterCreation/native-character-geometry-source-manifest");
            if (asset == null || source == null) throw new InvalidOperationException("Official native character geometry is not installed.");
            Receipt receipt = JsonUtility.FromJson<Receipt>(source.text);
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(asset.bytes)).Replace("-", "").ToLowerInvariant();
            if (receipt == null || receipt.manifest != "871_156598" || receipt.output_sha256 != hash)
                throw new InvalidOperationException("Official native character geometry differs from its source receipt.");
            Payload decoded = JsonUtility.FromJson<Payload>(asset.text);
            if (decoded == null || decoded.manifest != receipt.manifest || decoded.sexes == null)
                throw new InvalidOperationException("Official native character geometry payload is invalid.");
            payload = decoded;
        }

        private Influence[][] MeshVertices(Attachment attachment, int count)
        {
            if (attachment.uvs == null || attachment.uvs.Length != count * 2)
                throw new InvalidOperationException("Native vertex layout differs from the official attachment: " + attachment.attachment);
            var result = new Influence[count][];
            float[] source = attachment.vertices;
            bool weighted = source.Length != count * 2;
            int cursor = 0;
            for (int vertex = 0; vertex < count; vertex++)
            {
                int influences = weighted ? checked((int)source[cursor++]) : 1;
                result[vertex] = new Influence[influences];
                for (int i = 0; i < influences; i++)
                {
                    int bone = weighted ? checked((int)source[cursor++]) : attachment.bone;
                    float x = source[cursor++], y = source[cursor++];
                    float weight = weighted ? source[cursor++] : 1f;
                    if (bone < 0 || bone >= bones.Length) throw new InvalidOperationException("Official attachment bone index is invalid.");
                    result[vertex][i] = new Influence { Bone = bone, Position = new Vector3(x, y, 0f) * .01f, Weight = weight };
                }
            }
            if (cursor != source.Length) throw new InvalidOperationException("Official attachment vertex data has trailing values.");
            return result;
        }

        private static Vector2[] AttachmentUvs(Attachment attachment, Region region)
        {
            bool mesh = attachment.kind == "mesh";
            int count = mesh ? attachment.uvs.Length / 2 : 4;
            var result = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float u = mesh ? (attachment.uvs[2 * i] * region.offsets[2] - region.offsets[0]) / region.bounds[2]
                    : (i >= 2 ? 1f : 0f);
                float v = mesh ? (attachment.uvs[2 * i + 1] * region.offsets[3]
                    - (region.offsets[3] - region.offsets[1] - region.bounds[3])) / region.bounds[3]
                    : (i == 0 || i == 3 ? 1f : 0f);
                float x, y;
                switch (region.rotate)
                {
                    case 0: x = u * region.bounds[2]; y = v * region.bounds[3]; break;
                    case 90: x = v * region.bounds[3]; y = (1f - u) * region.bounds[2]; break;
                    case 180: x = (1f - u) * region.bounds[2]; y = (1f - v) * region.bounds[3]; break;
                    case 270: x = (1f - v) * region.bounds[3]; y = u * region.bounds[2]; break;
                    default: throw new InvalidOperationException("Unsupported official atlas rotation.");
                }
                result[i] = new Vector2((region.bounds[0] + x) / region.size[0], 1f - (region.bounds[1] + y) / region.size[1]);
            }
            return result;
        }

        private static Influence[][] RegionVertices(Attachment attachment, Vector2[] uv, Region match)
        {
            if (uv.Length != 4) throw new InvalidOperationException("A native region must have exactly four UV corners.");
            var result = new Influence[4][];
            float angle = attachment.rotation * Mathf.Deg2Rad, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            for (int i = 0; i < 4; i++)
            {
                float atlasX = uv[i].x * match.size[0] - match.bounds[0];
                float atlasY = (1f - uv[i].y) * match.size[1] - match.bounds[1];
                float u, v;
                switch (match.rotate)
                {
                    case 0: u = atlasX / match.bounds[2]; v = atlasY / match.bounds[3]; break;
                    case 90: u = 1f - atlasY / match.bounds[2]; v = atlasX / match.bounds[3]; break;
                    case 180: u = 1f - atlasX / match.bounds[2]; v = 1f - atlasY / match.bounds[3]; break;
                    case 270: u = atlasY / match.bounds[2]; v = 1f - atlasX / match.bounds[3]; break;
                    default: throw new InvalidOperationException("Unsupported official atlas rotation.");
                }
                float x = ((match.offsets[0] + u * match.bounds[2]) / match.offsets[2] - .5f) * attachment.width * attachment.scaleX;
                float y = ((match.offsets[1] + (1f - v) * match.bounds[3]) / match.offsets[3] - .5f) * attachment.height * attachment.scaleY;
                result[i] = new[] { new Influence { Bone = attachment.bone,
                    Position = new Vector3(attachment.x + cos * x - sin * y, attachment.y + sin * x + cos * y, 0f) * .01f,
                    Weight = 1f } };
            }
            return result;
        }

        public void RefreshForCapture() => LateUpdate();

        private void LateUpdate()
        {
            if (!initialized) return;
            foreach (Draw draw in draws)
            {
                if (draw.Source == null || !draw.Source.gameObject.activeInHierarchy) continue;
                if (draw.Renderer == null) CreateDraw(draw);
                draw.Renderer.enabled = draw.Source.enabled && !draw.PreviousForceOff;
                if (!draw.Renderer.enabled) continue;
                draw.Renderer.sortingLayerID = draw.Source.sortingLayerID;
                draw.Renderer.sortingOrder = draw.Source.sortingOrder;
                draw.Source.GetPropertyBlock(draw.Properties);
                draw.Renderer.SetPropertyBlock(draw.Properties);
                Matrix4x4 toLocal = draw.Renderer.transform.worldToLocalMatrix;
                for (int vertex = 0; vertex < draw.Vertices.Length; vertex++)
                {
                    Vector3 world = Vector3.zero;
                    foreach (Influence influence in draw.Vertices[vertex])
                        world += bones[influence.Bone].TransformPoint(influence.Position) * influence.Weight;
                    draw.Positions[vertex] = toLocal.MultiplyPoint3x4(world);
                }
                draw.Mesh.vertices = draw.Positions;
                draw.Mesh.RecalculateBounds();
            }
        }

        private static void CreateDraw(Draw draw)
        {
            var child = new GameObject("Official native attachment geometry", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(draw.Source.transform, false);
            child.layer = draw.Source.gameObject.layer;
            draw.Mesh = new Mesh { name = draw.Name + " official bone geometry" };
            draw.Mesh.MarkDynamic();
            draw.Mesh.vertices = draw.Positions;
            draw.Mesh.uv = draw.Uvs;
            if (draw.Colors != null && draw.Colors.Length == draw.Positions.Length) draw.Mesh.colors = draw.Colors;
            draw.Mesh.triangles = draw.Triangles;
            child.GetComponent<MeshFilter>().sharedMesh = draw.Mesh;
            draw.Renderer = child.GetComponent<MeshRenderer>();
            draw.Renderer.sharedMaterials = draw.Source.sharedMaterials;
            draw.Renderer.shadowCastingMode = draw.Source.shadowCastingMode;
            draw.Renderer.receiveShadows = draw.Source.receiveShadows;
        }

        private void OnDestroy()
        {
            foreach (Draw draw in draws)
            {
                if (draw.Source != null) draw.Source.forceRenderingOff = draw.PreviousForceOff;
                if (draw.Renderer != null)
                {
                    if (Application.isPlaying) Destroy(draw.Renderer.gameObject);
                    else DestroyImmediate(draw.Renderer.gameObject);
                }
                if (draw.Mesh != null)
                {
                    if (Application.isPlaying) Destroy(draw.Mesh);
                    else DestroyImmediate(draw.Mesh);
                }
            }
            draws.Clear();
        }
    }
}
