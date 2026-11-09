using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Observes the real authored graph and its GPU capture, without driving it.
    internal sealed class OfficialBattleAfterimageValidation
    {
        private readonly HashSet<AfterimageData> captures = new HashSet<AfterimageData>();
        private bool hasPixels, visible;
        internal bool Observe()
        {
            bool visibleNow = false;
            foreach (var playable in UnityEngine.Object.FindObjectsOfType<MyPlayableAfterimage>())
            {
                if (!(playable.myApplyComponent is ProfessionNativeSceneRenderer source)) continue;
                foreach (var data in playable.AfterimageDatas)
                {
                    if (!data.RTHandle || !data.RTRenderer) continue;
                    Require(playable.Time >= data.RecordTime, "Captured before original record time.");
                    var renderer = data.RTRenderer.GetComponent<MeshRenderer>();
                    var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                    Color actual = properties.GetColor("_Color"), expected = data.GetColor(playable.Time);
                    Require(Vector4.Distance(actual, expected) < .0001f, "Original fade/color differs.");
                    if (captures.Add(data))
                    {
                        var targets = new List<MeshRenderer>();
                        ((IProfessionAfterimageTarget)source).CollectRenderers(targets, Comparer<MeshRenderer>.Create(
                            (a, b) => a.transform.position.z.CompareTo(b.transform.position.z)));
                        Require(targets.Count(value => value && value.enabled && value.gameObject.activeInHierarchy
                            && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0) >= 2,
                            "Original body/appearance meshes are absent.");
                        ReadPixels(data.RTHandle);
                    }
                    if (actual.a > 0 && renderer.enabled && renderer.isVisible && renderer.gameObject.activeInHierarchy)
                        visibleNow = visible = true;
                }
            }
            return visibleNow;
        }

        private void ReadPixels(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            var sample = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                sample.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); sample.Apply();
                int pixels = sample.GetPixels32().Count(value => value.a > 0);
                Require(pixels > 0, "Actual afterimage render texture is empty.");
                if (!hasPixels)
                    File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                        "afterimage-native-capture.png"), sample.EncodeToPNG());
                hasPixels = true;
                Debug.Log("[Native afterimage] captured " + pixels + " nontransparent pixels at "
                    + texture.width + "x" + texture.height + ".");
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(sample); }
        }

        internal void Verify()
        {
            Require(captures.Count > 0 && hasPixels && visible, "No visible native afterimage was captured.");
            Require(captures.All(value => !value.RTHandle && !value.RTRenderer), "Capture resources were not returned on stop.");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Native afterimage diagnostic: " + message); }
    }
}
