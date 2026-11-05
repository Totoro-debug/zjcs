using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Official.BattleSpinePlayables;
using Zjcs.Official.StoryAttachment;

namespace Zjcs.Offline.ProfessionUI
{
    // Original attachment branches and application followers, with independent
    // geometry for the two verified direct-child bone followers.
    public sealed class ProfessionNativeBattlePlayerAttachments
    {
        public AttachAgent Agent { get; }
        public IReadOnlyDictionary<string, Transform> Transforms => transforms;
        public IReadOnlyList<AttachmentFollower> Followers => followers;
        public IReadOnlyList<ProfessionNativePlayerBoneFollower> BoneFollowers => boneFollowers;
        private readonly Dictionary<string, Transform> transforms = new Dictionary<string, Transform>();
        private readonly List<AttachmentFollower> followers = new List<AttachmentFollower>();
        private readonly List<ProfessionNativePlayerBoneFollower> boneFollowers = new List<ProfessionNativePlayerBoneFollower>();

        public ProfessionNativeBattlePlayerAttachments(ProfessionNativeBattlePlayerActor actor)
        {
            if (actor?.Renderer?.State == null) throw new ArgumentException("Initialize the original player appearance before attachment followers.");
            var asset = Resources.Load<TextAsset>("Combat/player-attachments");
            if (!asset) throw new InvalidOperationException("Original 3100 attachment data is missing.");
            var data = Obj(OfficialEquipmentJson.Parse(asset.text));
            if (!Equals(data["manifest"], "884_156692") || Convert.ToInt32(data["entityId"]) != 3100)
                throw new InvalidOperationException("Original player attachment source differs.");
            var nodes = Arr(data["nodes"]).Select(Obj).ToArray();
            var objects = new Dictionary<string, GameObject>();
            var renderers = new Dictionary<string, Component>();
            foreach (var row in nodes)
            {
                string path = (string)row["path"], key = (string)row["sourceKey"];
                var transform = path.Length == 0 ? actor.Root.transform : actor.Root.transform.Find(path);
                if (!transform)
                {
                    var go = Equals(row["transformType"], "RectTransform")
                        ? new GameObject((string)row["name"], typeof(RectTransform)) : new GameObject((string)row["name"]);
                    transform = go.transform;
                    transform.SetParent(transforms[(string)row["parentKey"]], false);
                }
                if ((transform is RectTransform) != Equals(row["transformType"], "RectTransform"))
                    throw new InvalidOperationException("Original player attachment Transform type differs: " + path);
                transforms.Add(key, transform); objects.Add((string)row["gameObjectKey"], transform.gameObject);
                var fields = Obj(row["fields"]);
                transform.gameObject.layer = Convert.ToInt32(row["layer"]);
                transform.localPosition = Vector(fields["m_LocalPosition"]); transform.localScale = Vector(fields["m_LocalScale"]);
                var q = Obj(fields["m_LocalRotation"]);
                transform.localRotation = new Quaternion(Number(q, "x"), Number(q, "y"), Number(q, "z"), Number(q, "w"));
                if (transform is RectTransform rect)
                {
                    rect.anchorMin = Vector2(fields["m_AnchorMin"]); rect.anchorMax = Vector2(fields["m_AnchorMax"]);
                    rect.pivot = Vector2(fields["m_Pivot"]); rect.sizeDelta = Vector2(fields["m_SizeDelta"]);
                    rect.anchoredPosition = Vector2(fields["m_AnchoredPosition"]);
                }
                if (path.Length != 0) transform.SetSiblingIndex(Convert.ToInt32(row["siblingIndex"]));
                transform.gameObject.SetActive(Flag(row, "active"));
                foreach (var component in Arr(row["components"]).Select(Obj))
                    if (Equals(component["type"], "SkeletonMecanim"))
                    {
                        if (transform != actor.Renderer.transform) throw new InvalidOperationException("Bind the original player renderer cross-reference.");
                        renderers.Add((string)component["sourceKey"], actor.Renderer);
                    }
            }
            foreach (var row in nodes)
            foreach (var component in Arr(row["components"]).Select(Obj))
            {
                if (!component.TryGetValue("fields", out var raw)) continue;
                var fields = Obj(raw); var references = Obj(component["references"]);
                var go = transforms[(string)row["sourceKey"]].gameObject;
                if (Equals(component["type"], "AttachAgent"))
                {
                    if (Agent || go != actor.Root) throw new InvalidOperationException("Original player attachment registry owner differs.");
                    Agent = go.AddComponent<AttachAgent>();
                    Agent.myBoneNameList = Arr(fields["myBoneNameList"]).Cast<string>().ToList();
                    Agent.myBoneList = Arr(fields["myBoneList"]).Select((_, index) =>
                        references.TryGetValue("/myBoneList/" + index, out var target)
                            ? transforms[(string)Obj(target)["sourceKey"]] : null).ToList();
                    Agent.enabled = Flag(fields, "m_Enabled"); Agent.Init();
                }
                else if (Equals(component["type"], "AttachmentFollower"))
                {
                    // Only this follower branch is inactive during injection;
                    // the already initialized player retains its lifecycle.
                    bool active = go.activeSelf; go.SetActive(false);
                    try
                    {
                        var follower = go.AddComponent<AttachmentFollower>();
                        follower.SkeletonRenderer = Resolve(renderers, references, "/SkeletonRenderer");
                        follower.SkeletonGraphic = Resolve(renderers, references, "/SkeletonGraphic");
                        follower.BoneName = (string)fields["BoneName"]; follower.SlotName = (string)fields["SlotName"];
                        follower.AttachmentName = (string)fields["AttachmentName"];
                        follower.Gos = Arr(fields["Gos"]).Select((_, index) => objects[(string)Obj(references["/Gos/" + index])["sourceKey"]]).ToArray();
                        follower.FollowZSpacing = Flag(fields, "FollowZSpacing"); follower.ZFront = Flag(fields, "ZFront");
                        follower.FollowLocalScale = Flag(fields, "FollowLocalScale"); follower.FollowRotation = Flag(fields, "FollowRotation");
                        follower.FollowSkeletonTransformFlipX = Flag(fields, "FollowSkeletonTransformFlipX");
                        follower.InitializeOnAwake = Flag(fields, "InitializeOnAwake"); follower.enabled = Flag(fields, "m_Enabled");
                        followers.Add(follower);
                    }
                    finally { go.SetActive(active); }
                }
                else if (Equals(component["type"], "BoneFollower"))
                {
                    var renderer = Resolve(renderers, references, "/skeletonRenderer") as ProfessionNativeSceneRenderer;
                    bool active = go.activeSelf; go.SetActive(false);
                    try
                    {
                        var follower = go.AddComponent<ProfessionNativePlayerBoneFollower>();
                        follower.Bind(renderer, fields); follower.enabled = Flag(fields, "m_Enabled");
                        boneFollowers.Add(follower);
                    }
                    finally { go.SetActive(active); }
                }
            }
            if (!Agent || Agent.myBoneNameList.Count != 49 || followers.Count != 3 || boneFollowers.Count != 2)
                throw new InvalidOperationException("Original player attachment set differs.");
        }

