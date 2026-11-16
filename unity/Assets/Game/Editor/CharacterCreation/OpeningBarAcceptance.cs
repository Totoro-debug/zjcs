using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Opening;

namespace Zjcs.Editor.CharacterCreation
{
    [InitializeOnLoad]
    public static class OpeningBarAcceptance
    {
        private const string Key = "Zjcs.OpeningBarAcceptance";
        static OpeningBarAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                {
                    SessionState.SetBool(Key, false);
                    new GameObject("Opening bar acceptance").AddComponent<OpeningBarRunner>();
                }
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
    }

    public sealed class OpeningBarRunner : MonoBehaviour
    {
        private OpeningFightPlayback playback;
        private OpeningBattlefield battlefield;
        private OpeningUiView ui;
        private IEnumerator Start()
        {
            AudioListener.volume = 0; Application.runInBackground = true;
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); Finish(1); yield break; }
                yield return current;
            }
            Debug.Log("OPENING_BARS_PASSED: original game_902, all 18 record units, shared foreground material and layer, red enemy pixels, damage and healing.");
            Finish(0);
        }
        private IEnumerator Check()
        {
            var assets = OpeningAssets.LoadDefault();
            var catalog = OfficialBattleHudCatalog.LoadDefault();
            ui = new OpeningUiView(assets.Ui, catalog.Font, null, 60);
            battlefield = OpeningBattlefield.Load(transform, Debug.Log);
            playback = new OpeningFightPlayback(assets, ui, OfficialBattleModelCatalog.LoadDefault(),
                battlefield.UnitRoot, battlefield, () => false, Debug.Log);
            yield return playback.Prepare();
            if (playback.SpawnedUnits != 18) throw new InvalidDataException("Original 18 opening actors were not spawned.");
            var target = new RenderTexture(900, 1600, 24);
            var camera = battlefield.StageCamera; camera.targetTexture = target;
            yield return null; yield return null;
            Verify(target, camera, "opening-full.png");
            var presentation = battlefield.UnitRoot.GetComponentInChildren<OpeningBattlePresentation>();
            if (!presentation) throw new InvalidDataException("Opening presentation is absent.");
            var monster = assets.Record.Entities.First(e => e.Faction == 127);
            presentation.ApplyDamage(monster.EntityId, -monster.MaxHp / 4);
            yield return new WaitForSeconds(.7f);
            Verify(target, camera, "opening-damaged.png");
            presentation.ApplyDamage(monster.EntityId, monster.MaxHp / 4);
            yield return new WaitForSeconds(.7f);
            Verify(target, camera, "opening-healed.png");
        }
        private void Verify(RenderTexture target, Camera camera, string name)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG());
            int checkedBars = 0;
            foreach (var image in battlefield.UnitRoot.GetComponentsInChildren<Image>())
            {
                if (image.transform.parent?.name != "FillMask" || image.transform.parent.parent.name != "HpBar") continue;
                if (image.color.r < image.color.g * 1.5f) continue;
                if (image.material.shader.name != "Zjcs/Combat/UnitBar") throw new InvalidDataException("Opening uses a separate HP material.");
                var root = image.GetComponentInParent<Canvas>();
                if (root.sortingOrder != 31998) throw new InvalidDataException("Opening overrides common fill sorting.");
                var point = camera.WorldToScreenPoint(image.rectTransform.TransformPoint(image.rectTransform.rect.center));
                if (point.x < 2 || point.y < 2 || point.x >= target.width - 2 || point.y >= target.height - 2) continue;
                bool red = false;
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                {
                    var pixel = texture.GetPixel((int)point.x + x, (int)point.y + y);
                    red |= pixel.r > pixel.g * 1.6f && pixel.r > pixel.b * 1.6f;
                }
                if (!red) throw new InvalidDataException("Opening monster bar is obscured/non-red: " + image.transform.parent.parent.parent.parent.name + " at " + point);
                checkedBars++;
            }
            UnityEngine.Object.Destroy(texture);
            if (checkedBars < 3) throw new InvalidDataException("Too few visible original monster bars: " + checkedBars);
            Debug.Log(name + " red enemy bars=" + checkedBars);
        }
        private void Finish(int result)
        {
            playback?.Dispose(); battlefield?.Dispose(); ui?.Dispose();
            EditorApplication.Exit(result);
        }
    }
}
