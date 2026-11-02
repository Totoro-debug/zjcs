using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        // Feed the actual host's live mutex instances into an isolated original
        // HUD. No fixture status mutation or production camera changes are needed.
        private sealed class StatusIconProbe : IDisposable
        {
            private readonly GameObject body = new GameObject("Status icon verification body");
            private readonly FightBattleUnitState unit;
            private readonly OfficialBattleUnitWorldHudView view;
            private readonly bool capture;
            private int firstItem;

            public StatusIconProbe(int id, bool capture)
            {
                this.capture = capture;
                unit = new FightBattleUnitState { Id = id, CharacterType = 1, Side = 1, Hp = 100, MaxHp = 100 };
                view = OfficialBattleUnitWorldHudView.Create(
                    Resources.Load<OfficialBattleHudCatalog>(OfficialBattleHudCatalog.DefaultResourcePath), unit, body.transform);
                Require(view != null, "Original player status HUD was not bound.");
                foreach (Transform node in body.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            }

            public void Check(FightBattleStatusState[] applied, FightBattleStatusState[] expected, string phase)
            {
                unit.Statuses.Clear(); unit.Statuses.AddRange(applied);
                view.Refresh(unit);
                var visible = (List<FightBattleStatusState>)typeof(OfficialBattleUnitWorldHudView)
                    .GetField("visibleStatuses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Require(visible.SequenceEqual(expected), "Status icon order/forbidden state mismatch: " + phase);
                var layout = view.Root.Find("StatusLayout");
                var active = layout.Cast<Transform>().Where(node => node.gameObject.activeSelf).ToArray();
                Require(active.Length == expected.Length, "Status icon pool count mismatch: " + phase);
                if (active.Length > 0)
                {
                    if (firstItem == 0) firstItem = active[0].GetInstanceID();
                    Require(active[0].GetInstanceID() == firstItem, "Status icon slot was recreated instead of reused.");
                }
                foreach (var node in active)
                {
                    var image = node.Find("IconImg").GetComponent<SpriteRenderer>();
                    Require(image.enabled && image.sprite != null && image.gameObject.activeInHierarchy,
                        "Original status icon image is missing: " + phase);
                    Require(Mathf.Approximately(node.localScale.x, .48f)
                        && Mathf.Approximately(image.transform.localScale.x, .5f)
                        && image.sortingLayerID == 1182434123 && image.sortingOrder == 2,
                        "Original world status dimensions/sorting were replaced by the menu prefab.");
                }
                if (capture && (phase == "independent" || phase == "partial" || phase == "released")) Capture(phase);
                if (phase == "ended")
                {
                    if (capture)
                    {
                        var counter = new FightBattleStatusState { ClassId = 24009, InstanceId = 9924009, StackCount = 7 };
                        Check(new[] { counter }, new[] { counter }, "counter");
                        var text = layout.GetChild(0).Find("StackCountText").GetComponent<TMPro.TextMeshPro>();
                        Require(text.gameObject.activeSelf && text.text == "7"
                            && Mathf.Approximately(text.fontSize, 16f) && text.font.name.Contains("FZY4K"),
                            "Original world counter/font binding did not retain seven layers.");
                        text.ForceMeshUpdate(); Capture("counter");
                        Check(Array.Empty<FightBattleStatusState>(), Array.Empty<FightBattleStatusState>(), "counter-cleared");
                    }
                    Debug.Log("[Status icon hosts] original mutex instances: hidden while forbidden,"
                        + " independent equipment visible, partial release hidden, full release appended, native icon pool reused/cleared.");
                }
            }

            private void Capture(string phase)
            {
                var cameraObject = new GameObject("Status icon verification camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .08f, .08f);
                foreach (Transform node in body.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Canvas.ForceUpdateCanvases();
                var bounds = new Bounds(); bool initialized = false;
                foreach (var image in body.GetComponentsInChildren<Image>())
                {
                    var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                        if (!initialized) { bounds = new Bounds(corner, Vector3.zero); initialized = true; }
                        else bounds.Encapsulate(corner);
                }
                foreach (var renderer in body.GetComponentsInChildren<Renderer>())
                    if (!initialized) { bounds = renderer.bounds; initialized = true; }
                    else bounds.Encapsulate(renderer.bounds);
                camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                camera.orthographicSize = Mathf.Max(bounds.size.y * .65f, bounds.size.x * .65f / (675f / 360f));
                var texture = RenderTexture.GetTemporary(675, 360, 24);
                var previous = RenderTexture.active;
                try
                {
                    camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                    var image = new Texture2D(675, 360, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 675, 360), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                        "status-icons-" + phase + ".png"), image.EncodeToPNG());
                    UnityEngine.Object.Destroy(image);
                }
                finally
                {
                    RenderTexture.active = previous; camera.targetTexture = null;
                    RenderTexture.ReleaseTemporary(texture); UnityEngine.Object.Destroy(cameraObject);
                }
            }

            public void Dispose() { view.Dispose(); UnityEngine.Object.Destroy(body); }
        }
    }
}
