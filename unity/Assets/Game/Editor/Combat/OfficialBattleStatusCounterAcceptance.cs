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
                VerifyRoundStartCounters(scene, root);
                Debug.Log("Original status counters passed: five target clocks, creator updates, configured fallback, zero/infinite hiding, pooled reuse and one stack; PlayMode=" + Application.isPlaying);
            }
            finally { scene?.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }

        private static TMP_Text Count(GameObject root) => root.GetComponentsInChildren<TMP_Text>(true)
            .Single(value => value.name == "StackCountText");

        private static void VerifyRoundStartCounters(OfficialBattleSceneView scene, GameObject root)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 23 };
            foreach (int id in new[] { 1, 2 })
            {
                var unit = new FightBattleUnitState { Id = id, CharacterType = 1, Hp = 100, MaxHp = 100 };
                unit.Turn.Id = id;
                checkpoint.Units.Add(unit);
            }
            foreach (int id in new[] { 24037, 24038, 24045, 24222, 24472, 24473, 24474, 24486, 24640 })
            {
                var owner = checkpoint.Units[0];
                owner.Statuses.Clear();
                var template = catalog.CreateStatus(id, 1, 2, id, 12950, 1295, 5, 20, 1);
                var status = FightStatusLifecycle.Apply(owner, template, 0, id);
                scene.Refresh(checkpoint);
                Require(Count(root).gameObject.activeSelf && Count(root).text == "3",
                    "Original custom round-start counter was not initialized: " + id);
                var calls = FightStatusTriggerRuntime.CollectRoundStartInvocations(owner, owner,
                    checkpoint.Units, new FightRandomStream(23), catalog, status);
                Require(calls.Count == 1 && status.RoundStartTriggeredCount == 1,
                    "Original round-start invocation was not collected: " + id);
                scene.Refresh(checkpoint);
                Require(Count(root).text == "2", "Invocation did not reduce the native custom counter: " + id);
                var renewed = FightStatusLifecycle.Apply(owner, template, 0, id + 100000);
                Require(ReferenceEquals(renewed, status) && status.RoundStartStackRoundCount == 2,
                    "Original same-source duration refresh did not retain its round-start quota: " + id);
                scene.Refresh(checkpoint);
                Require(Count(root).text == "5", "Custom counter used layer stacks or duration instead of remaining calls: " + id);
                checkpoint = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                owner = checkpoint.Units[0]; status = owner.Statuses.Single();
                scene.Refresh(checkpoint);
                Require(Count(root).text == "5", "Checkpoint reload lost the native remaining-call counter: " + id);
                for (int call = 0; call < 5; call++)
                    Require(FightStatusTriggerRuntime.CollectRoundStartInvocations(owner, owner,
                        checkpoint.Units, new FightRandomStream(23), catalog, status).Count == 1,
                        "Original extended invocation quota ended early: " + id);
                scene.Refresh(checkpoint);
                Require(!Count(root).gameObject.activeSelf, "An exhausted custom counter remained visible: " + id);
                status.RoundStartMaxCount = -1;
                scene.Refresh(checkpoint);
                Require(!Count(root).gameObject.activeSelf, "An unlimited custom counter showed negative calls: " + id);
            }
            foreach (int id in new[] { 24343, 24481, 25090 })
            {
                // Display fixture for the original round-replacement flag; this
                // does not assert execution of unsupported trigger parameters.
                var owner = checkpoint.Units[0]; owner.Statuses.Clear();
                var status = catalog.CreateStatus(id, 1, 2, id);
                owner.Statuses.Add(status);
                scene.Refresh(checkpoint);
                Require(Count(root).gameObject.activeSelf && Count(root).text == "3",
                    "Original round-start replacement counter was hidden: " + id);
                status.RoundStartTriggeredCount = 1;
                scene.Refresh(checkpoint);
                Require(Count(root).text == "2", "Round replacement read the duration clock instead of invocation count: " + id);
            }
            Debug.Log("Original round-start icon counters passed: nine custom definitions, collected invocations, same-origin refresh, six-call quota, checkpoint reload, exhausted/unlimited hiding and three round-replacement definitions.");
        }

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
