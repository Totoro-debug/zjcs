using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

namespace Zjcs.Presentation.Opening
{
    public sealed class OpeningGrapplingLineEffect : MonoBehaviour
    {
        private enum State { Clear, Grappling, Holding, Dragging, Done }

        private struct Node
        {
            public Vector2 Position;
            public Vector2 LastPosition;
            public Vector2 Direction;
            public float Z;

            public Node(Vector3 position, Vector2 direction)
            {
                Position = position;
                LastPosition = position;
                Direction = direction;
                Z = position.z;
            }
        }

        private readonly List<Node> nodes = new List<Node>();
        private readonly List<GameObject> anchors = new List<GameObject>();
        private LineRenderer line;
        private Transform startAnchor;
        private Transform targetAnchor;
        private bool followStart;
        private float outDuration;
        private float holdDuration;
        private float backDuration;
        private float linkLength;
        private float diffusionAngle;
        private float diffusionStrength;
        private int linkDensity;
        private int stiffness;
        private float stateStart;
        private float stateDuration;
        private State state;
        private Vector3 origin;
        private Vector3 target;

        public static bool Play(OpeningSpecialEffectRuntime runtime, Dictionary<string, object> entry,
            Dictionary<string, object> sourceEvent, Dictionary<string, object> fields,
            SkeletonAnimation source, SkeletonAnimation targetUnit, Vector3 sourcePosition,
            Vector3 targetPosition, Material lineMaterial, float eventSpeed)
        {
            if (runtime == null || lineMaterial == null
                || !sourceEvent.TryGetValue("grappling_line", out object rawLine)
                || !(rawLine is Dictionary<string, object> lineData)) return false;

            Dictionary<string, object> rendererData = OpeningJson.Object(lineData["line_renderer"], "grapple line renderer");
            var root = new GameObject("OpeningGrapplingLine_11830");
            root.transform.SetParent(runtime.transform, false);
            var effect = root.AddComponent<OpeningGrapplingLineEffect>();
            effect.Configure(rendererData, lineMaterial);
            Dictionary<string, object> startConfig = OpeningJson.Object(fields["mStartAttachPos"], "grapple start attachment");
            Dictionary<string, object> targetConfig = OpeningJson.Object(fields["mTargetAttachPos"], "grapple target attachment");
            effect.startAnchor = effect.CreateAnchor("Start", startConfig, source, targetUnit, sourcePosition);
            effect.targetAnchor = effect.CreateAnchor("Target", targetConfig, source, targetUnit, targetPosition);
            effect.followStart = OpeningJson.Bool(fields, "mFollowStartAttach");
            effect.linkDensity = 3;
            effect.linkLength = 0.1f;
            effect.stiffness = 10;
            effect.diffusionAngle = 90f;
            effect.diffusionStrength = 100f;
            float speed = Mathf.Clamp(eventSpeed, 0.01f, 3f);
            float delay = Mathf.Max(0f, (float)OpeningJson.Number(entry, "eventTime")) / speed;
            float outSetting = (float)OpeningJson.Number(fields, "mOutDuration", 10f);
            float backSetting = (float)OpeningJson.Number(fields, "mBackDuration", 0.2f);
            effect.outDuration = outSetting > 0f ? outSetting : 0f;
            effect.holdDuration = Mathf.Max(0f, (float)OpeningJson.Number(fields, "mStandDuration", 0.2f));
            effect.backDuration = backSetting >= 0f ? backSetting : 0f;
            if (outSetting <= 0f)
                effect.outDuration = Mathf.Max(0.00001f,
                    Vector3.Distance(sourcePosition, targetPosition) / Mathf.Max(0.00001f,
                        (float)OpeningJson.Number(fields, "mOutSpeed", 0f)));
            if (backSetting < 0f)
                effect.backDuration = Mathf.Max(0.00001f,
                    Vector3.Distance(sourcePosition, targetPosition) / Mathf.Max(0.00001f,
                        (float)OpeningJson.Number(fields, "mBackSpeed", 0f)));
            effect.outDuration /= speed;
            effect.holdDuration /= speed;
            effect.backDuration /= speed;
            effect.StartCoroutine(effect.BeginAfter(delay));
            return true;
        }

