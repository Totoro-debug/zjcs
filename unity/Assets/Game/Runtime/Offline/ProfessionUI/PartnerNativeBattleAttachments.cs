using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Official.BattleSpinePlayables;
using Zjcs.Official.StoryAttachment;

namespace Zjcs.Offline.ProfessionUI
{
    // Restore original Transform data and reuse the migrated application bindings.
    // Other prefab components are recorded in the source graph, not synthesized here.
    public sealed class PartnerNativeBattleAttachments
    {
        internal readonly struct RendererBinding
        {
            internal readonly Transform Transform;
            internal readonly ProfessionNativeAppearanceSkeleton Skeleton;
            internal readonly Dictionary<string, object> Settings;
            internal readonly Material Material;
            internal RendererBinding(Transform transform, ProfessionNativeAppearanceSkeleton skeleton,
                Dictionary<string, object> settings, Material material)
            { Transform = transform; Skeleton = skeleton; Settings = settings; Material = material; }
        }
        private readonly List<AttachmentFollower> followers = new List<AttachmentFollower>();
        private readonly Dictionary<string, Transform> transforms;
        public AttachAgent Agent { get; }
        public Dictionary<string, object> OriginalRow { get; }
        public IReadOnlyList<AttachmentFollower> Followers => followers;
        public IReadOnlyDictionary<string, Component> OriginalRenderers { get; }

