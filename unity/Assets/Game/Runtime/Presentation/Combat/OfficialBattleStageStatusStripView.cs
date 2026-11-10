using System;
using TMPro;
using UnityEngine;
using Zjcs.Official.BattleStageStatusUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    internal static class OfficialBattleStageStatusStripView
    {
        internal static FightMainPanelStageStatus Mount(GameObject previous,
            OfficialBattleHudCatalog catalog, Action open)
        {
            if (previous == null) throw new InvalidOperationException("Original stage-status parent is missing.");
            var prefab = Resources.Load<GameObject>("Combat/StageStatusUI/StageStatus");
            if (prefab == null) throw new InvalidOperationException("Original stage-status strip is not installed.");
            Transform parent = previous.transform.parent;
            var staging = new GameObject("BattleStageStatusStripStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false);
            staging.SetActive(false);
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                var presentation = new FightStageStatusPresentation(id =>
                {
                    if (!catalog.TryGetStatusSprites(id, out Sprite icon, out Sprite background, out _))
                        throw new InvalidOperationException("Original stage-status icon is not bound: " + id);
                    return new FightStageStatusPresentation.Item { Icon = icon, Background = background };
                }, target =>
                {
                    foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>(true))
                    { text.font = catalog.Font; text.fontSharedMaterial = catalog.Font.material; }
                    foreach (WorldMultiLanguageText text in target.GetComponentsInChildren<WorldMultiLanguageText>(true))
                        text.FontProvider = () => catalog.Font;
                });
                var status = instance.GetComponent<FightMainPanelStageStatus>();
                status.Init(null, presentation, open);
                foreach (WorldButtonSound sound in instance.GetComponentsInChildren<WorldButtonSound>(true))
                    sound.Post = (clip, owner) =>
                    {
                        var audio = owner.GetComponent<AudioSource>();
                        if (audio == null) audio = owner.AddComponent<AudioSource>();
                        audio.playOnAwake = false;
                        audio.PlayOneShot(clip);
                    };
                int sibling = previous.transform.GetSiblingIndex();
                string name = previous.name;
                previous.SetActive(false);
                previous.name = name + "_RetainedBinding";
                instance.transform.SetParent(parent, false);
                instance.name = name;
                instance.transform.SetSiblingIndex(sibling);
                instance.SetActive(true);
                return status;
            }
            catch { if (instance != null) UnityEngine.Object.Destroy(instance); throw; }
            finally { UnityEngine.Object.Destroy(staging); }
        }
    }
}
