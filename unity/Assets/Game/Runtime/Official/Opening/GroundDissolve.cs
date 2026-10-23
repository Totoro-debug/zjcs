using UnityEngine;

namespace Zjcs.Official.Opening
{
    // Official GroundDissolve, namespace adapted for the desktop project.
    [ExecuteAlways]
    public sealed class GroundDissolve : MonoBehaviour
    {
        [Range(0f, 1f)] public float Dissolve;
        public Material Material;
        public AnimationCurve ProgressCurve;
        public AnimationCurve AlphaCurve;
        private static readonly int DissolveAlpha = Shader.PropertyToID("_DissolveAlpha");
        private static readonly int DissolveProgress = Shader.PropertyToID("_DissolveProgress");
        public static GroundDissolve Instance { get; private set; }

        private void OnEnable() => Instance = this;
        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Material == null) return;
            Material.SetFloat(DissolveProgress, ProgressCurve.Evaluate(Dissolve));
            Material.SetFloat(DissolveAlpha, AlphaCurve.Evaluate(1f - Dissolve));
        }
    }
}