        internal PartnerNativeBattleAttachments(PartnerNativeBattleActor actor,
            Dictionary<string, Transform> transforms, RendererBinding[] bindings)
        {
            this.transforms = transforms;
            var asset = Resources.Load<TextAsset>("Combat/NativePartnerAssets/attachments");
            if (!asset) throw new FileNotFoundException("Original partner attachment graph is not packaged.");
            var root = Obj(OfficialEquipmentJson.Parse(asset.text));
            OriginalRow = Arr(root["partners"]).Select(Obj).Single(row => Int(row, "npcId") == Int(actor.OriginalRow, "npcId"));
            var nodes = Arr(OriginalRow["nodes"]).Select(Obj).ToArray();
            var renderers = new Dictionary<string, Component>(StringComparer.Ordinal);
            bool active = actor.Root.gameObject.activeSelf;
            actor.Root.gameObject.SetActive(false);
            try
            {
                foreach (var row in nodes)
                {
                    string key = (string)row["sourceKey"];
                    string parentKey = (string)row["parentKey"];
                    Transform parent = parentKey.Length == 0 ? actor.Root : transforms[parentKey];
                    if (!transforms.TryGetValue(key, out Transform transform))
                    {
                        transform = (Equals(row["transformType"], "RectTransform")
                            ? new GameObject((string)row["name"], typeof(RectTransform)) : new GameObject((string)row["name"])).transform;
                        transforms.Add(key, transform);
                    }
                    if (transform.parent != parent) transform.SetParent(parent, false);
                    transform.name = (string)row["name"]; transform.gameObject.layer = Int(row, "layer");
                    transform.localPosition = Vector(row["position"]); transform.localScale = Vector(row["scale"]);
                    var q = Obj(row["rotation"]);
                    transform.localRotation = new Quaternion(Number(q, "x"), Number(q, "y"), Number(q, "z"), Number(q, "w"));
                    if (row.TryGetValue("rect", out object rectValue))
                    {
                        var rect = transform as RectTransform ?? throw new InvalidDataException("Original RectTransform type differs.");
                        var data = Obj(rectValue);
                        rect.anchorMin = Vector2(data["m_AnchorMin"]); rect.anchorMax = Vector2(data["m_AnchorMax"]);
                        rect.pivot = Vector2(data["m_Pivot"]); rect.sizeDelta = Vector2(data["m_SizeDelta"]);
                        rect.anchoredPosition = Vector2(data["m_AnchoredPosition"]);
                    }
                    transform.gameObject.SetActive(Flag(row, "active"));
                    foreach (var component in Arr(row["components"]).Select(Obj))
                    {
                        if (!Equals(component["type"], "SkeletonMecanim") && !Equals(component["type"], "SkeletonAnimation")) continue;
                        var binding = bindings.Single(value => value.Transform == transform);
                        var port = transform.gameObject.AddComponent<PartnerNativeBattleRendererPort>();
                        port.Bind(binding.Skeleton, binding.Material, Int(binding.Settings, "updateWhenInvisible"),
                            Number(binding.Settings, "zSpacing"), (string)component["skeletonDataName"], actor.ApplyPose);
                        renderers.Add((string)component["sourceKey"], port);
                    }
                }
                foreach (var row in nodes) transforms[(string)row["sourceKey"]].SetSiblingIndex(Int(row, "siblingIndex"));
                foreach (var row in nodes)
                foreach (var component in Arr(row["components"]).Select(Obj))
                {
                    if (!component.TryGetValue("fields", out object fieldsValue)) continue;
                    var fields = Obj(fieldsValue); var references = Obj(component["references"]);
                    var go = transforms[(string)row["sourceKey"]].gameObject;
                    if (Equals(component["type"], "AttachAgent"))
                    {
                        if (Agent) throw new InvalidDataException("Multiple original partner attachment registries.");
                        Agent = go.AddComponent<AttachAgent>();
                        Agent.myBoneNameList = Arr(fields["myBoneNameList"]).Select(value => (string)value).ToList();
                        Agent.myBoneList = Arr(fields["myBoneList"]).Select((value, index) =>
                            references.TryGetValue("/myBoneList/" + index, out object reference)
                                ? transforms[(string)Obj(reference)["sourceKey"]] : null).ToList();
                        Agent.enabled = Flag(fields, "m_Enabled"); Agent.Init();
                    }
                    else if (Equals(component["type"], "AttachmentFollower"))
                    {
                        var follower = go.AddComponent<AttachmentFollower>();
                        follower.SkeletonRenderer = Resolve(renderers, references, "/SkeletonRenderer");
                        follower.SkeletonGraphic = Resolve(renderers, references, "/SkeletonGraphic");
                        follower.BoneName = (string)fields["BoneName"]; follower.SlotName = (string)fields["SlotName"];
                        follower.AttachmentName = (string)fields["AttachmentName"];
                        if (Arr(fields["Gos"]).Count != 0) throw new NotSupportedException("Bind original follower visibility targets.");
                        follower.Gos = Array.Empty<GameObject>();
                        follower.FollowZSpacing = Flag(fields, "FollowZSpacing"); follower.ZFront = Flag(fields, "ZFront");
                        follower.FollowLocalScale = Flag(fields, "FollowLocalScale"); follower.FollowRotation = Flag(fields, "FollowRotation");
                        follower.FollowSkeletonTransformFlipX = Flag(fields, "FollowSkeletonTransformFlipX");
                        follower.InitializeOnAwake = Flag(fields, "InitializeOnAwake"); follower.enabled = Flag(fields, "m_Enabled");
                        followers.Add(follower);
                    }
                }
                OriginalRenderers = renderers;
            }
            finally { actor.Root.gameObject.SetActive(active); }
            if (!Agent) throw new InvalidDataException("Original partner attachment registry is absent.");
            foreach (var follower in followers) if (follower.InitializeOnAwake) follower.Initialize();
            UpdateFollowers();
        }
        public void UpdateFollowers()
        {
            foreach (var follower in followers)
                if (follower && follower.isActiveAndEnabled) follower.ForceUpdate();
        }
        public void RebindOriginalShadow(Transform shadow)
        {
            if (!shadow) throw new ArgumentNullException(nameof(shadow));
            Transform previous = transforms.Values.Single(value => value != shadow && value.name == shadow.name
                && value.parent == shadow.parent);
            var replacements = new Dictionary<Transform, Transform>();
            foreach (string key in transforms.Keys.ToArray())
            {
                Transform old = transforms[key];
                if (old != previous && !old.IsChildOf(previous)) continue;
                var path = new List<string>();
                for (Transform part = old; part != previous; part = part.parent) path.Insert(0, part.name);
                Transform current = path.Count == 0 ? shadow : shadow.Find(string.Join("/", path));
                if (!current) throw new InvalidDataException("Original partner shadow hierarchy differs.");
                replacements.Add(old, current); transforms[key] = current;
            }
            for (int index = 0; index < Agent.myBoneList.Count; index++)
                if (Agent.myBoneList[index] && replacements.TryGetValue(Agent.myBoneList[index], out var current))
                    Agent.myBoneList[index] = current;
            Agent.Init();
            followers.RemoveAll(value => value && (value.transform == previous || value.transform.IsChildOf(previous)));
            followers.AddRange(shadow.GetComponentsInChildren<AttachmentFollower>(true));
            previous.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(previous.gameObject);
            else UnityEngine.Object.DestroyImmediate(previous.gameObject);
            UpdateFollowers();
        }
        private static Component Resolve(Dictionary<string, Component> values, Dictionary<string, object> references, string path)
            => references.TryGetValue(path, out object reference) ? values[(string)Obj(reference)["sourceKey"]] : null;
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
        private static Vector3 Vector(object value) { var v = Obj(value); return new Vector3(Number(v, "x"), Number(v, "y"), Number(v, "z")); }
        private static Vector2 Vector2(object value) { var v = Obj(value); return new Vector2(Number(v, "x"), Number(v, "y")); }
    }
}
