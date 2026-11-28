using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class OfficialCurrentSkillIconsValidation
    {
        const string Key = "Zjcs.CurrentSkillIcons.Active";
        [Serializable] sealed class Identity { public string path; }
        [Serializable] sealed class Binding { public int item_id; public string raw_icon, guid; public Identity sprite; }
        [Serializable] sealed class Alias { public string name, path, canonical; }
        [Serializable] sealed class Quality { public int itemId, quality; }
        [Serializable] sealed class Source { public string manifest; public Binding[] bindings; public Alias[] aliases; public Quality[] quality_items; }
        static OfficialCurrentSkillIconsValidation()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("MutedCurrentSkillIcons").AddComponent<Runner>();
            };
            EditorApplication.update += () => {
                if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                    Finish(new TimeoutException("Current skill icon runtime validation timed out."));
            };
        }
        public static void Run()
        {
            string root = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"));
            string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
            Require(Path.GetDirectoryName(work) == Path.Combine(root, "output", "validation") && File.Exists(Path.Combine(work, ".zjcs-validation.json"))
                && Path.GetFullPath(Application.dataPath) == Path.Combine(root, "unity", "Assets"), "owned source project validation workspace");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 480);
            EditorApplication.EnterPlaymode();
        }
        static void Finish(Exception error)
        {
            SessionState.SetBool(Key, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("CURRENT_SKILL_ICONS_PASSED: current item/quality lookups, canonical native Sprite identity, original character bindings/resource paths and actual battle HUD skill slots/rendering.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Current skill icons: " + message); }
        public sealed class Runner : MonoBehaviour
        {
            IEnumerator Start()
            {
                Application.runInBackground = true; AudioListener.volume = 0;
                var steps = Check();
                while (true)
                {
                    object value;
                    try { if (!steps.MoveNext()) break; value = steps.Current; }
                    catch (Exception error) { Finish(error); yield break; }
                    yield return value;
                }
                Finish(null);
            }
            IEnumerator Check()
            {
                string root = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
                string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(root, "assets/reference/battle-animation/skill-icons-source-manifest.json")));
                var icons = OfficialCombatSkillIconCatalog.TryLoadDefault(); var qualities = OfficialBattleSkillQualityCatalog.Load();
                Require(source.manifest == "911_156995" && icons && icons.Count == source.bindings.Length, "complete current source icon catalog");
                foreach (var row in source.bindings)
                {
                    var expected = AssetDatabase.LoadAssetAtPath<Sprite>(row.sprite.path.Substring("unity/".Length));
                    var actual = icons.Find(row.item_id);
                    Require(expected && actual == expected && actual.name == Path.GetFileName(row.raw_icon), "canonical Sprite lookup: " + row.item_id);
                    int quality = source.quality_items.Single(q => q.itemId == row.item_id).quality;
                    Require(qualities.Find(row.item_id) == qualities.FindQuality(quality), "current source quality: " + row.item_id);
                }
                Require(!icons.Find(0) && !icons.Find(-1) && !icons.Find(int.MaxValue), "unknown item lookup");
                foreach (var row in source.aliases)
                {
                    var alias = Resources.Load<Sprite>("Character/SkillSkinIcons/" + row.name);
                    var expected = AssetDatabase.LoadAssetAtPath<Sprite>(row.canonical.Substring("unity/".Length));
                    Require(alias && expected && alias.name == expected.name && alias.texture == expected.texture && alias.rect == expected.rect
                        && alias.pivot == expected.pivot && alias.border == expected.border && alias.pixelsPerUnit == expected.pixelsPerUnit
                        && alias.vertices.SequenceEqual(expected.vertices) && alias.uv.SequenceEqual(expected.uv) && alias.triangles.SequenceEqual(expected.triangles), "original skill resource geometry/texture: " + row.name);
                }
                // Call the actual lower-level character API without adding a presentation dependency to it.
                var characterType = Type.GetType("Zjcs.Offline.Progression.OfficialCharacterAssetCatalog, Zjcs.Offline.Progression", true);
                var character = Resources.Load("Character/OfficialCharacterAssetCatalog", characterType);
                var data = characterType.GetMethod("Load").Invoke(character, null);
                var findItem = data.GetType().GetMethod("FindItem"); var find = characterType.GetMethod("FindSkillSkinSprite"); int checkedCharacterItems = 0;
                foreach (var row in source.bindings)
                {
                    if (findItem.Invoke(data, new object[] { row.item_id }) == null) continue;
                    var expected = icons.Find(row.item_id); var actual = (Sprite)find.Invoke(character, new object[] { data, row.item_id });
                    Require(actual && actual.texture == expected.texture && actual.vertices.SequenceEqual(expected.vertices) && actual.uv.SequenceEqual(expected.uv), "actual character skill lookup: " + row.item_id);
                    checkedCharacterItems++;
                }
                Require(checkedCharacterItems >= 376, "existing character skill definitions exercised");
                OfficialBattleSkillSlotAcceptance.Run();
                var camera = new GameObject("MutedCurrentSkillIconCamera", typeof(Camera)).GetComponent<Camera>();
                camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                var render = new RenderTexture(768, 1365, 24); render.Create(); camera.targetTexture = render;
                var canvas = new GameObject("CurrentSkillIconHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var view = new OfficialBattleHudView(OfficialBattleHudCatalog.LoadDefault()); var hud = view.Mount(canvas.transform, camera);
                var checkpoint = new FightBattleCheckpoint(); var player = new FightBattleUnitState { Id = 1, Hp = 100, MaxHp = 100 }; checkpoint.Units.Add(player);
                int[] samples = { 10000, 10125, 10270, 10718 };
                for (int i = 0; i < samples.Length; i++) player.Skills.Add(new FightBattleSkillDefinition { Slot = i, ItemId = samples[i] });
                view.RefreshPhase(FightBattlePhase.Play, null);
                view.RefreshPrepareCountdown(0);
                view.RefreshPetFunction(false);
                view.Refresh(checkpoint);
                view.RefreshSkillSlots(checkpoint, icons.Find);
                yield return null; yield return null;
                var cards = hud.GetComponentsInChildren<WorldGameSkillIcon>(true).OrderBy(card => card.SkillIndex).ToArray();
                Require(cards.Length == samples.Length && cards.Select((card, i) => card.Icon.sprite == icons.Find(samples[i])).All(value => value), "actual HUD binds old, appearance and newly configured skill icons");
                OfficialBattleSkillQualityAcceptance.VerifyActual(hud, checkpoint);
                foreach (var card in cards)
                {
                    var corners = new Vector3[4]; card.Icon.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                        Require(point.x >= 0 && point.x <= render.width && point.y >= 0 && point.y <= render.height, "skill icon is inside the rendered viewport: " + point);
                    }
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = render;
                var image = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); image.Apply(); RenderTexture.active = previous;
                Require(image.GetPixels32().Count(pixel => pixel.r > 30 || pixel.g > 30 || pixel.b > 30) > 500, "actual battle HUD rendering");
                File.WriteAllBytes(Path.Combine(work, "current-combat-skill-icons.png"), image.EncodeToPNG());
                player.Skills.Clear(); view.RefreshSkillSlots(checkpoint, icons.Find);
                Require(cards.All(card => !card.Icon.gameObject.activeInHierarchy), "original slots clear after unequip");
                Object.Destroy(canvas.gameObject); Object.Destroy(camera.gameObject); Object.Destroy(image); render.Release(); Object.Destroy(render);
                Debug.Log("Current native icon checks: " + source.bindings.Length + " battle items, " + source.aliases.Length + " resource Sprites, " + checkedCharacterItems + " existing character items and four actual HUD slots.");
                yield return null;
            }
        }
    }
}
