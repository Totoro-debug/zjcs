using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleStatusCounterAcceptance
    {
        public static void Run()
        {
            var root = new GameObject("StatusCounterFixture");
            var source = new GameObject("StatusCounterEntityModel");
            source.transform.SetParent(root.transform, false);
            OfficialBattleSceneView scene = null;
            try
            {
                root.transform.position = new Vector3(0, 0, 3);
                var checkpoint = new FightBattleCheckpoint();
                foreach (int id in new[] { 1, 2, 3, 4 })
                    checkpoint.Units.Add(new FightBattleUnitState
                    {
                        Id = id, CharacterType = 1, Hp = 100, MaxHp = 100,
                        MasterId = id == 1 ? 3 : id == 2 ? 4 : 0,
                        SkillState = new FightSkillAgentState { Round = id + 6 }
                    });
                var owner = checkpoint.Units[0];
                var status = OfficialCombatAssetCatalog.LoadDefault().Load().CreateStatus(24679, 1, 2, 1);
                status = FightStatusLifecycle.Apply(owner, status, 4, 1);
                status.CurDurationRound = 10;
                scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                    _ => null, new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 4, Height = 4 }),
                    (_, __) => null, (_, __) => { }, OfficialBattleHudCatalog.LoadDefault(), root.transform);
                scene.Refresh(checkpoint);
                scene.AttachUnitTransform(1, source.transform);
                foreach (int target in new[] { 0, 1, 2, 3, 4 })
                {
                    status.RoundTarget = target;
                    scene.Refresh(checkpoint);
                    int expected = target == 0 ? 8 : target == 1 ? 6 : target == 2 ? 7 : target == 3 ? 4 : 5;
                    Require(Count(root).gameObject.activeSelf && Count(root).text == expected.ToString(),
                        "Status icon did not use the original target round: " + target
                        + ", expected=" + expected + ", actual=" + Count(root).text);
                }
                status.RoundTarget = 1;
                checkpoint.Units[1].SkillState.Round++;
                scene.Refresh(checkpoint);
                Require(Count(root).text == "5", "A creator round change did not refresh the applicator's status icon.");
                checkpoint.Units[1].SkillState.Round = 14;
                scene.Refresh(checkpoint);
                Require(!Count(root).gameObject.activeSelf, "A zero status counter left the original text node enabled.");
                checkpoint.Units[1].SkillState.Round = 13;
                scene.Refresh(checkpoint);
                Require(Count(root).gameObject.activeSelf && Count(root).text == "1", "A reused counter failed to show one.");
                if (Application.isPlaying) VerifyPixels(Count(root));
                checkpoint.Units[1].SkillState = null;
                scene.Refresh(checkpoint);
                Require(Count(root).text == "8", "A missing round component lost the configured-duration fallback.");
                status.DurationRound = -1;
                scene.Refresh(checkpoint);
                Require(!Count(root).gameObject.activeSelf, "An infinite status displayed a negative counter.");
                owner.Statuses.Clear();
                var stacked = OfficialCombatAssetCatalog.LoadDefault().Load().CreateStatus(24009, 1, 1, 2);
                stacked.StackCount = 1;
                owner.Statuses.Add(stacked);
                scene.Refresh(checkpoint);
                Require(Count(root).gameObject.activeSelf && Count(root).text == "1", "An original stack counter hid one stack.");
                owner.Statuses.Clear();
                scene.Refresh(checkpoint);
                Require(!Count(root).transform.parent.gameObject.activeSelf, "Removed status retained a visible pooled icon.");
                Debug.Log("Original status counters passed: five target clocks, creator updates, configured fallback, zero/infinite hiding, pooled reuse and one stack; PlayMode=" + Application.isPlaying);
            }
            finally { scene?.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }

        private static TMP_Text Count(GameObject root) => root.GetComponentsInChildren<TMP_Text>(true)
            .Single(value => value.name == "CountTxt");

        private static void VerifyPixels(TMP_Text text)
        {
            var cameraObject = new GameObject("StatusCounterRasterCamera");
            var target = new RenderTexture(512, 512, 24);
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Canvas canvas = text.GetComponentInParent<Canvas>();
                foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = .3f;
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                canvas.worldCamera = camera;
                Canvas.ForceUpdateCanvases();
                text.ForceMeshUpdate();
                Debug.Log("Status counter raster setup: active=" + text.gameObject.activeInHierarchy
                    + ", vertices=" + text.textInfo.meshInfo.Sum(value => value.vertexCount)
                    + ", scale=" + text.transform.lossyScale + ", canvas=" + canvas.renderMode);
                camera.transform.position = text.transform.position + new Vector3(0, 0, -10);
                camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                Color32[] withText = pixels.GetPixels32();
                string output = Environment.GetEnvironmentVariable("ZJCS_BATTLE_STATUS_COUNTER_PREVIEW");
                if (!string.IsNullOrEmpty(output)) File.WriteAllBytes(Path.Combine(output, "status-counter.png"), pixels.EncodeToPNG());
                text.gameObject.SetActive(false);
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                Color32[] withoutText = pixels.GetPixels32();
                int count = withText.Where((value, index) => value.r != withoutText[index].r
                    || value.g != withoutText[index].g || value.b != withoutText[index].b).Count();
                Require(count > 10, "Status counter produced no actual camera pixels: " + count);
                text.gameObject.SetActive(true);
                Debug.Log("Original status counter camera pixels=" + count);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void Require(bool value, string message) { if (!value) throw new BuildFailedException(message); }
    }
}