        private void Configure(Dictionary<string, object> source, Material material)
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.enabled = OpeningJson.Bool(source, "enabled", true);
            line.useWorldSpace = OpeningJson.Bool(source, "useWorldSpace", true);
            line.loop = OpeningJson.Bool(source, "loop");
            line.sortingLayerID = 0;
            line.sortingOrder = 25000 + OpeningJson.Int(source, "sortingOrder");
            line.widthMultiplier = (float)OpeningJson.Number(source, "widthMultiplier", 0.1f);
            line.widthCurve = ReadCurve(OpeningJson.Object(source["widthCurve"], "grapple width curve"));
            line.colorGradient = ReadGradient(OpeningJson.Object(source["colorGradient"], "grapple color gradient"));
            line.textureMode = (LineTextureMode)OpeningJson.Int(source, "textureMode");
            line.textureScale = Vector2Value(source, "textureScale", Vector2.one);
            line.alignment = (LineAlignment)OpeningJson.Int(source, "alignment");
            line.numCapVertices = OpeningJson.Int(source, "numCapVertices");
            line.numCornerVertices = OpeningJson.Int(source, "numCornerVertices");
            line.generateLightingData = OpeningJson.Bool(source, "generateLightingData");
            line.shadowBias = (float)OpeningJson.Number(source, "shadowBias", 0.5f);
            line.sharedMaterial = material;
        }

        private Transform CreateAnchor(string name, Dictionary<string, object> config,
            SkeletonAnimation source, SkeletonAnimation targetUnit, Vector3 fallback)
        {
            int attachTo = OpeningJson.Int(config, "AttachTo");
            SkeletonAnimation owner = attachTo == 0 ? source : attachTo == 1 ? targetUnit : null;
            var anchor = new GameObject("GrappleAnchor_" + name);
            anchors.Add(anchor);
            if (owner == null)
            {
                anchor.transform.position = fallback + ReadVector(config, "myOffset");
                return anchor.transform;
            }
            anchor.transform.SetParent(owner.transform, false);
            anchor.transform.localPosition = Vector3.zero;
            string boneName = OpeningJson.String(config, "myBoneName");
            // AttachPosRotCfg.GetBone falls back to the owner's transform when an
            // attachment alias is unavailable; an invalid BoneFollower retries forever.
            if (owner.Skeleton != null && owner.Skeleton.FindBone(boneName) != null)
            {
                var follower = anchor.AddComponent<BoneFollower>();
                follower.skeletonRenderer = owner;
                follower.boneName = boneName;
                follower.followXYPosition = true;
                follower.followZPosition = true;
                follower.followBoneRotation = false;
                follower.followLocalScale = OpeningJson.Bool(config, "myOffsetScale");
                follower.Initialize();
            }
            var offset = new GameObject("Offset");
            offset.transform.SetParent(anchor.transform, false);
            offset.transform.localPosition = ReadVector(config, "myOffset");
            return offset.transform;
        }

        private IEnumerator BeginAfter(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return null;
            if (startAnchor == null || targetAnchor == null || line == null) { Complete(); yield break; }
            origin = startAnchor.position;
            target = targetAnchor.position;
            Vector2 direction = (Vector2)(target - origin);
            float distance = direction.magnitude;
            if (distance <= 0f) { Complete(); yield break; }
            int count = Mathf.Max(2, (int)(distance / linkLength * linkDensity));
            line.positionCount = count;
            nodes.Clear();
            for (int index = 0; index < count; index++)
            {
                float angle = diffusionAngle * Mathf.SmoothStep(1f, 0f, (float)index / count)
                    * Mathf.Pow(-1f, (int)(index * linkLength));
                Vector2 nodeDirection = Rotate(direction.normalized, angle).normalized;
                nodes.Add(new Node(origin, nodeDirection));
                line.SetPosition(index, origin);
            }
            ChangeState(State.Grappling, outDuration);
        }

