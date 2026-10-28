using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    // Independent planar affine geometry. Source weights and curves are evaluated live;
    // no Spine runtime, pose baking or alternate art is involved.
    public sealed class NativeMap13SkeletonView : IDisposable
    {
        private sealed class Draw { public Mesh Mesh; public MeshRenderer Renderer; }
        private struct Vertex
        {
            public Vector2 Point, UV;
            public Vertex(Vector2 point, Vector2 uv) { Point = point; UV = uv; }
        }
        private readonly NativeMap13SkeletonSource source;
        private readonly NativeMap13Renderer profile;
        private readonly Matrix4x4[] matrices;
        private readonly Vector2[] positions, scales, shears;
        private readonly float[] rotations;
        private readonly Draw[] draws;
        private readonly int[] attachments, order;
        private readonly Color[] colors;
        private readonly Transform skeletonRoot;
        private MeshRenderer[] nativeTargets;
        private Mesh[] nativeMeshes;
        private HashSet<int> separatorSlots;
        private bool[] nativeEnabled;
        private MeshRenderer fallbackTarget;
        private Mesh fallbackMesh;
        private bool separationEnabled = true;
        private NativeMap13Animation animation;
        private float elapsed;
        private bool loop, disposed;
        public Transform Transform { get; }
        public bool IsAnimationFinished { get; private set; }
        public IReadOnlyList<Matrix4x4> BoneMatrices => matrices;
        public int VisibleTriangles { get; private set; }
        public Bounds LocalBounds { get; private set; }

        // Bind the original prefab's mesh targets. Their transforms, sorting,
        // renderer flags and native Animator clocks remain on the original objects.
        // Segments follow the current slot draw order, ending at each separator.
        public void BindMeshTargets(MeshRenderer[] targets, string[] separators, MeshRenderer fallback = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap13SkeletonView));
            if (nativeTargets != null) throw new InvalidOperationException("Original mesh targets are already bound.");
            if (targets == null || separators == null || targets.Length != separators.Length + 1 || targets.Any(t => !t))
                throw new InvalidOperationException("Original separator/parts renderer inventory differs.");
            var indices = new HashSet<int>();
            foreach (string separator in separators)
            {
                int slot = Array.FindIndex(source.slots, s => s.name == separator);
                if (slot < 0 || !indices.Add(slot)) throw new InvalidOperationException("Original separator slot is absent/duplicated.");
            }
            foreach (var target in targets)
                if (!target.GetComponent<MeshFilter>()) throw new InvalidOperationException("Original mesh filter is not bound.");
            if (fallback && (!fallback.GetComponent<MeshFilter>() || fallback.sharedMaterials.Length != targets.Length))
                throw new InvalidOperationException("Original unsplit renderer/submesh binding differs.");
            nativeTargets = (MeshRenderer[])targets.Clone(); separatorSlots = indices;
            nativeEnabled = targets.Select(t => t.enabled).ToArray();
            nativeMeshes = targets.Select(t => new Mesh { name = t.name + " original slot segment" }).ToArray();
            for (int i = 0; i < targets.Length; i++)
            {
                nativeMeshes[i].MarkDynamic();
                targets[i].GetComponent<MeshFilter>().sharedMesh = nativeMeshes[i];
            }
            if (fallback)
            {
                fallbackTarget = fallback; fallbackMesh = new Mesh { name = fallback.name + " original unsplit slot mesh" };
                fallbackMesh.MarkDynamic(); fallback.GetComponent<MeshFilter>().sharedMesh = fallbackMesh;
            }
            DrawGeometry();
        }
        public void SetSeparationEnabled(bool value)
        {
            if (nativeTargets == null || !fallbackTarget) throw new InvalidOperationException("Original separator fallback is not bound.");
            separationEnabled = value;
            for (int i = 0; i < nativeTargets.Length; i++) nativeTargets[i].enabled = value && nativeEnabled[i] && nativeMeshes[i].vertexCount > 0;
            fallbackTarget.enabled = !value && fallbackMesh.vertexCount > 0;
        }

        public NativeMap13SkeletonView(NativeMap13SkeletonData asset, NativeMap13Renderer renderer,
            Transform parent, string name, int sortingOrder = 0)
        {
            if (!asset || !asset.Material || asset.Source?.bones == null || renderer == null)
                throw new InvalidOperationException("Original Map13 skeleton/material/profile is not bound.");
            source = asset.Source; profile = renderer;
            if (source.constraints.Length != 0 || profile.tintBlack)
                throw new InvalidOperationException("Constrained/two-color skeletons require their own migration.");
            Transform = new GameObject(name).transform; Transform.SetParent(parent, false);
            var group = Transform.gameObject.AddComponent<SortingGroup>(); group.sortingOrder = sortingOrder;
            skeletonRoot = new GameObject("Native planar geometry").transform; skeletonRoot.SetParent(Transform, false);
            skeletonRoot.localScale = new Vector3(profile.initialFlipX ? -1 : 1, profile.initialFlipY ? -1 : 1, 1);
            matrices = new Matrix4x4[source.bones.Length]; positions = new Vector2[matrices.Length];
            scales = new Vector2[matrices.Length]; shears = new Vector2[matrices.Length]; rotations = new float[matrices.Length];
            attachments = new int[source.slots.Length]; order = new int[attachments.Length]; colors = new Color[attachments.Length];
            draws = new Draw[source.attachments.Length];
            for (int i = 0; i < draws.Length; i++)
            {
                var a = source.attachments[i]; if (a.kind == "clipping") continue;
                var go = new GameObject(a.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(skeletonRoot, false);
                var mesh = new Mesh { name = a.name + " original weighted geometry" }; mesh.MarkDynamic();
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var draw = go.GetComponent<MeshRenderer>(); draw.sharedMaterial = asset.Material;
                draws[i] = new Draw { Mesh = mesh, Renderer = draw };
            }
            ApplyPose(null, 0);
        }

        public bool Play(string name, bool loopAnimation, bool restart = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap13SkeletonView));
            var next = source.animations.FirstOrDefault(a => a.name == name); if (next == null) return false;
            if (animation == next && loop == loopAnimation && !restart) return true;
            animation = next; elapsed = 0; loop = loopAnimation; IsAnimationFinished = false; ApplyPose(next, 0); return true;
        }
        public void Tick(float deltaTime)
        {
            if (animation == null || disposed || IsAnimationFinished || deltaTime <= 0) return;
            elapsed += deltaTime; float time = elapsed;
            if (loop && animation.duration > 0) time = Mathf.Repeat(time, animation.duration);
            else if (time >= animation.duration) { time = animation.duration; IsAnimationFinished = true; }
            ApplyPose(animation, time);
        }
        public Matrix4x4 BoneWorldMatrix(int bone) => skeletonRoot.localToWorldMatrix * matrices[bone];

        public void ApplyPose(NativeMap13Animation pose, float time)
        {
            ResetPose(); ApplyAnimation(pose, time, 1); UpdateGeometry();
        }

        // Independently implemented from the documented Mix Next operation:
        // apply the outgoing pose, then interpolate the incoming keyed channels.
        // https://us.esotericsoftware.com/spine-unity-main-components-4.2
        public void ApplyBlend(NativeMap13Animation previous, float previousTime,
            NativeMap13Animation next, float nextTime, float incomingWeight)
        {
            if (float.IsNaN(incomingWeight) || incomingWeight < 0 || incomingWeight > 1)
                throw new ArgumentOutOfRangeException(nameof(incomingWeight));
            ResetPose(); ApplyAnimation(previous, previousTime, 1);
            ApplyAnimation(next, nextTime, incomingWeight); UpdateGeometry();
        }

        private void ResetPose()
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap13SkeletonView));
            for (int i = 0; i < matrices.Length; i++)
            {
                var b = source.bones[i]; positions[i] = new Vector2(b.x, b.y) * source.scale;
                scales[i] = new Vector2(b.scaleX, b.scaleY); rotations[i] = b.rotation; shears[i] = new Vector2(b.shearX, b.shearY);
            }
            for (int i = 0; i < attachments.Length; i++)
            {
                attachments[i] = source.slots[i].attachment; order[i] = i;
                if (source.slots[i].blend != "normal" || !ColorUtility.TryParseHtmlString("#" + source.slots[i].color, out colors[i]))
                    throw new InvalidOperationException("Original prop slot blend/color differs.");
            }
        }
        private void ApplyAnimation(NativeMap13Animation pose, float time, float weight)
        {
            if (pose != null && weight > 0)
            {
                foreach (var track in pose.tracks)
                {
                    if (track.keys.Length == 0 || time < track.keys[0].time) continue;
                    if (track.Curve == null) throw new InvalidOperationException("Original prop curve is not imported.");
                    float v = track.Curve.Evaluate(time); int i = track.index, axis = track.axis;
                    if (track.kind == 0 && axis == 2)
                    {
                        float target = source.bones[i].rotation + v;
                        rotations[i] = weight == 1 ? target : rotations[i] + Mathf.DeltaAngle(rotations[i], target) * weight;
                    }
                    else if (track.kind == 0)
                    {
                        var p = positions[i]; var b = source.bones[i];
                        float target = (axis == 0 ? b.x : b.y) * source.scale + v * source.scale;
                        p[axis] = weight == 1 ? target : Mathf.LerpUnclamped(p[axis], target, weight); positions[i] = p;
                    }
                    else if (track.kind == 1)
                    {
                        var s = scales[i]; var b = source.bones[i]; float target = (axis == 0 ? b.scaleX : b.scaleY) * v;
                        s[axis] = weight == 1 ? target : Mathf.LerpUnclamped(s[axis], target, weight); scales[i] = s;
                    }
                    else if (track.kind == 2)
                    { var c = colors[i]; c[axis] = weight == 1 ? v : Mathf.LerpUnclamped(c[axis], v, weight); colors[i] = c; }
                    else if (track.kind == 4)
                    {
                        var s = shears[i]; var b = source.bones[i]; float target = (axis == 0 ? b.shearX : b.shearY) + v;
                        s[axis] = weight == 1 ? target : Mathf.LerpUnclamped(s[axis], target, weight); shears[i] = s;
                    }
                    else throw new InvalidOperationException("Unsupported original prop timeline.");
                }
                foreach (var key in pose.attachments) if (key.time <= time) attachments[key.slot] = key.attachment;
                foreach (var key in pose.drawOrders) if (key.time <= time) Array.Copy(key.slots, order, order.Length);
            }
        }
        private void UpdateGeometry()
        {
            for (int i = 0; i < matrices.Length; i++)
            {
                var b = source.bones[i]; if (b.parent >= i) throw new InvalidOperationException("Bone parent order differs.");
                var parent = b.parent < 0 ? Matrix4x4.identity : matrices[b.parent];
                matrices[i] = PlanarMatrix(parent, positions[i], rotations[i], scales[i], shears[i], b.inherit);
            }
            DrawGeometry();
        }

        private static Vector2 Direction(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        public static Matrix4x4 PlanarMatrix(Matrix4x4 parent, Vector2 position, float rotation, Vector2 scale, Vector2 shear, string inheritance)
        {
            // A two-column affine basis preserves shear exactly, unlike Unity TRS.
            Vector2 x, y;
            if (inheritance == "normal" || string.IsNullOrEmpty(inheritance))
            {
                x = parent.MultiplyVector(Direction(rotation + shear.x) * scale.x);
                y = parent.MultiplyVector(Direction(rotation + 90 + shear.y) * scale.y);
            }
            else if (inheritance == "onlyTranslation")
            { x = Direction(rotation + shear.x) * scale.x; y = Direction(rotation + 90 + shear.y) * scale.y; }
            else if (inheritance == "noScale")
            {
                // Preserve the parent's transformed direction and reflection while removing
                // its axis lengths. Local shear/scale then acts in that orthonormal basis.
                Vector2 u = parent.MultiplyVector(Direction(rotation));
                float length = u.magnitude; u = length > 1e-7f ? u / length : Vector2.zero;
                float reflection = parent.m00 * parent.m11 - parent.m01 * parent.m10 < 0 ? -1 : 1;
                Vector2 v = new Vector2(-u.y, u.x) * reflection;
                Vector2 a = Direction(shear.x) * scale.x, b = Direction(90 + shear.y) * scale.y;
                x = u * a.x + v * a.y; y = u * b.x + v * b.y;
            }
            else throw new InvalidOperationException("Unrestored original bone inheritance: " + inheritance);
            Vector3 p = parent.MultiplyPoint3x4(position);
            var result = Matrix4x4.identity; result.m00 = x.x; result.m10 = x.y; result.m01 = y.x; result.m11 = y.y;
            result.m03 = p.x; result.m13 = p.y; return result;
        }

        private Vector2[] Points(NativeMap13Attachment a)
        {
            var points = new Vector2[a.vertices.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var vertex = a.vertices[i]; Vector3 p = Vector3.zero;
                for (int j = 0; j < vertex.count; j++)
                {
                    var w = a.influences[vertex.first + j];
                    p += matrices[w.bone].MultiplyPoint3x4(new Vector3(w.x, w.y) * source.scale) * w.weight;
                }
                points[i] = p;
            }
            return points;
        }
        private void DrawGeometry()
        {
            foreach (var draw in draws) if (draw != null) draw.Renderer.enabled = false;
            List<CombineInstance>[] segments = nativeTargets == null ? null :
                Enumerable.Range(0, nativeTargets.Length).Select(_ => new List<CombineInstance>()).ToArray();
            int segment = 0;
            List<Vector2[]> clipping = null; int clipEnd = -1; VisibleTriangles = 0;
            bool anyBounds = false; Bounds bounds = default;
            for (int drawOrder = 0; drawOrder < order.Length; drawOrder++)
            {
                int slot = order[drawOrder], id = attachments[slot];
                if (id >= 0)
                {
                    var a = source.attachments[id]; var points = Points(a);
                    if (a.kind == "clipping")
                    {
                        if (profile.useClipping && clipping == null) { clipping = ConvexPieces(points); clipEnd = a.endSlot; }
                        if (separatorSlots != null && separatorSlots.Contains(slot)) segment++;
                        continue;
                    }
                    var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
                    for (int i = 0; i < a.triangles.Length; i += 3)
                    {
                        var triangle = new List<Vertex>();
                        for (int j = 0; j < 3; j++) { int v = a.triangles[i + j]; triangle.Add(new Vertex(points[v], new Vector2(a.uv[v * 2], a.uv[v * 2 + 1]))); }
                        void Append(List<Vertex> polygon)
                        {
                            int first = vertices.Count;
                            foreach (var v in polygon)
                            {
                                var p = new Vector3(v.Point.x, v.Point.y, drawOrder * profile.zSpacing);
                                vertices.Add(p); uv.Add(v.UV);
                                if (!anyBounds) { bounds = new Bounds(p, Vector3.zero); anyBounds = true; } else bounds.Encapsulate(p);
                            }
                            for (int j = 1; j < polygon.Count - 1; j++) { triangles.Add(first); triangles.Add(first + j); triangles.Add(first + j + 1); }
                        }
                        if (clipping == null) Append(triangle);
                        else foreach (var piece in clipping) Append(Clip(triangle, piece));
                    }
                    var color = colors[slot]; if (profile.pmaVertexColors) { color.r *= color.a; color.g *= color.a; color.b *= color.a; }
                    var draw = draws[id]; draw.Mesh.Clear(); draw.Mesh.SetVertices(vertices); draw.Mesh.SetUVs(0, uv);
                    draw.Mesh.SetColors(Enumerable.Repeat(color, vertices.Count).ToList()); draw.Mesh.SetTriangles(triangles, 0); draw.Mesh.RecalculateBounds();
                    draw.Renderer.sortingOrder = drawOrder;
                    draw.Renderer.enabled = nativeTargets == null && triangles.Count > 0; VisibleTriangles += triangles.Count / 3;
                    if (segments != null && triangles.Count > 0)
                        segments[segment].Add(new CombineInstance { mesh = draw.Mesh,
                            transform = Matrix4x4.Scale(skeletonRoot.localScale) });
                }
                if (slot == clipEnd) { clipping = null; clipEnd = -1; }
                if (separatorSlots != null && separatorSlots.Contains(slot)) segment++;
            }
            if (segments != null)
                for (int i = 0; i < segments.Length; i++)
                {
                    var mesh = nativeMeshes[i]; mesh.Clear();
                    if (segments[i].Count > 0) mesh.CombineMeshes(segments[i].ToArray(), true, true, false);
                    mesh.RecalculateBounds(); nativeTargets[i].enabled = separationEnabled && nativeEnabled[i] && mesh.vertexCount > 0;
                }
            if (fallbackMesh)
            {
                fallbackMesh.Clear();
                var populated = nativeMeshes.Where(m => m.vertexCount > 0).Select(m => new CombineInstance { mesh = m }).ToArray();
                if (populated.Length > 0) fallbackMesh.CombineMeshes(populated, true, false, false);
                fallbackMesh.subMeshCount = nativeMeshes.Length; int offset = 0;
                for (int i = 0; i < nativeMeshes.Length; i++)
                {
                    fallbackMesh.SetTriangles(nativeMeshes[i].triangles.Select(index => index + offset).ToArray(), i, false);
                    offset += nativeMeshes[i].vertexCount;
                }
                fallbackMesh.RecalculateBounds(); fallbackTarget.enabled = !separationEnabled && fallbackMesh.vertexCount > 0;
            }
            LocalBounds = bounds;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static float Area(IReadOnlyList<Vector2> p) { float a = 0; for (int i = 0; i < p.Count; i++) a += Cross(p[i], p[(i + 1) % p.Count]); return a; }
        public static List<Vector2[]> ConvexPieces(Vector2[] boundary)
        {
            var result = new List<Vector2[]>(); if (boundary.Length < 3 || Mathf.Abs(Area(boundary)) < 1e-12f) return result;
            float sign = Area(boundary) < 0 ? -1 : 1; bool convex = true;
            for (int i = 0; i < boundary.Length; i++) if (sign * Cross(boundary[(i + 1) % boundary.Length] - boundary[i], boundary[(i + 2) % boundary.Length] - boundary[(i + 1) % boundary.Length]) < -1e-7f) convex = false;
            if (convex) { result.Add(boundary); return result; }
            var remaining = boundary.ToList();
            while (remaining.Count > 3)
            {
                bool removed = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    Vector2 a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (sign * Cross(b - a, c - b) <= 1e-10f) continue;
                    bool occupied = false;
                    for (int j = 0; j < remaining.Count; j++)
                    {
                        if (j == i || j == (i + 1) % remaining.Count || j == (i + remaining.Count - 1) % remaining.Count) continue;
                        Vector2 p = remaining[j];
                        if (sign * Cross(b - a, p - a) >= -1e-10f && sign * Cross(c - b, p - b) >= -1e-10f && sign * Cross(a - c, p - c) >= -1e-10f) { occupied = true; break; }
                    }
                    if (occupied) continue;
                    result.Add(new[] { a, b, c }); remaining.RemoveAt(i); removed = true; break;
                }
                if (!removed) throw new InvalidOperationException("Original clipping boundary cannot be triangulated without changing its geometry.");
            }
            result.Add(remaining.ToArray()); return result;
        }
        private static List<Vertex> Clip(List<Vertex> input, Vector2[] boundary)
        {
            var polygon = input; float sign = Area(boundary) < 0 ? -1 : 1;
            for (int i = 0; i < boundary.Length && polygon.Count > 0; i++)
            {
                Vector2 a = boundary[i], edge = boundary[(i + 1) % boundary.Length] - a;
                var output = new List<Vertex>(); var previous = polygon[polygon.Count - 1]; float pd = sign * Cross(edge, previous.Point - a);
                foreach (var current in polygon)
                {
                    float cd = sign * Cross(edge, current.Point - a);
                    if ((pd >= 0) != (cd >= 0)) { float t = pd / (pd - cd); output.Add(new Vertex(Vector2.LerpUnclamped(previous.Point, current.Point, t), Vector2.LerpUnclamped(previous.UV, current.UV, t))); }
                    if (cd >= 0) output.Add(current); previous = current; pd = cd;
                }
                polygon = output;
            }
            return polygon;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var draw in draws) if (draw != null) UnityEngine.Object.Destroy(draw.Mesh);
            if (nativeMeshes != null)
                for (int i = 0; i < nativeMeshes.Length; i++)
                {
                    if (nativeTargets[i] && nativeTargets[i].GetComponent<MeshFilter>().sharedMesh == nativeMeshes[i])
                        nativeTargets[i].GetComponent<MeshFilter>().sharedMesh = null;
                    UnityEngine.Object.Destroy(nativeMeshes[i]);
                }
            if (fallbackMesh)
            {
                if (fallbackTarget && fallbackTarget.GetComponent<MeshFilter>().sharedMesh == fallbackMesh)
                    fallbackTarget.GetComponent<MeshFilter>().sharedMesh = null;
                UnityEngine.Object.Destroy(fallbackMesh);
            }
            if (Transform) UnityEngine.Object.Destroy(Transform.gameObject);
        }
    }
}
