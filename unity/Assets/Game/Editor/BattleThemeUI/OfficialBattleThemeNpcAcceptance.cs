using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Editor.Combat;
using Zjcs.Offline.BattleThemeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleThemeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.BattleThemeUI
{
    [InitializeOnLoad]
    public static class OfficialBattleThemeNpcAcceptance
    {
        private const string Active = "Zjcs.BattleThemeNpcAcceptance";
        static OfficialBattleThemeNpcAcceptance()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original battle theme NPC validation").AddComponent<BattleThemeNpcRunner>();
            };
        }
        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace) || !Path.GetFullPath(Application.dataPath).StartsWith(
                    Path.GetFullPath(workspace) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use this session's isolated theme UI project.");
            BattleGameViewInput.Focus();
            SessionState.SetString(Active + ".PreviousAutomation",
                Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION") ?? "");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            EditorApplication.EnterPlaymode();
        }
        internal static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            string previous = SessionState.GetString(Active + ".PreviousAutomation", "");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", previous.Length == 0 ? null : previous);
            if (error != null) Debug.LogException(error);
            else Debug.Log("ORIGINAL_BATTLE_THEME_NPC_PASSED: original slot, prefab, legacy show/loop sequence, native UI cheer geometry, hide/re-enable and texture lifetime. Formal controller and Windows Player remain separate checks.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class BattleThemeNpcRunner : MonoBehaviour
    {
        private int errors;
        private IEnumerator Start()
        {
            AudioListener.volume = 0;
            Application.logMessageReceived += (_, __, type) =>
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            IEnumerator steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { OfficialBattleThemeNpcAcceptance.Finish(error); yield break; }
                yield return current;
            }
            OfficialBattleThemeNpcAcceptance.Finish(errors == 0 ? null :
                new InvalidDataException("Original theme NPC reported runtime errors: " + errors));
        }

        private IEnumerator Check()
        {
            CheckSkeletonReflection();
            yield return BattleGameViewInput.ResizePortrait(675);
            var camera = new GameObject("Theme NPC validation camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .16f, .18f);
            var canvas = new GameObject("Theme NPC validation canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var catalog = OfficialBattleHudCatalog.LoadDefault();
            var original = catalog.FindPrefab("main").nodes.Single(row =>
                row.path == "/FightMainPanel/Content/ThemeNpcSlot");
            GameObject slot = new OfficialBattleHudRenderer(catalog).CreateSubtree(original, canvas.transform);
            var previous = ProfessionAppearanceTextureBindings.Current;
            var empty = OfflineBattleThemeNpcView.Mount(slot.transform, null);
            yield return null;
            Require(slot.transform.childCount == 0, "No-theme activation created a theme character.");
            empty.Dispose(); Destroy(empty);
            yield return null;

            var previousLocalizer = WorldMultiLanguageText.Localize;
            Func<string, string> unrelatedLocalizer = key => "Unrelated host text: " + key;
            WorldMultiLanguageText.Localize = unrelatedLocalizer;
            var view = OfflineBattleThemeNpcView.Mount(slot.transform,
                "UI/Fight/FightMainPanel/ThemeNpc/FightThemeNpcLuoTianYi");
            yield return new WaitForSeconds(.2f);
            Require(slot.transform.childCount == 1, "Original theme activation must create exactly one prefab.");
            var npc = slot.transform.GetChild(0).gameObject;
            var labels = npc.GetComponentsInChildren<WorldMultiLanguageText>(true);
            Require(labels.Length == 1 && labels[0].myKey == "LuoTianYi_npc_txt" && labels[0].Text == "加油！",
                "Original theme localization did not override the unrelated host text service.");
            Require(ReferenceEquals(WorldMultiLanguageText.Localize, unrelatedLocalizer),
                "The theme NPC replaced the host's global localization service.");
            var nativeText = labels[0].GetComponent<TMP_Text>();
            nativeText.ForceMeshUpdate();
            foreach (char character in "加油！")
                Require(nativeText.font.characterLookupTable.ContainsKey(character),
                    "Original dynamic theme font did not load its Chinese glyph: " + character);
            var animation = npc.GetComponent<Animation>();
            Require(animation && animation.IsPlaying("FightThemeNpcLuoTianYi_Show"), "Original theme entrance clip did not start.");
            Require(npc.GetComponent<AnimationSequence>(), "Original legacy sequence component is missing.");
            var actors = npc.GetComponentsInChildren<PartnerNativeUiActor>(true);
            Require(actors.Length == 2 && actors.All(actor => actor.IsValid), "Original theme character/decoration skeletons are not initialized.");
            Require(actors.Any(actor => actor.Pose.GetCurrent(0)?.Animation.Name == "map/cheer_01"),
                "Original theme sequence did not start its cheer animation.");
            string firstGeometry = Geometry(actors);
            yield return BattleGameViewInput.CaptureGameViewScreenshot(Output("theme-npc-entrance.png"));
            yield return new WaitForSeconds(1.5f);
            Require(animation.IsPlaying("FightThemeNpcLuoTianYi_Loop"), "Original entrance did not advance to its infinite loop.");
            Require(firstGeometry != Geometry(actors), "Original theme skeleton geometry did not animate.");
            yield return BattleGameViewInput.CaptureGameViewScreenshot(Output("theme-npc-loop.png"));
            slot.SetActive(false);
            yield return new WaitForSeconds(.2f);
            slot.SetActive(true);
            yield return new WaitForSeconds(.2f);
            Require(animation.IsPlaying("FightThemeNpcLuoTianYi_Show"), "Original enable did not restart the entrance sequence.");
            yield return BattleGameViewInput.CaptureGameViewScreenshot(Output("theme-npc-reenabled.png"));
            view.Dispose(); Destroy(view);
            yield return null;
            Require(slot.transform.childCount == 0, "Original theme child survived its battle lifetime.");
            Require(ReferenceEquals(previous, ProfessionAppearanceTextureBindings.Current), "The previous appearance texture binding was not restored.");
            WorldMultiLanguageText.Localize = previousLocalizer;
            Destroy(canvas); Destroy(camera.gameObject);
            yield return null;
        }

        private static string Geometry(PartnerNativeUiActor[] actors)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            int vertices = 0;
            foreach (var renderer in actors.SelectMany(actor => new[] { actor.Graphic.canvasRenderer }
                         .Concat(actor.Graphic.canvasRenderers)).Distinct())
            {
                Mesh mesh = renderer.GetMesh();
                if (!mesh) continue;
                foreach (Vector3 vertex in mesh.vertices)
                { writer.Write(vertex.x); writer.Write(vertex.y); writer.Write(vertex.z); vertices++; }
            }
            Require(vertices > 0, "Original theme UI produced no canvas geometry.");
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }

        // Global skeleton reflection must survive every selective inheritance
        // mode, setup-pose resets, and an already-solved constraint parent.
        private static void CheckSkeletonReflection()
        {
            var bones = new ProfessionNativeBonePose.Bone[11];
            bones[0] = new ProfessionNativeBonePose.Bone
                { name = "root", x = 3, y = -4, rotation = 31, scaleX = -2, scaleY = .7f, shearY = 17 };
            for (int mode = 0; mode < 5; mode++)
            {
                bones[mode + 1] = new ProfessionNativeBonePose.Bone
                    { name = "mode" + mode, parent = 0, inherit = mode,
                        x = 5, y = -2, rotation = -23, scaleX = .8f, scaleY = -1.3f, shearX = 9 };
                bones[mode + 6] = new ProfessionNativeBonePose.Bone
                    { name = "nested" + mode, parent = mode + 1, inherit = mode,
                        x = -1, y = 6, rotation = 13, scaleX = 1.2f, scaleY = .9f, shearY = -7 };
            }
            var baseline = new ProfessionNativeBonePose(bones, true, true);
            var reflected = new ProfessionNativeBonePose(bones, true, true);
            for (int flags = 1; flags <= 3; flags++)
            {
                bool flipX = (flags & 1) != 0, flipY = (flags & 2) != 0;
                Matrix4x4 frame = Matrix4x4.Scale(new Vector3(flipX ? -1 : 1, flipY ? -1 : 1, 1));
                reflected.SetSkeletonFlip(flipX, flipY);
                baseline.Reset(); reflected.Reset();
                Compare();
                baseline.SetRotation(0, -58); reflected.SetRotation(0, -58);
                Compare();
                Matrix4x4 constrained = Matrix4x4.TRS(new Vector3(-7, 11, 0),
                    Quaternion.Euler(0, 0, 42), new Vector3(-1.4f, .6f, 1));
                baseline.OverrideWorld(0, constrained);
                reflected.OverrideWorld(0, frame * constrained);
                Compare();
                void Compare()
                {
                    for (int bone = 0; bone < bones.Length; bone++)
                    {
                        Matrix4x4 expected = frame * baseline.World(bone), actual = reflected.World(bone);
                        for (int cell = 0; cell < 16; cell++)
                            Require(Mathf.Abs(expected[cell] - actual[cell]) < .0002f,
                                "Skeleton reflection changed selective inheritance: " + bones[bone].name + "/" + flags);
                    }
                }
            }
            reflected.SetSkeletonFlip(false, false);
            baseline.Reset(); reflected.Reset();
            for (int bone = 0; bone < bones.Length; bone++)
                Require(baseline.World(bone) == reflected.World(bone), "Removing skeleton reflection did not restore the original pose.");
            Debug.Log("ORIGINAL_THEME_SKELETON_REFLECTION_PASSED: X/Y/both, five inheritance modes, nested bones, reset and constrained parent.");
        }
        private static string Output(string name) => Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name);
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidDataException(message); }
    }
}
