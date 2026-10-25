using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleTimelineLifetime : MonoBehaviour
    {
        private GameObject worldRoot;
        public void Initialize(GameObject root) { worldRoot = root; worldRoot.SetActive(gameObject.activeInHierarchy); }
        private void OnEnable() { if (worldRoot != null) worldRoot.SetActive(true); }
        private void OnDisable() { if (worldRoot != null) worldRoot.SetActive(false); }
        private void OnDestroy()
        {
            if (worldRoot == null) return;
            if (Application.isPlaying) Destroy(worldRoot);
            else DestroyImmediate(worldRoot);
        }
    }
}
