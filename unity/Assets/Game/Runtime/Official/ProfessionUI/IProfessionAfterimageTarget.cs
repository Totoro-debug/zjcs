using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Reads the completed original pose; capturing never advances animation.
    public interface IProfessionAfterimageTarget
    {
        Vector3 GetCapturePosition(string boneName, Vector3 worldOffset);
        void CollectRenderers(List<MeshRenderer> renderers, Comparer<MeshRenderer> comparer);
    }
}
