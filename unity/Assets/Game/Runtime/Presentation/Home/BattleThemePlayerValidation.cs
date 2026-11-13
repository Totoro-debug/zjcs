using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    // Explicit, isolated Windows diagnostic; ordinary startup never creates it.
    public sealed class BattleThemePlayerValidation : MonoBehaviour
    {
        private string output;
        private Exception failure;
        [Serializable] private sealed class Result { public bool passed; public string error; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--zjcs-validate-battle-theme");
            if (at < 0) return;
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (at + 1 >= args.Length || string.IsNullOrEmpty(workspace)
                || !File.Exists(Path.Combine(workspace, ".zjcs-validation.json")))
                throw new ArgumentException("Use a marked battle theme validation workspace.");
            string destination = Path.GetFullPath(args[at + 1]);
            if (!destination.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) || !args.Contains("-homeAcceptance"))
                throw new ArgumentException("Use a workspace child and the isolated startup switch.");
            AudioListener.volume = 0;
            Application.runInBackground = true;
            var host = new GameObject("Battle theme Windows verification");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<BattleThemePlayerValidation>();
            runner.output = destination;
            Directory.CreateDirectory(destination);
            runner.StartCoroutine(runner.Run());
        }

        private IEnumerator Run()
        {
            Application.logMessageReceived += Observe;
            var stack = new Stack<IEnumerator>();
            stack.Push(Steps());
            float deadline = Time.realtimeSinceStartup + 180;
            while (stack.Count > 0 && failure == null)
            {
                object next = null;
                try
                {
                    Require(Time.realtimeSinceStartup < deadline, "Theme Windows verification timed out.");
                    var current = stack.Peek();
                    if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    next = current.Current;
                }
                catch (Exception error) { failure = error; }
                if (failure == null) yield return next;
            }
            while (stack.Count > 0)
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { if (failure == null) failure = error; }
            yield return null;
            Application.logMessageReceived -= Observe;
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(
                new Result { passed = failure == null, error = failure?.ToString() ?? "" }, true));
            if (failure != null) Debug.LogException(failure);
            else Debug.Log("ORIGINAL_BATTLE_THEME_WINDOWS_PASSED: normal/themed formal HUD, native cheer geometry, Chinese glyphs, show/loop, visibility, pointer start, damage and disposal.");
            Application.Quit(failure == null ? 0 : 1);
        }

        private IEnumerator Steps()
        {
            yield return null;
            var root = new GameObject("Battle theme Windows fixture");
            var world = new GameObject("Battle world");
            world.transform.SetParent(root.transform, false);
            var camera = new GameObject("Battle camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.position = new Vector3(0, 0, -20);
            camera.orthographic = true; camera.orthographicSize = 6; camera.tag = "MainCamera";
            var canvas = new GameObject("Battle canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(root.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var events = root.AddComponent<EventSystem>();
            var controller = world.AddComponent<OfficialBattleRuntimeController>();
            try
            {
                for (int theme = 0; theme <= 1; theme++)
                {
                    var assets = OfficialCombatAssetCatalog.LoadDefault();
                    var rules = assets.Load();
                    var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 43,
                        Grid = new FightBattleGrid { Width = 8, Height = 8 }, ResultRules = new FightBattleResultRules() };
                    var actor = Unit(1, 1, 2, 2, 100);
                    actor.Properties.Add(new FightPropertyValue { Id = rules.PropertyType("MoveDist"), Value = 4 });
                    int item = rules.MonsterActiveSkillItems(3206, 0).Single();
                    int entity = assets.LoadSkillItems().Required(item).EntityId;
                    var props = rules.CalculateSkillProps(entity, 1, 1, 1)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();
                    var skill = rules.CreateSkill(entity, 0, item, 1, 1, 1, props);
                    actor.Skills.Add(skill);
                    actor.SkillState.Slots.Add(rules.CreateSkillSlot(skill,
                        rules.CalculateSkillCooldown(entity, 1, 1, 1, actor.Properties.ToDictionary(p => p.Id, p => p.Value))));
                    state.Units.Add(actor); state.Units.Add(Unit(2, 2, 4, 2, 1));
                    controller.Begin(new OfficialBattleRequest { Checkpoint = state, WorldRoot = world.transform,
                        HudParent = canvas.transform, CombatAssets = assets, InitialStepDelay = 0, FightThemeId = theme,
                        EnvironmentGameId = 1000, HostPresentation = new OfficialBattleHostPresentation(camera, world.transform, null),
                        Options = new OfficialBattleSessionOptions { SkipPrepareStatus = false, ShowMainPanelDelay = .05f } });
                    const string slotPath = "FightMainPanel/Content/ThemeNpcSlot";
                    Require(canvas.transform.Find(slotPath) == null, "Original HUD delay was skipped.");
                    for (int frame = 0; frame < 180 && canvas.transform.Find(slotPath) == null; frame++)
                    { controller.Tick(Time.deltaTime); yield return null; }
                    Transform slot = canvas.transform.Find(slotPath);
                    Require(slot != null && slot.childCount == theme, "Original theme selection did not match its HUD child.");
                    if (theme == 1)
                    {
                        var label = slot.GetComponentInChildren<WorldMultiLanguageText>();
                        Require(label != null && label.Text == "加油！", "Original theme Chinese text was lost.");
                        var text = label.GetComponent<TMP_Text>(); text.ForceMeshUpdate();
                        Require("加油！".All(character => text.font.characterLookupTable.ContainsKey(character)),
                            "The Windows dynamic font has missing cheer glyphs.");
                        var animation = slot.GetComponentInChildren<Animation>();
                        Require(animation != null && animation.IsPlaying("FightThemeNpcLuoTianYi_Show"), "Original Show clip did not start.");
                        var actors = slot.GetComponentsInChildren<PartnerNativeUiActor>(true);
                        Require(actors.Length == 2 && actors.All(value => value.IsValid)
                            && actors.Any(value => value.Pose.GetCurrent(0)?.Animation.Name == "map/cheer_01"),
                            "The original cheer skeletons were not initialized.");
                        string before = Geometry(actors);
                        yield return Capture("theme-show");
                        yield return new WaitForSeconds(1.4f);
                        Require(animation.IsPlaying("FightThemeNpcLuoTianYi_Loop") && Geometry(actors) != before,
                            "Original Show/Loop or animated canvas geometry failed on Windows.");
                        yield return Capture("theme-loop");
                        controller.SetHudVisible(false); yield return null;
                        Require(!slot.gameObject.activeInHierarchy, "Hidden HUD left the theme NPC visible.");
                        controller.SetHudVisible(true); yield return null;
                        Require(slot.gameObject.activeInHierarchy && slot.childCount == 1
                            && animation.IsPlaying("FightThemeNpcLuoTianYi_Show"), "Restoring the HUD did not restart its original entrance.");
                        yield return Capture("theme-restored");
                    }
                    bool hit = false;
                    controller.StepCompleted += result => { if (result.Event.DamageEvents.Count > 0) hit = true; };
                    var start = canvas.transform.Find(OfficialBattleHudView.StartButtonPath.TrimStart('/')).GetComponent<Button>();
                    Canvas.ForceUpdateCanvases();
                    var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null,
                        start.transform.TransformPoint(((RectTransform)start.transform).rect.center)), button = PointerEventData.InputButton.Left };
                    var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
                    Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == start.gameObject,
                        "Original start button is obstructed.");
                    pointer.pointerCurrentRaycast = hits[0];
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler); yield return null;
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    Require(controller.Phase == FightBattlePhase.Play, "Simulated pointer did not start the battle.");
                    for (int frame = 0; frame < 360 && !(hit && !controller.IsPresentingStep); frame++)
                    { controller.Tick(Time.deltaTime); yield return null; }
                    Require(hit, "The formal battle did not settle its actual skill damage.");
                    yield return Capture(theme == 0 ? "normal-battle" : "theme-battle");
                    controller.DisposeSession(); yield return null;
                    Require(canvas.transform.Find(slotPath) == null, "The original theme HUD survived disposal.");
                }
            }
            finally { controller.DisposeSession(); Destroy(root); Time.timeScale = 1; }
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Require(texture != null && texture.width == Screen.width && texture.height == Screen.height, "Windows render capture is missing.");
            try { File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); }
            finally { Destroy(texture); }
        }

        private static string Geometry(PartnerNativeUiActor[] actors)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            int count = 0;
            foreach (var renderer in actors.SelectMany(actor => new[] { actor.Graphic.canvasRenderer }
                .Concat(actor.Graphic.canvasRenderers)).Distinct())
            {
                Mesh mesh = renderer.GetMesh();
                if (!mesh) continue;
                foreach (Vector3 vertex in mesh.vertices) { writer.Write(vertex.x); writer.Write(vertex.y); writer.Write(vertex.z); count++; }
            }
            Require(count > 0, "Original Windows theme has no rendered canvas mesh.");
            using var hash = SHA256.Create(); return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }

        private static FightBattleUnitState Unit(int id, int side, int x, int y, int speed)
        {
            const long hp = 1000000;
            var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 2, MonsterId = 3206,
                MonsterTemplateId = 3206, RoleEntityId = 3206, Hp = hp, MaxHp = hp, LastMaxHp = hp,
                Level = 1, SubRank = 1, X = x, Y = y, Turn = new FightTurnUnit { Id = id, Hp = hp, Speed = speed } };
            foreach (var pair in new[] { (1, 100L), (2, 100L), (3, hp), (4, (long)speed) })
                unit.Properties.Add(new FightPropertyValue { Id = pair.Item1, Value = pair.Item2 });
            return unit;
        }
        private void Observe(string message, string trace, LogType type)
        { if (failure == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) failure = new InvalidOperationException(message + "\n" + trace); }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
    }
}
