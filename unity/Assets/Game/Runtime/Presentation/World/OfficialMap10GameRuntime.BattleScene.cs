using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        private readonly Dictionary<GameObject, bool> battleHiddenWorldObjects = new Dictionary<GameObject, bool>();
        private float preBattleCameraSize;
        private bool battleOwnsCamera;

        // FightSceneRoot/FightRoot activate a separate scene in the official client. The offline
        // adapter shares one Unity scene, so suspend the world roots while retaining their states.
        private void SuspendWorldForBattle()
        {
            if (battleHiddenWorldObjects.Count > 0) return;
            foreach (Transform child in mapRoot)
            {
                if (child == avatar.transform || child == battleRoot) continue;
                battleHiddenWorldObjects.Add(child.gameObject, child.gameObject.activeSelf);
                child.gameObject.SetActive(false);
            }
            preBattleCameraSize = worldCamera.orthographicSize;
            battleOwnsCamera = battleController.TryGetSceneCamera(out Vector3 position, out float size);
            if (battleOwnsCamera)
            {
                worldCamera.transform.position = position;
                worldCamera.orthographicSize = size;
            }
        }

        private bool UpdateBattleSceneCamera()
        {
            if (!battleOwnsCamera || battleController == null
                || !battleController.TryGetSceneCamera(out Vector3 position, out float size)) return false;
            worldCamera.transform.position = position;
            worldCamera.orthographicSize = size;
            return true;
        }

        private void RestoreWorldAfterBattle()
        {
            foreach (KeyValuePair<GameObject, bool> item in battleHiddenWorldObjects)
                if (item.Key != null) item.Key.SetActive(item.Value);
            battleHiddenWorldObjects.Clear();
            if (battleOwnsCamera && worldCamera != null)
            {
                worldCamera.orthographicSize = preBattleCameraSize;
                worldCamera.transform.position = avatar.transform.position + new Vector3(0f, 0f, -20f);
            }
            battleOwnsCamera = false;
        }
    }
}
