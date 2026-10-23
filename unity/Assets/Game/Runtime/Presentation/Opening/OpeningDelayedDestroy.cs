using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    /// <summary>Keeps a defeated unit alive long enough to finish its death animation.</summary>
    public sealed class OpeningDelayedDestroy : MonoBehaviour
    {
        public float Delay = 0.9f;
        private float elapsed;

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Delay) Destroy(gameObject);
        }
    }
}
