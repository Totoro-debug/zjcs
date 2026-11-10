using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStageStatusUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleStageStatusDialogView : MonoBehaviour
    {
        [Serializable] private sealed class Bindings
        {
            public string manifest;
            public int[] scaleStatusIds;
            public Popup popup;
            public Text[] texts;
        }
        [Serializable] private sealed class Popup
        {
            public int maskType;
            public bool closeByMask;
            public OfficialBattlePopupMask.Binding mask;
        }
        [Serializable] private sealed class Text { public string key, text; }

        private Bindings bindings;
        private OfficialBattleHudCatalog catalog;
        private FightStageStatusPresentation presentation;
        private WorldPopupWindow popup;
        private OfficialBattlePopupMask mask;
        private bool preparing, closing;
        private double propsScaleFactor = 1d;

        public bool IsReady => mask != null && mask.IsReady && popup.State == WorldPopupWindow.EState.Show;

        public static OfficialBattleStageStatusDialogView Open(Transform parent,
            OfficialBattleHudCatalog catalog, List<int> statusIds, FightBattleCheckpoint checkpoint)
        {
            var prefab = Resources.Load<GameObject>("Combat/StageStatusUI/FightStageStatusDialog");
            var source = Resources.Load<TextAsset>("Combat/StageStatusUI/texts");
            if (prefab == null || source == null)
                throw new InvalidOperationException("Original stage-status dialog assets are not installed.");
            var staging = new GameObject("BattleStageStatusStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false);
            staging.SetActive(false);
            GameObject instance = null;
            try
            {
                instance = Instantiate(prefab, staging.transform, false);
                var view = instance.AddComponent<OfficialBattleStageStatusDialogView>();
                view.catalog = catalog;
                view.bindings = JsonUtility.FromJson<Bindings>(source.text);
                if (view.bindings.manifest != "884_156692" || view.bindings.popup.maskType != 3)
                    throw new InvalidOperationException("Unpinned original stage-status dialog bindings.");
                view.preparing = checkpoint != null && checkpoint.PreparationPending;
                view.presentation = new FightStageStatusPresentation(view.Resolve, view.Bind);
                view.Bind(instance);
                instance.GetComponent<FightStageStatusDialog>().Init(new List<int>(statusIds), view.presentation);
                view.popup = instance.GetComponent<WorldPopupWindow>();
                view.popup.SetMaskClickAction(view.Close);
                instance.transform.SetParent(parent, false);
                instance.SetActive(true);
                view.mask = instance.AddComponent<OfficialBattlePopupMask>();
                view.mask.Show(view.bindings.popup.mask, view.bindings.popup.maskType,
                    view.bindings.popup.closeByMask, view.Close, view.popup.Show);
                return view;
            }
            catch { if (instance != null) Destroy(instance); throw; }
            finally { Destroy(staging); }
        }

        // The user-confirmed local rule fixes the server scale to one.
        // The input also exercises the original provider's update/format branches.
        public void Refresh(FightBattleCheckpoint checkpoint, double scaleFactor = 1d)
        {
            bool nextPreparing = checkpoint != null && checkpoint.PreparationPending;
            if (preparing == nextPreparing && propsScaleFactor == scaleFactor) return;
            preparing = nextPreparing;
            propsScaleFactor = scaleFactor;
            presentation.NotifyDescriptionChanged();
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            popup.Hide(() => Destroy(gameObject));
        }

        private FightStageStatusPresentation.Item Resolve(int statusId)
        {
            if (!catalog.TryGetStatusDetails(statusId, out string name, out string description))
                throw new InvalidOperationException("Original stage-status text is not bound: " + statusId);
            catalog.TryGetStatusSprites(statusId, out Sprite icon, out Sprite background, out _);
            bool scale = bindings.scaleStatusIds.Contains(statusId);
            if (scale && preparing)
                description += Format("fight_status_props_scale_format", Localize("fight_status_props_scale_not_exist"));
            else if (scale)
            {
                double factor = propsScaleFactor;
                string detail = (factor == 1d || Math.Abs(factor - 1d) < double.Epsilon) ? Localize("fight_status_props_not_scale")
                    : Format("fight_status_props_has_scale", ProfessionPropertyFormula.ToStandardSignificantPercent(
                        Math.Round(factor, 3), Zjcs.Official.World.NativeLabelUI.Framework.LanguageType.zh_CN));
                description += Format("fight_status_props_scale_format", detail);
            }
            return new FightStageStatusPresentation.Item { Icon = icon, Background = background,
                Name = name, Description = description, DescriptionUpdatable = scale };
        }

        private string Format(string key, object value) => string.Format(Localize(key), value);
        private string Localize(string key) => bindings.texts.FirstOrDefault(value => value.key == key)?.text ?? key;

        private void Bind(GameObject target)
        {
            foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>(true))
            { text.font = catalog.Font; text.fontSharedMaterial = catalog.Font.material; }
            foreach (WorldMultiLanguageText text in target.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                text.Localizer = Localize;
                text.FontProvider = () => catalog.Font;
                text.Refresh();
            }
            foreach (WorldPopupWindowSound sound in target.GetComponentsInChildren<WorldPopupWindowSound>(true))
                sound.Post = (clip, owner) =>
                {
                    var audio = owner.GetComponent<AudioSource>();
                    if (audio == null) audio = owner.AddComponent<AudioSource>();
                    audio.playOnAwake = false;
                    audio.PlayOneShot(clip);
                };
        }
    }
}
