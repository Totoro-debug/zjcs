using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;
using AttachmentFollower = Zjcs.Official.BattleSpinePlayables.AttachmentFollower;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunShadows()
        {
            var originals = Resources.LoadAll<GameObject>("Combat/CharacterShadows/Prefabs");
            Require(originals.Length == 19 && originals.All(prefab =>
                (prefab.GetComponent<AttachmentFollower>() != null || prefab.GetComponent<OfficialBattleShadowBoneFollower>() != null)
                && prefab.GetComponentsInChildren<SpriteRenderer>(true).All(renderer =>
                    renderer.sprite && renderer.sprite.texture && renderer.sharedMaterial && renderer.sharedMaterial.shader.isSupported)),
                "all 19 original configured shadow prefabs import with explicit native assets and supported followers");
            RunCasts(); SessionState.SetBool(Key + ".shadows", true);
        }

        private static IEnumerator CheckShadows(GameObject actor, Func<int, float, IEnumerator> pump, string label)
        {
            var root = new GameObject("Original shadow shared scene validation");
            var previousParent = actor.transform.parent;
            var previousPosition = actor.transform.localPosition;
            var previousScale = actor.transform.localScale;
            var native = actor.GetComponent<ProfessionNativeSceneRenderer>();
            var state = new FightBattleCheckpoint();
            state.Units.Add(new FightBattleUnitState { Id = 1, CharacterType = 1, Hp = 100, MaxHp = 100 });
            var grid = new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 8, Height = 8 });
            using var scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                _ => null, grid, (_, __) => null, (_, __) => { }, OfficialBattleHudCatalog.LoadDefault(), root.transform);
            try
            {
                scene.Refresh(state);
                scene.AttachUnitTransform(1, actor.transform);
                Transform entity = actor.transform;
                while (entity.parent != root.transform && entity.parent != null) entity = entity.parent;
                Require(entity.parent == root.transform, "production attached role root");
                var shadow = entity.Find("3100_view/ShadowBoneFollower");
                Require(shadow != null, "production scene mounts original shadow subtree: "
                    + string.Join(",", entity.GetComponentsInChildren<Transform>(true).Select(child => child.name)));
                var follower = shadow.GetComponent<AttachmentFollower>();
                Require(follower != null && follower.SkeletonRenderer == native && follower.BoneName == "shadow",
                    "original application follower binds actual native skeleton");
                var sprite = shadow.GetComponentInChildren<SpriteRenderer>();
                Require(sprite && sprite.sprite && sprite.sprite.vertices.Length == 4 && sprite.sprite.triangles.Length == 6
                    && sprite.sprite.texture.mipmapCount == 1 && sprite.sortingOrder == 17990,
                    "original native Sprite geometry, complete source mip chain and flattened layer order: "
                    + (sprite ? sprite.sprite?.name + "/vertices=" + sprite.sprite?.vertices.Length
                        + "/indices=" + sprite.sprite?.triangles.Length + "/mips=" + sprite.sprite?.texture.mipmapCount
                        + "/order=" + sprite.sortingOrder : "missing renderer"));
                var source = (IProfessionFollowerRenderer)native;
                for (int frame = 0; frame < 12; frame++)
                {
                    entity.position += new Vector3(.03f, -.02f, 0);
                    yield return pump(1, .02f);
                    follower.ForceUpdate();
                    var bone = source.Skeleton.FindBone("shadow");
                    var target = native.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0));
                    Require(Vector2.Distance(shadow.position, target) < .0001f,
                        "original shadow tracks animated native bone through actual role movement");
                }
                CaptureShadowPixels(sprite, label + "-original-shadow");

                // Exercise the other actual serialized follower branch without
                // claiming that a sampled monster mesh supplies a live skeleton.
                using (var sdkBranch = OfficialBattleShadowView.Create(3198, root.transform, native))
                {
                    var boneFollower = sdkBranch.Root.GetComponent<OfficialBattleShadowBoneFollower>();
                    Require(boneFollower && boneFollower.skeletonRenderer == native, "original 3198 follower fields bind native bone ports");
                    yield return pump(1, .02f);
                    boneFollower.ForceUpdate();
                    var bone = source.Skeleton.FindBone(boneFollower.boneName);
                    var target = native.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0));
                    Require(Vector2.Distance(boneFollower.transform.position, target) < .0001f
                        && boneFollower.transform.localScale == new Vector3(bone.ScaleX, bone.ScaleY, 1),
                        "restricted original BoneFollower configuration follows live XY and local bone scale");
                }
                scene.DetachUnitTransform(1);
                yield return null;
                Require(shadow == null && actor.transform.parent == previousParent
                    && actor.transform.localPosition == previousPosition && actor.transform.localScale == previousScale,
                    "detach destroys owned shadow and restores borrowed character");
                scene.AttachUnitTransform(1, actor.transform);
                Require(root.GetComponentsInChildren<AttachmentFollower>(true).Length == 1,
                    "reattach mounts exactly one original shadow");
                scene.Dispose();
                yield return null;
                Require(actor.transform.parent == previousParent && root.GetComponentsInChildren<AttachmentFollower>(true).Length == 0,
                    "scene disposal releases all owned shadows and retains borrowed character");
                Debug.Log("[Original battle shadows] " + label + ": production scene attachment, original Sprite/full mip texture, live bone movement, both original follower branches, detach/reattach/disposal and actual shadow pixels passed. Monster native skeleton migration and full visibility host remain separate.");
            }
            finally { scene.Dispose(); Object.DestroyImmediate(root); }
        }

        private static void CaptureShadowPixels(SpriteRenderer sprite, string label)
        {
            var go = new GameObject("Original shadow rendered-pixel probe");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.aspect = 675f / 1463;
            var bounds = sprite.bounds;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.4f;
            camera.transform.position = bounds.center + Vector3.back * 10;
            camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
            var target = RenderTexture.GetTemporary(675, 1463, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(675, 1463, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                Color32[] Draw()
                {
                    camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 675, 1463), 0, 0); image.Apply();
                    return image.GetPixels32();
                }
                var visible = Draw();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), label + ".png"), image.EncodeToPNG());
                sprite.enabled = false;
                var hidden = Draw();
                Require(visible.Where((pixel, i) => !pixel.Equals(hidden[i])).Count() > 100,
                    "actual native shadow contributes rendered pixels");
            }
            finally
            {
                sprite.enabled = true; camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target); Object.Destroy(image); Object.Destroy(go);
            }
        }
    }
}
