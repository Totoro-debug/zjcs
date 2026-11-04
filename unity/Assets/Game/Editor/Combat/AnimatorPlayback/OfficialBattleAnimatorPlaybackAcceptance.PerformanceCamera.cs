using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        private static int performanceCameraCase;
        private static IEnumerator CheckPerformanceCamera(GameObject actor, Func<int, float, IEnumerator> pump)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(OfficialBattleRuntimeController).Assembly.GetType("Zjcs.Presentation.Combat.OfficialBattleCameraView", true);
            var root = new GameObject("Original performance camera host validation");
            root.transform.SetParent(actor.transform.parent, false);
            var outputGo = new GameObject("Actual performance camera output");
            var output = outputGo.AddComponent<Camera>();
            output.enabled = false; output.orthographic = true; output.aspect = 675f / 1463;
            output.clearFlags = CameraClearFlags.SolidColor; output.backgroundColor = Color.clear;
            output.transform.position = actor.transform.position + Vector3.back * 20;
            var view = Activator.CreateInstance(type, hidden, null,
                new object[] { output, root.transform, 100, output.transform.position, new Func<int, Transform>(_ => actor.transform) }, null);
            var host = (IBattleCameraPerformanceHost)view;
            var offsets = (IBattleCameraOffsetHost)view;
            var brain = output.GetComponent<CinemachineBrain>();
            brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.ManualUpdate;
            // Cut only in this diagnostic, so pixel capture observes the selected
            // camera immediately rather than the fixture Brain's default blend.
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0);
            type.GetMethod("SetVisible", hidden).Invoke(view, new object[] { true });
            var normal = (CinemachineVirtualCamera)type.GetField("virtualCamera", hidden).GetValue(view);
            var performance = root.GetComponentsInChildren<CinemachineVirtualCamera>(true).Single(v => v != normal);
            bool Allowed() => (bool)type.GetProperty("AllowsGameObjectInput", hidden).GetValue(view);
            var stage = new GameObject("Original performance apply target fixture");
            stage.transform.SetParent(root.transform, false); stage.SetActive(false);
            var staging = new GameObject("Inactive original camera leaf fixture");
            staging.transform.SetParent(root.transform, false); staging.SetActive(false);
            var prefab = Resources.Load<GameObject>("Combat/SkillGraphs/Prefabs/EcEntity/Fight/FightSkill/15840/playable15840");
            Require(prefab, "original 15840 camera graph resource");
            var original = prefab.GetComponentsInChildren<MyPlayableFightCameraPerformance>(true).Single();
            var template = Object.Instantiate(original.gameObject, staging.transform);
            template.SetActive(false);
            template.GetComponent<MyPlayableFightCameraPerformance>().myApplyComponent = stage.transform;
            var sourcePosition = actor.transform.localPosition;
            using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
            // Exercise the same public host-binding seam as the production controller.
            using var graphPlayback = new OfficialBattleSkillGraphPlayback(
                OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope,
                new OfficialBattleSkillEntityPlayback(root.transform,
                    new OfficialBattleGridMapper(root.transform, new Zjcs.Combat.Official.FightBattleGrid { Width = 8, Height = 8 }),
                    scope, _ => null));
            graphPlayback.BindSceneHosts(offsets);
            try
            {
                Require(original.Duration == 1.77f && original.IsMoveSource
                    && original.MoveOffsetOfSource == new Vector3(0, -1.5f, 0), "actual original camera leaf fields");
                Require(performance.transform.localPosition == new Vector3(1000, 0, -20)
                    && performance.m_Lens.OrthographicSize == 5 && performance.Priority == 10
                    && !performance.gameObject.activeSelf && Allowed(), "original inactive camera hierarchy and lens");
                var playing = scope.Play(template, actor, null);
                yield return pump(2, .01f); scope.Tick(.02f); scope.LateTick(); brain.ManualUpdate();
                Require(performance.gameObject.activeSelf && !normal.gameObject.activeSelf && !Allowed()
                    && brain.ActiveVirtualCamera == performance
                    && actor.transform.localPosition == new Vector3(1000, -1.5f, 0)
                    && stage.transform.localPosition == new Vector3(1000, 0, 0), "native leaf switches camera, source, apply target and input through production host injection");
                int index = offsets.RegisterCameraOffset(new Vector3(.2f, -.1f, .5f), 3);
                Require(performance.GetComponent<CinemachineCameraOffset>().m_Offset == new Vector3(.2f, -.1f, 0)
                    && performance.m_Lens.OrthographicSize == 5, "performance camera consumes XY offset without inventing a missing zoom recomposer");
                offsets.UnregisterCameraOffset(index); brain.ManualUpdate();
                string label = actor.GetComponent<Zjcs.Offline.ProfessionUI.ProfessionNativeSceneRenderer>().State.Source.Sex
                    + "-case-" + ++performanceCameraCase;
                CapturePerformanceCamera(output, label + "-active");
                yield return pump(180, .01f); scope.Tick(1.8f); scope.LateTick(); brain.ManualUpdate();
                Require(playing.myStatus == MyPlayable.PlayableStatus.Stop && Allowed()
                    && normal.gameObject.activeSelf && !performance.gameObject.activeSelf
                    && actor.transform.localPosition == sourcePosition && stage.transform.localPosition == Vector3.zero,
                    "original finite duration restores source, apply target, camera and input");
                CapturePerformanceCamera(output, label + "-restored");
                long first = host.BlockGameObjectInput(), second = host.BlockGameObjectInput();
                Require(first != second && (int)(first >> 32) == 1 && (int)(second >> 32) == 1, "original layer signature identity");
                host.UnblockGameObjectInput(first); host.UnblockGameObjectInput(first); host.UnblockGameObjectInput(0);
                Require(!Allowed(), "duplicate and unknown unblock preserve another operation's block");
                host.UnblockGameObjectInput(second); Require(Allowed(), "last matching input operation releases the world layer");
                scope.Play(template, actor, null); scope.Dispose(); brain.ManualUpdate();
                Require(Allowed() && normal.gameObject.activeSelf && !performance.gameObject.activeSelf
                    && actor.transform.localPosition == sourcePosition, "active graph scope teardown restores the battle before scene disposal");
                Debug.Log("[Original performance camera] actual 15840 leaf, production scope injection, original camera hierarchy/lens, source/apply positions, XY offset, finite duration, independent input tokens and active teardown passed. Full 15840 graph and interactive HUD remain separate.");
            }
            finally
            {
                graphPlayback.Dispose(); ((IDisposable)view).Dispose();
                actor.transform.localPosition = sourcePosition;
                Object.DestroyImmediate(outputGo); Object.DestroyImmediate(root);
            }
            yield return null;
        }

        private static void CapturePerformanceCamera(Camera camera, string label)
        {
            var target = RenderTexture.GetTemporary(675, 1463, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var image = new Texture2D(675, 1463, TextureFormat.RGBA32, false, true);
            try
            {
                if (GraphicsSettings.currentRenderPipeline)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else { camera.targetTexture = target; camera.Render(); }
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 675, 1463), 0, 0); image.Apply();
                Require(image.GetPixels32().Count(p => p.a > 8) > 100, "selected actual battle camera sees original native character geometry");
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                    "performance-camera-" + label + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target); Object.Destroy(image);
            }
        }
    }
}
