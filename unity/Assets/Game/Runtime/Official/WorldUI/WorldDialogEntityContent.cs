using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    public sealed class WorldDialogEntityContent : MonoBehaviour
    {
        public Transform mPreviewSlot;
        private GameObject icon;
        public void Init(WorldStageUiContext stage)
        {
            if (mPreviewSlot == null) return;
            mPreviewSlot.gameObject.SetActive(stage.DialogIconPrefab != null);
            if (icon != null) Destroy(icon);
            if (stage.DialogIconPrefab != null) icon = Instantiate(stage.DialogIconPrefab, mPreviewSlot, false);
        }
    }
}