        public void RebindOriginalShadow(Transform shadow)
        {
            if (!shadow) throw new ArgumentNullException(nameof(shadow));
            var previous = transforms.Values.Single(value => value != shadow && value.name == shadow.name
                && value.parent == shadow.parent);
            var replacements = new Dictionary<Transform, Transform>();
            foreach (var old in transforms.Values.Where(value => value == previous || value.IsChildOf(previous)))
            {
                var path = new List<string>();
                for (var node = old; node != previous; node = node.parent) path.Insert(0, node.name);
                var current = path.Count == 0 ? shadow : shadow.Find(string.Join("/", path));
                if (!current) throw new InvalidOperationException("Original player shadow replacement hierarchy differs.");
                replacements.Add(old, current);
            }
            foreach (string key in transforms.Keys.ToArray())
                if (replacements.TryGetValue(transforms[key], out var current)) transforms[key] = current;
            for (int index = 0; index < Agent.myBoneList.Count; index++)
                if (Agent.myBoneList[index] && replacements.TryGetValue(Agent.myBoneList[index], out var current))
                    Agent.myBoneList[index] = current;
            Agent.Init();
            followers.RemoveAll(value => value && (value.transform == previous || value.transform.IsChildOf(previous)));
            followers.AddRange(shadow.GetComponentsInChildren<AttachmentFollower>(true));
            previous.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(previous.gameObject);
            else UnityEngine.Object.DestroyImmediate(previous.gameObject);
        }

        private static Component Resolve(Dictionary<string, Component> renderers, Dictionary<string, object> references, string path)
            => references.TryGetValue(path, out var target) ? renderers[(string)Obj(target)["sourceKey"]] : null;
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static bool Flag(Dictionary<string, object> row, string key) => Convert.ToBoolean(row[key]);
        private static float Number(Dictionary<string, object> row, string key) => Convert.ToSingle(row[key]);
        private static Vector3 Vector(object value) { var row = Obj(value); return new Vector3(Number(row, "x"), Number(row, "y"), Number(row, "z")); }
        private static Vector2 Vector2(object value) { var row = Obj(value); return new Vector2(Number(row, "x"), Number(row, "y")); }
    }
}
