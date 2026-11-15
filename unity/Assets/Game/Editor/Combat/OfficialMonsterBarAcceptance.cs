using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class OfficialMonsterBarAcceptance
    {
        private const string Key = "Zjcs.MonsterBarAcceptance";
        private static Camera camera;
        private static RenderTexture target;
        private static OfficialBattleLiveMonsterModelView model;
        private static OfficialBattleUnitWorldHudView hud;
        private static FightBattleUnitState unit;
        private static int stage;
        private static double next;

        static OfficialMonsterBarAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) Begin();
            };
        }

        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Begin()
        {
            SessionState.SetBool(Key, false);
            try
            {
                AudioListener.volume = 0;
                Application.runInBackground = true;
                var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                camera = new GameObject("Monster bar camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true; camera.orthographicSize = 1.6f;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.backgroundColor = new Color(.2f, .2f, .2f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                target = new RenderTexture(640, 800, 24); camera.targetTexture = target;
                var light = new GameObject("Global light", typeof(Light2D)).GetComponent<Light2D>();
                light.lightType = Light2D.LightType.Global; light.intensity = 1;
                unit = new FightBattleUnitState { Id = 2, CharacterType = 2, Side = 2,
                    MonsterId = int.TryParse(Environment.GetEnvironmentVariable("ZJCS_BAR_MONSTER"), out int id) ? id : 3206,
                    Hp = 100, MaxHp = 100, LastMaxHp = 100 };
                model = OfficialBattleLiveMonsterModelView.TryCreate(unit, new GameObject("Monster").transform);
                Require(model != null, "Original monster model was not loaded.");
                hud = OfficialBattleUnitWorldHudView.Create(OfficialBattleHudCatalog.LoadDefault(), unit, model.Transform);
                Require(hud != null, "Original monster bar was not loaded.");
                // Stress the reported overlap with the actual original mesh.
                // Only the fixture's body moves; the authored bar geometry stays intact.
                model.ModelTransform.localPosition += new Vector3(0, -.45f, 0);
                stage = 0; next = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Tick()
        {
            try
            {
                model.Tick(.016f); hud.Tick(.016f);
                if (EditorApplication.timeSinceStartup < next) return;
                Canvas.ForceUpdateCanvases();
                var fill = hud.Root.Find("HpBar/FillMask/FillImage").GetComponent<Image>();
                Debug.Log("Monster bar color=" + fill.color + " canvas=" + fill.canvas.sortingOrder);
                var texture = Capture(stage == 0 ? "monster-full.png" : "monster-damaged.png");
                var point = camera.WorldToScreenPoint(fill.rectTransform.TransformPoint(fill.rectTransform.rect.center));
                var pixel = texture.GetPixel((int)point.x, (int)point.y);
                Debug.Log("Monster bar center pixel=" + pixel + " at " + point);
                Require(pixel.r > pixel.g * 1.8f && pixel.r > pixel.b * 1.8f,
                    "Rendered monster HP is obscured or not red: " + pixel);
                UnityEngine.Object.DestroyImmediate(texture);
                if (stage++ == 0)
                {
                    var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 14, RandomSeed = 1 };
                    unit.Turn.Id = unit.Id;
                    checkpoint.Units.Add(unit);
                    unit.Hp = 75;
                    unit = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint)).Units.Single();
                    hud.Refresh(unit);
                    for (int i = 0; i < 100; i++) hud.Tick(.016f);
                    next = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                Require(Mathf.Abs(hud.Root.Find("HpBar/FillMask").GetComponent<RectTransform>().rect.width
                    / hud.Root.Find("HpBar").GetComponent<RectTransform>().rect.width - .75f) < .01f,
                    "Restored HP fill is incorrect.");
                Debug.Log("MONSTER_BAR_PASSED: actual original monster, full/damaged color, body overlap and checkpoint restore.");
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static Texture2D Capture(string name)
        {
            camera.Render();
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG());
            return texture;
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (error != null) Debug.LogException(error);
            hud?.Dispose(); model?.Dispose();
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
