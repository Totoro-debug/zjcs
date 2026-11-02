using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        // Observe the caller's real rendered geometry in character-local space,
        // so camera/grid movement cannot pass an otherwise frozen-pose check.
        public static IEnumerator VerifyBattleCharacterMotion(Transform player)
        {
            Dictionary<MeshFilter, Vector3[]> CaptureVertices() => player.GetComponentsInChildren<MeshFilter>()
                .Where(filter => filter.sharedMesh != null && filter.GetComponent<Renderer>() is Renderer renderer
                    && renderer.enabled && !renderer.forceRenderingOff)
                .ToDictionary(filter => filter, filter => filter.sharedMesh.vertices.Select(vertex =>
                    player.InverseTransformPoint(filter.transform.TransformPoint(vertex))).ToArray());
            var before = CaptureVertices();
            Require(before.Count > 0, "The player has no rendered native geometry to verify.");
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            var after = CaptureVertices();
            bool changed = before.Any(pair => after.TryGetValue(pair.Key, out var vertices)
                && pair.Value.Length == vertices.Length
                && pair.Value.Where((vertex, index) => Vector3.Distance(vertex, vertices[index]) > .0001f).Any());
            Require(changed, "The player's rendered geometry stayed in the setup pose while its battle idle clock advanced.");
        }
    }
}