        private void FixedUpdate()
        {
            if (state == State.Clear || state == State.Done || nodes.Count < 2) return;
            if (followStart && startAnchor != null)
            {
                origin = startAnchor.position;
                Node first = nodes[0];
                first.Position = origin;
                first.LastPosition = origin;
                first.Z = origin.z;
                nodes[0] = first;
                line.SetPosition(0, origin);
            }
            float process = stateDuration > 0f ? (Time.time - stateStart) / stateDuration : 1f;
            switch (state)
            {
                case State.Grappling:
                    Grappling(process, Time.fixedDeltaTime);
                    if (process >= 1f) ChangeState(State.Holding, holdDuration);
                    break;
                case State.Holding:
                    Grappling(1f, Time.fixedDeltaTime);
                    if (process >= 1f) ChangeState(State.Dragging, backDuration);
                    break;
                case State.Dragging:
                    Dragging(process);
                    if (process >= 1f) Complete();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (state == State.Clear || line == null) return;
            for (int index = 1; index < nodes.Count; index++) line.SetPosition(index, Position3D(nodes[index]));
        }

        private void Grappling(float process, float deltaTime)
        {
            int last = nodes.Count - 1;
            Node end = nodes[last];
            end = UpdateNode(end, SmoothStep(end.Position, target, process));
            nodes[last] = end;
            for (int index = 1; index < last; index++)
            {
                Node node = nodes[index];
                Vector2 predicted = node.Position + (node.Position - node.LastPosition);
                Vector2 acceleration = deltaTime * deltaTime * node.Direction;
                Vector2 toTarget = (Vector2)target - node.Position;
                Vector2 next = predicted + acceleration * Mathf.SmoothStep(toTarget.magnitude * diffusionStrength, 0f, process);
                if (IsOverThrough(origin, target, next)) next = target;
                node = UpdateNode(node, next);
                nodes[index] = node;
            }
            for (int pass = 0; pass < stiffness; pass++)
            {
                for (int index = last; index > 0; index--)
                {
                    Node right = nodes[index];
                    Node left = nodes[index - 1];
                    Vector2 delta = left.Position - right.Position;
                    if (delta == Vector2.zero) continue;
                    float magnitude = delta.magnitude;
                    float correction = (magnitude - linkLength) / magnitude;
                    if (index < last && right.Position != (Vector2)target)
                    {
                        right = UpdateNode(right, right.Position + 0.5f * correction * delta, false);
                        nodes[index] = right;
                    }
                    if (index > 1 && left.Position != (Vector2)target)
                    {
                        left = UpdateNode(left, left.Position - 0.5f * correction * delta, false);
                        nodes[index - 1] = left;
                    }
                }
            }
            Vector2 axis = (Vector2)target - (Vector2)origin;
            float distance = axis.magnitude;
            if (distance <= 0f) return;
            for (int index = last; index > 0; index--)
            {
                Node node = nodes[index];
                float along = Vector2.Dot((Vector2)target - node.Position, axis) / distance;
                float t = 1f - along / distance;
                node.Z = Mathf.Lerp(origin.z, target.z, t);
                nodes[index] = node;
            }
        }

        private void Dragging(float process)
        {
            int last = nodes.Count - 1;
            for (int index = 1; index <= last; index++)
            {
                Node node = nodes[index];
                Vector2 next = index == last
                    ? Vector2.Lerp(target, origin, process)
                    : SmoothStep(SmoothStep(node.Position, GetPerpendicularPoint(target, origin, node.Position), process), origin, process);
                node = UpdateNode(node, next);
                nodes[index] = node;
            }
            Vector2 axis = (Vector2)origin - (Vector2)target;
            float distance = axis.magnitude;
            if (distance > 0f)
                for (int index = last; index > 0; index--)
                {
                    Node node = nodes[index];
                    float along = Vector2.Dot((Vector2)origin - node.Position, axis) / distance;
                    node.Z = Mathf.Lerp(target.z, origin.z, 1f - along / distance);
                    nodes[index] = node;
                }
        }

        private void ChangeState(State next, float duration)
        {
            state = next;
            stateDuration = duration;
            stateStart = Time.time;
        }

        private void Complete()
        {
            state = State.Done;
            if (line != null) line.positionCount = 0;
            foreach (GameObject anchor in anchors) if (anchor != null) Destroy(anchor);
            anchors.Clear();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            foreach (GameObject anchor in anchors) if (anchor != null) Destroy(anchor);
            anchors.Clear();
        }

        private static Node UpdateNode(Node node, Vector2 next, bool advance = true)
        {
            if (advance) node.LastPosition = node.Position;
            node.Position = next;
            return node;
        }

        private static Vector3 Position3D(Node node) => new Vector3(node.Position.x, node.Position.y, node.Z);

        private static Vector2 SmoothStep(Vector2 from, Vector2 to, float t)
        {
            t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            return Vector2.Lerp(from, to, t);
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            return new Vector2(direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }

        private static Vector2 GetPerpendicularPoint(Vector2 a, Vector2 b, Vector2 point)
        {
            float x = b.x - a.x, y = b.y - a.y;
            float t = ((point.x - a.x) * x + (point.y - a.y) * y) / (x * x + y * y);
            return new Vector2(a.x + t * x, a.y + t * y);
        }

        private static bool IsOverThrough(Vector3 start, Vector3 end, Vector2 current)
        {
            Vector2 direction = (Vector2)(end - start);
            return Vector2.Dot(current - (Vector2)start, direction) / direction.sqrMagnitude > 1f;
        }

        private static AnimationCurve ReadCurve(Dictionary<string, object> source)
        {
            List<object> rows = OpeningJson.Array(source["m_Curve"], "curve keys");
            var keys = new Keyframe[rows.Count];
            for (int index = 0; index < rows.Count; index++)
            {
                Dictionary<string, object> row = OpeningJson.Object(rows[index], "curve key");
                var key = new Keyframe((float)OpeningJson.Number(row, "time"),
                    (float)OpeningJson.Number(row, "value"), (float)OpeningJson.Number(row, "inSlope"),
                    (float)OpeningJson.Number(row, "outSlope"));
                key.weightedMode = (WeightedMode)OpeningJson.Int(row, "weightedMode");
                key.inWeight = (float)OpeningJson.Number(row, "inWeight", 1f / 3f);
                key.outWeight = (float)OpeningJson.Number(row, "outWeight", 1f / 3f);
                keys[index] = key;
            }
            return new AnimationCurve(keys);
        }

        private static Gradient ReadGradient(Dictionary<string, object> source)
        {
            List<object> rgb = OpeningJson.Array(source["rgb"], "gradient colors");
            List<object> alpha = OpeningJson.Array(source["alpha"], "gradient alpha");
            var colors = new GradientColorKey[rgb.Count];
            var alphas = new GradientAlphaKey[alpha.Count];
            for (int index = 0; index < rgb.Count; index++)
            {
                List<object> row = OpeningJson.Array(rgb[index], "gradient color key");
                colors[index] = new GradientColorKey(new Color((float)OpeningJson.ToNumber(row[1]),
                    (float)OpeningJson.ToNumber(row[2]), (float)OpeningJson.ToNumber(row[3])),
                    (float)OpeningJson.ToNumber(row[0]));
            }
            for (int index = 0; index < alpha.Count; index++)
            {
                List<object> row = OpeningJson.Array(alpha[index], "gradient alpha key");
                alphas[index] = new GradientAlphaKey((float)OpeningJson.ToNumber(row[1]),
                    (float)OpeningJson.ToNumber(row[0]));
            }
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            gradient.mode = OpeningJson.Int(source, "mode") == 1 ? GradientMode.Fixed : GradientMode.Blend;
            return gradient;
        }

        private static Vector2 Vector2Value(Dictionary<string, object> source, string key, Vector2 fallback)
        {
            if (!source.TryGetValue(key, out object raw) || !(raw is List<object> value) || value.Count < 2)
                return fallback;
            return new Vector2((float)OpeningJson.ToNumber(value[0]), (float)OpeningJson.ToNumber(value[1]));
        }

        private static Vector3 ReadVector(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object raw) || !(raw is Dictionary<string, object> value)) return Vector3.zero;
            return new Vector3((float)OpeningJson.Number(value, "x"), (float)OpeningJson.Number(value, "y"),
                (float)OpeningJson.Number(value, "z"));
        }
    }
}
